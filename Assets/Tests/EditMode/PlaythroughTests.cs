using System;
using System.Collections.Generic;
using System.Linq;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Save;
using Deepshaft.Session;
using Deepshaft.Summoning;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>
    /// Pacing check (SCRUM-8, SCRUM-2): a simple free player plays the whole prototype through the
    /// GameSession with the default content and numbers, and must clear all 3 tiers without paying.
    /// It runs for three play rhythms (check in every hour, every 8 hours, once a day) and several
    /// seeds, and prints when each step was first cleared. Run it after every tuning change and read
    /// the output (Test Runner → select the test → output pane).
    ///
    /// The roster is the prototype's: the 7 starter Commons plus the Uncommon (+25% stats) and Rare
    /// (+60%) Minions that summons can give (Content/Minions).
    ///
    /// Each check-in the player: collects the Mine and the automated Expedition (stops it), learns
    /// Smith ranks, summons with every shard, puts its best Minions to work, does every breakthrough
    /// it can, buys the cheapest affordable upgrade until nothing is affordable, then fights every next
    /// step it's within 90% of, and restarts the deepest cleared Expedition. It plays perfectly, so
    /// real players are slower.
    /// </summary>
    public class PlaythroughTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        const int MaxHours = 24 * 60;
        static readonly string[] StepNames = { "T1 boss", "T1 exped", "T2 boss", "T2 exped", "T3 boss", "T3 exped" };

        readonly List<MinionDefinition> definitions = new List<MinionDefinition>();
        GameDatabase database;

        [SetUp]
        public void SetUp()
        {
            const MinionKind M = MinionKind.Miner;
            const MinionKind A = MinionKind.Adventure;
            Add("Minion_Miner:C", M, MinionRole.None, Rarity.Common, MinerTrait.Gold);
            Add("Minion_Miner_Ore:C", M, MinionRole.None, Rarity.Common, MinerTrait.Ore);
            Add("Minion_Miner_Speed:C", M, MinionRole.None, Rarity.Common, MinerTrait.Speed);
            Add("Minion_Miner:U", M, MinionRole.None, Rarity.Uncommon, MinerTrait.Gold);
            Add("Minion_Miner_Speed:U", M, MinionRole.None, Rarity.Uncommon, MinerTrait.Speed);
            Add("Minion_Miner_Ore:R", M, MinionRole.None, Rarity.Rare, MinerTrait.Ore);
            Add("Minion_Miner_Speed:R", M, MinionRole.None, Rarity.Rare, MinerTrait.Speed);
            foreach (MinionRole role in new[] { MinionRole.Tank, MinionRole.Fighter, MinionRole.Support, MinionRole.Mage, MinionRole.Archer })
            {
                Add("Minion_" + role + ":C", A, role, Rarity.Common);
                Add("Minion_" + role + ":U", A, role, Rarity.Uncommon);
            }
            foreach (MinionRole role in new[] { MinionRole.Tank, MinionRole.Mage, MinionRole.Archer })
            {
                Add("Minion_" + role + ":R", A, role, Rarity.Rare);
            }

            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(definitions);
            database.EditorSetStarterMinions(new[]
            {
                "Minion_Miner:C", "Minion_Miner_Ore:C", "Minion_Tank:C", "Minion_Fighter:C",
                "Minion_Support:C", "Minion_Mage:C", "Minion_Archer:C",
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in definitions) UnityEngine.Object.DestroyImmediate(d);
            definitions.Clear();
            UnityEngine.Object.DestroyImmediate(database);
        }

        void Add(string id, MinionKind kind, MinionRole role, Rarity rarity, MinerTrait trait = MinerTrait.None)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, role, rarity, trait);
            d.EditorScaleStats(rarity == Rarity.Common ? 1f : rarity == Rarity.Uncommon ? 1.25f : 1.6f);
            definitions.Add(d);
        }

        /// <param name="checkInEveryHours">1 = very active; 8 = about three sessions a day; 24 = once a day.</param>
        [TestCase(1)]
        [TestCase(8)]
        [TestCase(24)]
        public void FreePlayer_ClearsAllThreeTiers(int checkInEveryHours)
        {
            TestContext.WriteLine("Check-in every " + checkInEveryHours + " h. Hour each step was first cleared:");
            TestContext.WriteLine("seed | " + string.Join(" | ", StepNames) + " | visits | nothing to buy | lost fights | minion BT | gear BT | summons new/dup");

            for (int seed = 1; seed <= 5; seed++)
            {
                RunResult r = Play(checkInEveryHours, seed);
                TestContext.WriteLine(seed + " | " + string.Join(" | ", r.ClearedAt.Select(h => h.HasValue ? h.Value.ToString() : "-")) +
                                      " | " + r.Visits + " | " + r.IdleVisits + " | " + r.LostFights + " | " + r.MinionBreakthroughs +
                                      " | " + r.GearBreakthroughs + " | " + r.NewSummons + "/" + r.DuplicateSummons);
                Assert.IsTrue(r.ClearedAt.All(h => h.HasValue),
                    "Seed " + seed + ": not everything was cleared in " + MaxHours + " h. A free player hit a dead end.");
            }
        }

        sealed class RunResult
        {
            public readonly int?[] ClearedAt = new int?[6];
            public int Visits, IdleVisits, LostFights, MinionBreakthroughs, GearBreakthroughs, NewSummons, DuplicateSummons;
        }

        RunResult Play(int every, int seed)
        {
            var clock = new ManualClock(Start);
            var game = new GameSession(database, new MemorySaveStorage(), clock, seed);
            var result = new RunResult();

            for (int hour = 0; hour <= MaxHours; hour += every)
            {
                if (hour > 0) clock.Advance(TimeSpan.FromHours(every));
                result.Visits++;
                int upgradesBefore = game.Playtest.Events.Count(IsUpgrade);
                CheckIn(game, hour, result);
                if (game.Playtest.Events.Count(IsUpgrade) == upgradesBefore) result.IdleVisits++;
                if (result.ClearedAt[5].HasValue) break;
            }

            // The playtest log (SCRUM-8) already records everything; count from it.
            foreach (PlaytestEvent e in game.Playtest.Events)
            {
                bool ok = e.detail.Contains(" Ok ");
                if (e.type == "minion_breakthrough" && ok) result.MinionBreakthroughs++;
                if (e.type == "gear_breakthrough" && ok) result.GearBreakthroughs++;
                if (e.type == "summon" && e.detail.Contains(" new ")) result.NewSummons++;
                if (e.type == "summon" && e.detail.Contains(" duplicate ")) result.DuplicateSummons++;
                if ((e.type == "boss_fight" || e.type == "first_clear") && e.detail.Contains(" lost ")) result.LostFights++;
            }
            return result;
        }

        static bool IsUpgrade(PlaytestEvent e) =>
            (e.type == "level_up" || e.type == "gear_upgrade" || e.type == "smith_rank" || e.type.EndsWith("breakthrough"))
            && e.detail.Contains(" Ok ");

        void CheckIn(GameSession game, int hour, RunResult result)
        {
            game.CollectMine();
            // Expedition loot only reaches the wallet when the run is stopped; it's restarted below.
            if (game.Expeditions.IsRunning) game.StopExpedition();
            while (game.Smith.CheckRankUp(game.Wallet).CanDo) game.UpgradeSmith();

            while (game.Summoning.Check(MinionKind.Adventure, ShardType.Advanced, game.Wallet).Ok) game.Summon(MinionKind.Adventure, ShardType.Advanced);
            MinionKind pool = MinionKind.Adventure;
            while (game.Summoning.Check(pool, ShardType.Basic, game.Wallet).Ok)
            {
                game.Summon(pool, ShardType.Basic);
                pool = pool == MinionKind.Adventure ? MinionKind.Miner : MinionKind.Adventure;
            }
            PutBestToWork(game);

            foreach (OwnedMinion m in game.State.minions.ToArray())
            {
                if (game.Minions.CheckBreakthrough(m.instanceId, game.Wallet).CanDo) game.BreakthroughMinion(m.instanceId);
                foreach (EquipmentSlot slot in AllSlots)
                {
                    if (game.Smith.CheckBreakthrough(m.instanceId, slot, game.Wallet).CanDo) game.BreakthroughGear(m.instanceId, slot);
                }
            }
            while (BuyCheapestUpgrade(game)) { }

            FightEverythingWithinReach(game, hour, result);

            if (!game.Expeditions.IsRunning)
            {
                ExpeditionDefinition best = game.Expeditions.GetUnlocked()
                    .Where(e => game.Expeditions.IsCleared(e.id)).OrderByDescending(e => e.tier).FirstOrDefault();
                if (best != null) game.StartExpedition(best.id);
            }
        }

        /// <summary>Fights the next step (boss, then first clear, tier by tier) while the team is within 90% of it.</summary>
        void FightEverythingWithinReach(GameSession game, int hour, RunResult result)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int step = NextStep(game);
                if (step >= 6) return;
                int tier = step / 2 + 1;
                float power = game.Bosses.GetTeamPower();
                bool won;
                if (step % 2 == 0)
                {
                    var boss = database.FindBossForTier(tier);
                    if (power < boss.recommendedPower * 0.9f || game.Tiers.HighestUnlockedTier < tier) return;
                    won = game.FightBoss(boss.id).Won;
                }
                else
                {
                    ExpeditionDefinition e = database.Expeditions.First(x => x.tier == tier);
                    if (power < e.recommendedPower * 0.9f) return;
                    won = game.FirstClearExpedition(e.id).Won;
                }
                if (!won) return;
                result.ClearedAt[step] = hour;
            }
        }

        int NextStep(GameSession game)
        {
            for (int tier = 1; tier <= 3; tier++)
            {
                if (!game.Bosses.IsDefeated(database.FindBossForTier(tier).id)) return (tier - 1) * 2;
                if (!game.Expeditions.IsCleared(database.Expeditions.First(x => x.tier == tier).id)) return (tier - 1) * 2 + 1;
            }
            return 6;
        }

        static readonly EquipmentSlot[] AllSlots = { EquipmentSlot.Tool, EquipmentSlot.Weapon, EquipmentSlot.Armor };

        /// <summary>The strongest Miners mine and the strongest Adventurers fight.</summary>
        static void PutBestToWork(GameSession game)
        {
            foreach (MinionKind kind in new[] { MinionKind.Miner, MinionKind.Adventure })
            {
                if (kind == MinionKind.Adventure && game.Roster.IsTeamBusy) continue;
                bool miners = kind == MinionKind.Miner;
                List<OwnedMinion> current = miners ? game.Roster.GetMiners() : game.Roster.GetTeam();
                var all = new List<OwnedMinion>(current);
                all.AddRange(game.Roster.GetUnassigned(kind));
                var wanted = all.OrderByDescending(m => Strength(game, m)).Take(miners ? game.Roster.MinerSlots : game.Roster.AdventureSlots).ToList();
                foreach (OwnedMinion m in current.ToList())
                {
                    if (wanted.Contains(m)) continue;
                    if (miners) game.UnassignMiner(m.instanceId); else game.RemoveFromTeam(m.instanceId);
                }
                foreach (OwnedMinion m in wanted)
                {
                    if (game.Roster.IsAssigned(m.instanceId)) continue;
                    if (miners) game.AssignMiner(m.instanceId); else game.AssignToTeam(m.instanceId);
                }
            }
        }

        static float Strength(GameSession game, OwnedMinion m)
        {
            MinionDefinition d = game.Database.FindMinion(m.definitionId);
            return d.Kind == MinionKind.Miner
                ? d.GetMiningPower(m.level)
                : game.Database.Combat.GetPower(MinionStatCalculator.GetHp(game.Database, m), MinionStatCalculator.GetAtk(game.Database, m));
        }

        /// <summary>Buys the affordable Minion level or gear level with the lowest Gold cost, for working Minions only.</summary>
        static bool BuyCheapestUpgrade(GameSession game)
        {
            long best = long.MaxValue;
            Action buy = null;
            var working = new HashSet<int>(game.Roster.GetMiners().Concat(game.Roster.GetTeam()).Select(m => m.instanceId));
            foreach (OwnedMinion m in game.State.minions)
            {
                if (!working.Contains(m.instanceId)) continue;
                int id = m.instanceId;
                MinionUpgradeCheck level = game.Minions.CheckLevelUp(id, game.Wallet);
                if (level.CanDo && Gold(level.Cost) < best) { best = Gold(level.Cost); buy = () => game.LevelUpMinion(id); }
                foreach (EquipmentSlot slot in AllSlots)
                {
                    var gear = game.Smith.CheckUpgrade(id, slot, game.Wallet);
                    if (gear.CanDo && Gold(gear.Cost) < best) { best = Gold(gear.Cost); EquipmentSlot s = slot; buy = () => game.UpgradeGear(id, s); }
                }
            }
            buy?.Invoke();
            return buy != null;
        }

        static long Gold(IReadOnlyList<ResourceAmount> cost) => cost.Where(c => c.id == ResourceId.Gold).Sum(c => c.amount);
    }
}
