using System;
using System.Collections.Generic;
using System.Globalization;
using Deepshaft.Bosses;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.Save;
using Deepshaft.Smith;
using Deepshaft.Summoning;
using Deepshaft.State;
using Deepshaft.Time;
using UnityEngine;

namespace Deepshaft.Session
{
    /// <summary>What happened while the player was away, for the "Welcome back" message.</summary>
    public sealed class OfflineReport
    {
        public OfflineReport(double hoursAway, List<MinedAmount> mine, List<MinedAmount> expedition)
        {
            HoursAway = hoursAway;
            Mine = mine;
            Expedition = expedition;
        }

        /// <summary>Time between the last save and this load. 0 for a new game.</summary>
        public double HoursAway { get; }

        /// <summary>Everything waiting in the Mine (includes what was left uncollected before).</summary>
        public IReadOnlyList<MinedAmount> Mine { get; }

        /// <summary>Everything the running automated Expedition has earned. Empty if none runs.</summary>
        public IReadOnlyList<MinedAmount> Expedition { get; }
    }

    /// <summary>
    /// One running game (SCRUM-30): loads the save, builds every system in the right order,
    /// gives a new game its starter Minions, and saves after each action.
    ///
    /// The UI talks to the game through this class. It reads through the service properties
    /// (<see cref="Mining"/>, <see cref="Roster"/>, ...) and changes things through the action
    /// methods here, which save and raise <see cref="Changed"/>. Everything runs from the
    /// <see cref="IClock"/>, so nothing needs a per-frame update; the host only calls
    /// <see cref="Save"/> now and then and when the app pauses or quits.
    ///
    /// Pure C#: no MonoBehaviour, so it runs in EditMode tests. The Unity side is GameSessionRunner.
    /// </summary>
    public class GameSession
    {
        readonly SaveService saves;
        readonly ISaveStorage storage;
        readonly System.Random rng;

        /// <param name="seed">Fixed seed for repeatable fights (tests). Null: a random seed.</param>
        public GameSession(GameDatabase database, ISaveStorage storage, IClock clock, int? seed = null)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            saves = new SaveService(storage, clock);
            LoadResult loaded = saves.Load();
            State = loaded.State;
            LoadStatus = loaded.Status;
            long lastSavedMs = State.lastSavedAtUnixMs;

            // Wiring order (Technical Architecture §11): each service gets what it depends on.
            Wallet = new Wallet(State.wallet);
            Mining = new MiningService(State, database, clock, new EquipmentToolBonus(() => database.Equipment));
            Tiers = new TierProgressionService(State, database, Mining);
            Expeditions = new ExpeditionService(State, database, clock, Tiers);
            Roster = new RosterService(State, database, Mining, Expeditions);
            Bosses = new BossService(State, database, Expeditions, Tiers);
            Minions = new MinionProgressionService(State, database, Mining);
            Smith = new SmithService(State, database, Mining);
            Summoning = new SummonService(State, database, Roster);

            Playtest = new PlaytestLog(State.playtest, clock);
            if (State.minions.Count == 0) GrantStarterMinions();

            bool fromSave = LoadStatus == LoadStatus.Loaded || LoadStatus == LoadStatus.LoadedFromBackup;
            double hoursAway = fromSave
                ? Math.Max(0.0, (UnixTime.ToUnixMs(clock.UtcNow) - lastSavedMs) / 3600000.0)
                : 0.0;
            Offline = new OfflineReport(hoursAway, Mining.GetPending(), Expeditions.GetPending());
            Playtest.Add(fromSave ? "session_start" : "new_game",
                "hours_away=" + hoursAway.ToString("0.0", CultureInfo.InvariantCulture) + " tier=" + Tiers.HighestUnlockedTier +
                " waiting_mine=" + Describe(Offline.Mine) + " waiting_expedition=" + Describe(Offline.Expedition));

            Save();
        }

        public GameDatabase Database { get; }
        public IClock Clock { get; }
        public GameState State { get; }
        public LoadStatus LoadStatus { get; }
        public OfflineReport Offline { get; }

        public Wallet Wallet { get; }
        public MiningService Mining { get; }
        public TierProgressionService Tiers { get; }
        public ExpeditionService Expeditions { get; }
        public RosterService Roster { get; }
        public BossService Bosses { get; }
        public MinionProgressionService Minions { get; }
        public SmithService Smith { get; }
        public SummonService Summoning { get; }

        /// <summary>Local playtest event log (SCRUM-8). Settings → Debug copies it.</summary>
        public PlaytestLog Playtest { get; }

        /// <summary>False after loading a save from a newer build: nothing will be written.</summary>
        public bool SavingAllowed => saves.SavingAllowed;

        /// <summary>Raised after every action, so screens can refresh.</summary>
        public event Action Changed;

