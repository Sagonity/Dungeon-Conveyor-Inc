using System;
using System.Collections.Generic;
using Deepshaft.Bosses;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.Save;
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
            Mining = new MiningService(State, database, clock);
            Tiers = new TierProgressionService(State, database, Mining);
            Expeditions = new ExpeditionService(State, database, clock, Tiers);
            Roster = new RosterService(State, database, Mining, Expeditions);
            Bosses = new BossService(State, database, Expeditions, Tiers);

            if (State.minions.Count == 0) GrantStarterMinions();

            bool fromSave = LoadStatus == LoadStatus.Loaded || LoadStatus == LoadStatus.LoadedFromBackup;
            double hoursAway = fromSave
                ? Math.Max(0.0, (UnixTime.ToUnixMs(clock.UtcNow) - lastSavedMs) / 3600000.0)
                : 0.0;
            Offline = new OfflineReport(hoursAway, Mining.GetPending(), Expeditions.GetPending());

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

        /// <summary>False after loading a save from a newer build: nothing will be written.</summary>
        public bool SavingAllowed => saves.SavingAllowed;

        /// <summary>Raised after every action, so screens can refresh.</summary>
        public event Action Changed;

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

        public List<ResourceAmount> CollectMine() => Done(Mining.Collect(Wallet));

        public AssignResult AssignMiner(int instanceId) => Done(Roster.AssignMiner(instanceId));
        public AssignResult UnassignMiner(int instanceId) => Done(Roster.UnassignMiner(instanceId));
        public AssignResult AssignToTeam(int instanceId) => Done(Roster.AssignToTeam(instanceId));
        public AssignResult RemoveFromTeam(int instanceId) => Done(Roster.RemoveFromTeam(instanceId));

        public BossFightResult FightBoss(string bossId) => Done(Bosses.Fight(bossId, Wallet, rng));

        public FirstClearResult FirstClearExpedition(string expeditionId) =>
            Done(Expeditions.TryFirstClear(expeditionId, Wallet, rng));

        public ExpeditionCheck StartExpedition(string expeditionId) => Done(Expeditions.Start(expeditionId));

        public ExpeditionCollectResult StopExpedition() => Done(Expeditions.StopAndCollect(Wallet));

        /// <summary>Pays for and unlocks the next tier, for tiers with a cost. Free tiers unlock by themselves.</summary>
        public TierUnlockCheck UnlockNextTier() => Done(Tiers.TryUnlockNext(Wallet));

        /// <summary>
        /// Debug only, until Minion levels and breakthroughs exist (SCRUM-10): raises every owned
        /// Minion by <paramref name="levels"/>. Ignores the level cap, because breakthroughs can't
        /// be done yet. Lets the playtest reach Tier 2 and 3.
        /// </summary>
        public void DebugLevelUpAll(int levels = 1)
        {
            if (levels <= 0) return;
            Mining.ChangeRate(() =>
            {
                foreach (OwnedMinion m in State.minions) m.level = Math.Max(1, m.level) + levels;
            });
            Done(0);
        }

        T Done<T>(T result)
        {
            Save();
            Changed?.Invoke();
            return result;
        }

        void GrantStarterMinions()
        {
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
