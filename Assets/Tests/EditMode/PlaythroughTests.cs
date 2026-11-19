using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Roster;
using Deepshaft.Save;
using Deepshaft.Session;
using Deepshaft.Smith;
using Deepshaft.Summoning;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>
    /// Playtest readiness (SCRUM-8): a simple free player plays the whole prototype through the
    /// GameSession, with the default content and numbers, and must clear all 3 tiers without
    /// paying. It's not optimal; it only proves the loop has no dead end, and logs rough pacing
    /// (hours of game time) for SCRUM-2.
    ///
    /// Each check-in: collect the Mine and the automated Expedition (stop and restart it), learn Smith ranks, do breakthroughs, buy the cheapest
    /// affordable upgrade until nothing is affordable, summon, then fight the next boss or first
    /// clear (the team is free, because the Expedition was stopped to collect), and otherwise keep
    /// the deepest cleared Expedition automated.
    /// </summary>
    public class PlaythroughTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        const int MaxHours = 24 * 60;

        readonly List<MinionDefinition> definitions = new List<MinionDefinition>();
        GameDatabase database;
        ManualClock clock;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            // The prototype's starter roster (Resources/GameDatabase): 2 Common Miners, 5 Common Adventurers.
            Add("miner_c", MinionKind.Miner, MinionRole.None, Rarity.Common, MinerTrait.Gold);
            Add("ore_miner_c", MinionKind.Miner, MinionRole.None, Rarity.Common, MinerTrait.Ore);
            Add("tank_c", MinionKind.Adventure, MinionRole.Tank, Rarity.Common);
            Add("fighter_c", MinionKind.Adventure, MinionRole.Fighter, Rarity.Common);
            Add("support_c", MinionKind.Adventure, MinionRole.Support, Rarity.Common);
            Add("mage_c", MinionKind.Adventure, MinionRole.Mage, Rarity.Common);
            Add("archer_c", MinionKind.Adventure, MinionRole.Archer, Rarity.Common);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(definitions);
            database.EditorSetStarterMinions(new[] { "miner_c", "ore_miner_c", "tank_c", "fighter_c", "support_c", "mage_c", "archer_c" });
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
            definitions.Add(d);
        }

        /// <param name="checkInEveryHours">1 = an engaged player; 8 = about three sessions a day.</param>
        [TestCase(1)]
        [TestCase(8)]
        public void FreePlayer_ClearsAllThreeTiers(int checkInEveryHours)
        {
            var game = new GameSession(database, new MemorySaveStorage(), clock, 2026);
            var milestones = new List<string>();
            int hour = 0;

            for (; hour <= MaxHours; hour += checkInEveryHours)
            {
                CheckIn(game, hour, milestones);
                if (game.Bosses.IsDefeated(DefaultAdventureContent.Tier3Boss) &&
                    game.Expeditions.IsCleared(DefaultAdventureContent.Tier3Expedition)) break;
                clock.Advance(TimeSpan.FromHours(checkInEveryHours));
            }

            TestContext.WriteLine("Check-in every " + checkInEveryHours + " h:");
            foreach (string m in milestones) TestContext.WriteLine(m);
            foreach (var m in game.State.minions)
            {
                TestContext.WriteLine(m.definitionId + " Lv " + m.level + "/" + game.Minions.GetMaxLevel(m) + " bt " + m.breakthroughs +
                    " gear " + string.Join(",", m.equipment.ConvertAll(e => e.slot + ":" + e.level + "b" + e.breakthroughs)) +
                    (game.Roster.IsAssigned(m.instanceId) ? " [working]" : ""));
            }
            foreach (var w in game.State.wallet.balances) TestContext.WriteLine("  " + w);
            TestContext.WriteLine("Team power at the end: " + game.Bosses.GetTeamPower().ToString("0") +
                                  " · Smith rank " + game.Smith.Rank + " · Minions owned " + game.State.minions.Count);
            Assert.IsTrue(game.Bosses.IsDefeated(DefaultAdventureContent.Tier3Boss), "Tier 3 boss not beaten in " + MaxHours + " h.");
            Assert.IsTrue(game.Expeditions.IsCleared(DefaultAdventureContent.Tier3Expedition), "Tier 3 Expedition not cleared in " + MaxHours + " h.");
        }

        void CheckIn(GameSession game, int hour, List<string> milestones)
        {
            game.CollectMine();
            // Expedition loot only reaches the wallet when the run is stopped; restart it below.
            if (game.Expeditions.IsRunning) game.StopExpedition();
            while (game.Smith.CheckRankUp(game.Wallet).CanDo) game.UpgradeSmith();
            DoBreakthroughs(game);
            while (BuyCheapestUpgrade(game)) { }
            while (game.Summoning.Check(MinionKind.Adventure, ShardType.Advanced, game.Wallet).Ok) game.Summon(MinionKind.Adventure, ShardType.Advanced);
            while (game.Summoning.Check(MinionKind.Miner, ShardType.Basic, game.Wallet).Ok) game.Summon(MinionKind.Miner, ShardType.Basic);
            FillTeamWithStrongest(game);

            Fight(game, hour, milestones);

            if (!game.Expeditions.IsRunning)
            {
                ExpeditionDefinition best = null;
                foreach (ExpeditionDefinition e in game.Expeditions.GetUnlocked())
                {
                    if (game.Expeditions.IsCleared(e.id) && (best == null || e.tier > best.tier)) best = e;
                }
                if (best != null) game.StartExpedition(best.id);
            }
        }

        void Fight(GameSession game, int hour, List<string> milestones)
        {
            float power = game.Bosses.GetTeamPower();
            for (int tier = 1; tier <= 3; tier++)
            {
                var boss = database.FindBossForTier(tier);
                if (!game.Bosses.IsDefeated(boss.id))
                {
                    if (power < boss.recommendedPower * 0.9f || game.Tiers.HighestUnlockedTier < tier) return;
                    if (game.FightBoss(boss.id).Won) milestones.Add(Line(hour, "beat " + boss.displayName, power));
                    return;
                }
                foreach (ExpeditionDefinition e in game.Database.Expeditions)
                {
                    if (e.tier != tier || game.Expeditions.IsCleared(e.id)) continue;
                    if (power < e.recommendedPower * 0.9f) return;
                    if (game.FirstClearExpedition(e.id).Won) milestones.Add(Line(hour, "first-cleared " + e.displayName, power));
                    return;
                }
            }
        }

        static string Line(int hour, string what, float power) =>
            "Hour " + hour.ToString().PadLeft(4) + " (day " + (hour / 24 + 1) + "): " + what + " at team power " + power.ToString("0");

        static void DoBreakthroughs(GameSession game)
        {
            foreach (var m in game.State.minions.ToArray())
            {
                if (game.Minions.CheckBreakthrough(m.instanceId, game.Wallet).CanDo) game.BreakthroughMinion(m.instanceId);
                foreach (EquipmentSlot slot in new[] { EquipmentSlot.Tool, EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    if (game.Smith.CheckBreakthrough(m.instanceId, slot, game.Wallet).CanDo) game.BreakthroughGear(m.instanceId, slot);
                }
            }
        }

        /// <summary>Buys the affordable Minion level or gear level with the lowest Gold cost. False if none.</summary>
        static bool BuyCheapestUpgrade(GameSession game)
        {
            long best = long.MaxValue;
            Action buy = null;
            var working = new HashSet<int>();
            foreach (var m in game.Roster.GetMiners()) working.Add(m.instanceId);
            foreach (var m in game.Roster.GetTeam()) working.Add(m.instanceId);

            foreach (var m in game.State.minions)
            {
                if (!working.Contains(m.instanceId)) continue; // only invest in Minions that work
                int id = m.instanceId;
                var level = game.Minions.CheckLevelUp(id, game.Wallet);
                if (level.CanDo && Gold(level.Cost) < best) { best = Gold(level.Cost); buy = () => game.LevelUpMinion(id); }
                foreach (EquipmentSlot slot in new[] { EquipmentSlot.Tool, EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    var gear = game.Smith.CheckUpgrade(id, slot, game.Wallet);
                    if (gear.CanDo && Gold(gear.Cost) < best) { best = Gold(gear.Cost); EquipmentSlot s = slot; buy = () => game.UpgradeGear(id, s); }
                }
            }
            buy?.Invoke();
            return buy != null;
        }

        static long Gold(IReadOnlyList<ResourceAmount> cost)
        {
            foreach (ResourceAmount c in cost) if (c.id == ResourceId.Gold) return c.amount;
            return 0;
        }

        /// <summary>Puts the three strongest Adventurers in the team, keeping a Support if one is owned.</summary>
        static void FillTeamWithStrongest(GameSession game)
        {
            if (game.Roster.IsTeamBusy) return;
            var all = new List<OwnedMinion>(game.Roster.GetTeam());
            all.AddRange(game.Roster.GetUnassigned(MinionKind.Adventure));
            all.Sort((a, b) => Power(game, b).CompareTo(Power(game, a)));
            var wanted = all.GetRange(0, Math.Min(game.Roster.AdventureSlots, all.Count));
            foreach (var m in new List<OwnedMinion>(game.Roster.GetTeam())) if (!wanted.Contains(m)) game.RemoveFromTeam(m.instanceId);
            foreach (var m in wanted) if (!game.Roster.IsAssigned(m.instanceId)) game.AssignToTeam(m.instanceId);
        }

        static float Power(GameSession game, OwnedMinion m) =>
            game.Database.Combat.GetPower(MinionStatCalculator.GetHp(game.Database, m), MinionStatCalculator.GetAtk(game.Database, m));
    }
}