        /// <summary>
        /// Everything earned per hour right now (SCRUM-9, HQ): the Mine plus the running automated
        /// Expedition, one entry per resource.
        /// </summary>
        public List<MinedAmount> GetProductionPerHour()
        {
            var total = new List<MinedAmount>();
            MiningMath.AddProduction(total, Mining.GetHourlyRates(), 1.0);
            MiningMath.AddProduction(total, Expeditions.GetHourlyRates(), 1.0);
            return total;
        }

        /// <summary>Banks Mining and the running Expedition, then writes the save. Safe to call any time.</summary>
        public bool Save()
        {
            Mining.Bank();
            Expeditions.Bank();
            return saves.Save(State);
        }

        /// <summary>Deletes the save from storage. Create a new GameSession afterwards to start over.</summary>
        public void DeleteSave() => storage.DeleteAll();

        // ---------- Actions ----------
        // Each action is also written to the playtest log (SCRUM-8).

        public List<ResourceAmount> CollectMine()
        {
            List<ResourceAmount> collected = Mining.Collect(Wallet);
            Playtest.Add("collect_mine", Describe(collected));
            return Done(collected);
        }

        public AssignResult AssignMiner(int instanceId) => Done(LogAssign("assign_miner", instanceId, Roster.AssignMiner(instanceId)));
        public AssignResult UnassignMiner(int instanceId) => Done(LogAssign("unassign_miner", instanceId, Roster.UnassignMiner(instanceId)));
        public AssignResult AssignToTeam(int instanceId) => Done(LogAssign("assign_team", instanceId, Roster.AssignToTeam(instanceId)));
        public AssignResult RemoveFromTeam(int instanceId) => Done(LogAssign("remove_team", instanceId, Roster.RemoveFromTeam(instanceId)));

        public BossFightResult FightBoss(string bossId)
        {
            float power = Bosses.GetTeamPower();
            BossFightResult result = Bosses.Fight(bossId, Wallet, rng);
            float recommended = Database.FindBoss(bossId)?.recommendedPower ?? 0f;
            Playtest.Add("boss_fight", bossId + " " + (result.Check != BossCheck.Ok ? result.Check.ToString() : result.Won ? "won" : "lost") +
                                       " power=" + Num(power) + " rec=" + Num(recommended) + (result.FirstWin ? " first_win" : ""));
            return Done(result);
        }

        public FirstClearResult FirstClearExpedition(string expeditionId)
        {
            float power = Expeditions.GetTeamPower();
            FirstClearResult result = Expeditions.TryFirstClear(expeditionId, Wallet, rng);
            float recommended = Database.FindExpedition(expeditionId)?.recommendedPower ?? 0f;
            Playtest.Add("first_clear", expeditionId + " " + (result.Check != ExpeditionCheck.Ok ? result.Check.ToString() : result.Won ? "won" : "lost") +
                                        " power=" + Num(power) + " rec=" + Num(recommended));
            return Done(result);
        }

        public ExpeditionCheck StartExpedition(string expeditionId)
        {
            ExpeditionCheck check = Expeditions.Start(expeditionId);
            Playtest.Add("expedition_start", expeditionId + " " + check);
            return Done(check);
        }

        public ExpeditionCollectResult StopExpedition()
        {
            ExpeditionCollectResult result = Expeditions.StopAndCollect(Wallet);
            Playtest.Add("expedition_stop", result.ExpeditionId + " hours=" + result.HoursRun.ToString("0.0", CultureInfo.InvariantCulture) +
                                            " " + Describe(result.Collected));
            return Done(result);
        }

        /// <summary>Pays for and unlocks the next tier, for tiers with a cost. Free tiers unlock by themselves.</summary>
        public TierUnlockCheck UnlockNextTier()
        {
            TierUnlockCheck check = Tiers.TryUnlockNext(Wallet);
            Playtest.Add("tier_unlock", "tier=" + check.Tier + " " + check.Status);
            return Done(check);
        }

        /// <summary>Pays Gold + ores and raises a Minion one level (SCRUM-10).</summary>
        public MinionUpgradeCheck LevelUpMinion(int instanceId)
        {
            MinionUpgradeCheck result = Minions.TryLevelUp(instanceId, Wallet);
            Playtest.Add("level_up", MinionName(instanceId) + " " + result.Status + " level=" + result.Level);
            return Done(result);
        }

        /// <summary>
        /// Buys as many levels as the player can afford, up to the cap, in one tap (SCRUM-32).
        /// Saves and logs once, not per level.
        /// </summary>
        public BulkPurchase LevelUpMinionMax(int instanceId)
        {
            BulkPurchase bought = Minions.TryLevelUpMax(instanceId, Wallet);
            Playtest.Add("level_up_max", MinionName(instanceId) + " levels=" + bought.Levels + " level=" + bought.ToLevel);
            return Done(bought);
        }

        /// <summary>Pays a rare Expedition material and raises a Minion's level cap (SCRUM-10).</summary>
        public MinionUpgradeCheck BreakthroughMinion(int instanceId)
        {
            MinionUpgradeCheck result = Minions.TryBreakthrough(instanceId, Wallet);
            Playtest.Add("minion_breakthrough", MinionName(instanceId) + " " + result.Status + " cap=" + result.MaxLevel);
            return Done(result);
        }

        /// <summary>Smith: pays Gold + ores and raises one item of one Minion a level (SCRUM-9).</summary>
        public SmithCheck UpgradeGear(int instanceId, EquipmentSlot slot)
        {
            SmithCheck result = Smith.TryUpgrade(instanceId, slot, Wallet);
            Playtest.Add("gear_upgrade", MinionName(instanceId) + " " + slot + " " + result.Status + " level=" + result.Level);
            return Done(result);
        }

        /// <summary>Smith: buys as many levels of one item as the player can afford, up to its cap (SCRUM-32).</summary>
        public BulkPurchase UpgradeGearMax(int instanceId, EquipmentSlot slot)
        {
            BulkPurchase bought = Smith.TryUpgradeMax(instanceId, slot, Wallet);
            Playtest.Add("gear_upgrade_max", MinionName(instanceId) + " " + slot + " levels=" + bought.Levels + " level=" + bought.ToLevel);
            return Done(bought);
        }

        /// <summary>Smith: pays a rare Expedition material and raises the item's level cap (SCRUM-9).</summary>
        public SmithCheck BreakthroughGear(int instanceId, EquipmentSlot slot)
        {
            SmithCheck result = Smith.TryBreakthrough(instanceId, slot, Wallet);
            Playtest.Add("gear_breakthrough", MinionName(instanceId) + " " + slot + " " + result.Status + " cap=" + result.MaxLevel);
            return Done(result);
        }

        /// <summary>Pays shards and summons one Minion from the Miner or Adventure pool (SCRUM-11).</summary>
        public SummonResult Summon(MinionKind pool, ShardType shard)
        {
            SummonResult result = Summoning.TrySummon(pool, shard, Wallet, rng);
            Playtest.Add("summon", pool + " " + shard + " " + (!result.Ok ? result.Status.ToString()
                : (result.IsDuplicate ? "duplicate " : "new ") + result.Definition.Id + " " + result.Definition.Rarity +
                  (result.IsDuplicate ? " essence=" + result.Essence : "")));
            return Done(result);
        }

        /// <summary>Smith: pays a unique boss material and teaches the Smith its next rank (SCRUM-9).</summary>
        public SmithCheck UpgradeSmith()
        {
            SmithCheck result = Smith.TryRankUp(Wallet);
            Playtest.Add("smith_rank", result.Status + " rank=" + Smith.Rank);
            return Done(result);
        }

        /// <summary>
        /// For the UI: records something the player looked at (e.g. the tab they opened), so the
        /// playtest log shows where they went after a loss. Doesn't save by itself.
        /// </summary>
        public void LogView(string what) => Playtest.Add("view", what);

        AssignResult LogAssign(string type, int instanceId, AssignResult result)
        {
            Playtest.Add(type, MinionName(instanceId) + " " + result);
            return result;
        }

        string MinionName(int instanceId)
        {
            OwnedMinion m = State.FindMinion(instanceId);
            return m != null ? m.definitionId + "#" + instanceId : "#" + instanceId;
        }

        static string Num(float value) => value.ToString("0", CultureInfo.InvariantCulture);

        static string Describe(IEnumerable<ResourceAmount> amounts)
        {
            var parts = new List<string>();
            if (amounts != null) foreach (ResourceAmount a in amounts) if (a.amount > 0) parts.Add(a.amount + " " + a.id);
            return parts.Count > 0 ? string.Join(" + ", parts) : "nothing";
        }

        static string Describe(IEnumerable<MinedAmount> amounts)
        {
            var parts = new List<string>();
            if (amounts != null) foreach (MinedAmount a in amounts) if (a.amount >= 1.0) parts.Add(Math.Floor(a.amount) + " " + a.id);
            return parts.Count > 0 ? string.Join(" + ", parts) : "nothing";
        }

        T Done<T>(T result)
        {
            Save();
            Changed?.Invoke();
            return result;
        }

        /// <summary>A new game: the starter Minions and the starting resources (SCRUM-32).</summary>
        void GrantStarterMinions()
        {
            Wallet.Add(Database.NewGameResources);
            foreach (string id in Database.StarterMinions)
            {
                if (Database.FindMinion(id) == null)
                {
                    Debug.LogWarning("GameSession: starter Minion '" + id + "' is not in the GameDatabase.");
                    continue;
                }
                Roster.AddMinion(id);
            }
        }
    }
}
