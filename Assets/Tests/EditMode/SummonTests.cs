using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Roster;
using Deepshaft.State;
using Deepshaft.Summoning;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>
    /// Summoning (SCRUM-11) with the default <see cref="SummonRules"/>: Basic 75/23/2,
    /// Advanced 0/85/15, 10 shards a summon, duplicates → 5/15/50 Essence. Seeded random.
    /// </summary>
    public class SummonTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        readonly List<MinionDefinition> definitions = new List<MinionDefinition>();
        GameDatabase database;
        GameState state;
        Wallet wallet;
        RosterService roster;
        SummonService summon;

        [SetUp]
        public void SetUp()
        {
            var clock = new ManualClock(Start);
            Add("miner_c", MinionKind.Miner, Rarity.Common, MinerTrait.Gold);
            Add("miner_u", MinionKind.Miner, Rarity.Uncommon, MinerTrait.Speed);
            Add("miner_r", MinionKind.Miner, Rarity.Rare, MinerTrait.Ore);
            Add("adv_c1", MinionKind.Adventure, Rarity.Common);
            Add("adv_c2", MinionKind.Adventure, Rarity.Common);
            Add("adv_u", MinionKind.Adventure, Rarity.Uncommon);
            Add("adv_r", MinionKind.Adventure, Rarity.Rare);
            Add("adv_e", MinionKind.Adventure, Rarity.Epic);
            Add("adv_l", MinionKind.Adventure, Rarity.Legendary);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(definitions);
            state = GameState.CreateNew(clock);
            wallet = new Wallet(state.wallet);
            var mining = new MiningService(state, database, clock);
            roster = new RosterService(state, database, mining, null);
            summon = new SummonService(state, database, roster);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in definitions) UnityEngine.Object.DestroyImmediate(d);
            definitions.Clear();
            UnityEngine.Object.DestroyImmediate(database);
        }

        void Add(string id, MinionKind kind, Rarity rarity, MinerTrait trait = MinerTrait.None)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, kind == MinionKind.Adventure ? MinionRole.Fighter : MinionRole.None, rarity, trait);
            definitions.Add(d);
        }

        // ---------- Paying ----------

        [Test]
        public void Summon_CostsTenShards_NeverGoldOrOres()
        {
            wallet.Add(ResourceId.Gold, 1000);
            wallet.Add(ResourceId.Iron, 1000);

            SummonResult refused = summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, new System.Random(1));
            Assert.AreEqual(SummonStatus.CantAfford, refused.Status);
            Assert.AreEqual(ResourceId.BasicShard, refused.Shortfall[0].id);
            Assert.AreEqual(1000, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(0, state.minions.Count);

            wallet.Add(ResourceId.BasicShard, 15);
            SummonResult done = summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, new System.Random(1));

            Assert.IsTrue(done.Ok);
            Assert.AreEqual(5, wallet.Get(ResourceId.BasicShard));
            Assert.AreEqual(1000, wallet.Get(ResourceId.Gold));
        }

        [Test]
        public void AdvancedSummon_UsesAdvancedShards()
        {
            wallet.Add(ResourceId.BasicShard, 100);
            Assert.AreEqual(SummonStatus.CantAfford, summon.Check(MinionKind.Miner, ShardType.Advanced, wallet).Status);
            wallet.Add(ResourceId.AdvancedShard, 10);
            Assert.IsTrue(summon.TrySummon(MinionKind.Miner, ShardType.Advanced, wallet, new System.Random(1)).Ok);
            Assert.AreEqual(0, wallet.Get(ResourceId.AdvancedShard));
            Assert.AreEqual(100, wallet.Get(ResourceId.BasicShard));
        }

        // ---------- Pools and rates ----------

        [Test]
        public void EachPool_OnlyGivesItsKind()
        {
            var rng = new System.Random(7);
            wallet.Add(ResourceId.BasicShard, 10 * 200);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(MinionKind.Miner, summon.TrySummon(MinionKind.Miner, ShardType.Basic, wallet, rng).Definition.Kind);
                Assert.AreEqual(MinionKind.Adventure, summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, rng).Definition.Kind);
            }
        }

        [Test]
        public void Rates_MatchTheDesign_WhenThePoolHasEveryRarity()
        {
            Dictionary<Rarity, double> basic = summon.GetRates(MinionKind.Adventure, ShardType.Basic);
            Dictionary<Rarity, double> advanced = summon.GetRates(MinionKind.Adventure, ShardType.Advanced);

            // SCRUM-38: Basic 70 / 22 / 5 / 2.5 / 0.5, Advanced 0 / 55 / 30 / 12 / 3.
            Assert.AreEqual(0.70, basic[Rarity.Common], 1e-6);
            Assert.AreEqual(0.22, basic[Rarity.Uncommon], 1e-6);
            Assert.AreEqual(0.05, basic[Rarity.Rare], 1e-6);
            Assert.AreEqual(0.025, basic[Rarity.Epic], 1e-6);
            Assert.AreEqual(0.005, basic[Rarity.Legendary], 1e-6);
            Assert.AreEqual(0.0, advanced[Rarity.Common], 1e-6);
            Assert.AreEqual(0.55, advanced[Rarity.Uncommon], 1e-6);
            Assert.AreEqual(0.30, advanced[Rarity.Rare], 1e-6);
            Assert.AreEqual(0.12, advanced[Rarity.Epic], 1e-6);
            Assert.AreEqual(0.03, advanced[Rarity.Legendary], 1e-6);
        }

        [Test]
        public void SeededSummons_FollowTheRates()
        {
            var rng = new System.Random(12345);
            const int n = 20000;
            var counts = new Dictionary<Rarity, int>
            {
                [Rarity.Common] = 0, [Rarity.Uncommon] = 0, [Rarity.Rare] = 0, [Rarity.Epic] = 0, [Rarity.Legendary] = 0,
            };
            wallet.Add(ResourceId.BasicShard, 10L * n);
            for (int i = 0; i < n; i++) counts[summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, rng).Definition.Rarity]++;

            Assert.AreEqual(0.70, counts[Rarity.Common] / (double)n, 0.015);
            Assert.AreEqual(0.22, counts[Rarity.Uncommon] / (double)n, 0.015);
            Assert.AreEqual(0.05, counts[Rarity.Rare] / (double)n, 0.005);
            Assert.AreEqual(0.025, counts[Rarity.Epic] / (double)n, 0.004);
            Assert.AreEqual(0.005, counts[Rarity.Legendary] / (double)n, 0.002);
        }

        [Test]
        public void AdvancedShard_NeverGivesACommon()
        {
            var rng = new System.Random(3);
            wallet.Add(ResourceId.AdvancedShard, 10 * 500);
            for (int i = 0; i < 500; i++)
            {
                Assert.AreNotEqual(Rarity.Common, summon.TrySummon(MinionKind.Miner, ShardType.Advanced, wallet, rng).Definition.Rarity);
            }
        }

        [Test]
        public void MissingRarity_IsLeftOut_AndTheOthersKeepTheirProportions()
        {
            definitions.RemoveAll(d => d.Id == "adv_r");
            database.EditorSetMinions(definitions);

            Dictionary<Rarity, double> basic = summon.GetRates(MinionKind.Adventure, ShardType.Basic);

            Assert.AreEqual(0.0, basic[Rarity.Rare]);
            Assert.AreEqual(70.0 / 95.0, basic[Rarity.Common], 1e-6);
            Assert.AreEqual(22.0 / 95.0, basic[Rarity.Uncommon], 1e-6);
            Assert.AreEqual(2.5 / 95.0, basic[Rarity.Epic], 1e-6);
        }

        [Test]
        public void EmptyPool_CantBeSummoned()
        {
            definitions.RemoveAll(d => d.Kind == MinionKind.Miner);
            database.EditorSetMinions(definitions);
            wallet.Add(ResourceId.BasicShard, 10);

            Assert.AreEqual(SummonStatus.EmptyPool, summon.TrySummon(MinionKind.Miner, ShardType.Basic, wallet, new System.Random(1)).Status);
            Assert.AreEqual(10, wallet.Get(ResourceId.BasicShard), "Nothing is spent.");
        }

        // ---------- New Minions and duplicates ----------

        [Test]
        public void NewMinion_JoinsTheRoster_AndFillsAFreeSlot()
        {
            wallet.Add(ResourceId.BasicShard, 10);
            SummonResult result = summon.TrySummon(MinionKind.Miner, ShardType.Basic, wallet, new System.Random(1));

            Assert.IsFalse(result.IsDuplicate);
            Assert.IsNotNull(result.Added);
            Assert.AreEqual(result.Definition.Id, result.Added.definitionId);
            Assert.IsTrue(roster.IsAssigned(result.Added.instanceId), "A new Miner starts mining in a free slot.");
            Assert.AreEqual(result.Definition.MinerTrait, database.FindMinion(result.Added.definitionId).MinerTrait,
                "A named Miner's trait comes from its definition.");
        }

        [Test]
        public void Duplicate_BecomesEssenceByRarity_AndAddsNoMinion()
        {
            foreach (MinionDefinition d in definitions) state.AddMinion(d.Id); // own everything
            int owned = state.minions.Count;
            var rng = new System.Random(99);
            wallet.Add(ResourceId.BasicShard, 10 * 300);
            long expected = 0;

            for (int i = 0; i < 300; i++)
            {
                SummonResult r = summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, rng);
                Assert.IsTrue(r.IsDuplicate);
                Assert.AreEqual(database.Summoning.GetEssence(r.Definition.Rarity), r.Essence);
                expected += r.Essence;
            }

            Assert.AreEqual(owned, state.minions.Count);
            Assert.AreEqual(expected, wallet.Get(ResourceId.MinionEssence));
            Assert.AreEqual(5, database.Summoning.GetEssence(Rarity.Common));
            Assert.AreEqual(15, database.Summoning.GetEssence(Rarity.Uncommon));
            Assert.AreEqual(50, database.Summoning.GetEssence(Rarity.Rare));
            Assert.AreEqual(120, database.Summoning.GetEssence(Rarity.Epic));
            Assert.AreEqual(300, database.Summoning.GetEssence(Rarity.Legendary));
        }

        [Test]
        public void SameSeed_GivesTheSameMinion()
        {
            wallet.Add(ResourceId.BasicShard, 20);
            string a = summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, new System.Random(42)).Definition.Id;
            state.minions.Clear();
            string b = summon.TrySummon(MinionKind.Adventure, ShardType.Basic, wallet, new System.Random(42)).Definition.Id;
            Assert.AreEqual(a, b);
        }

        // ---------- Shard sources ----------

        [Test]
        public void BossFirstWins_GiveOneAdvancedSummon()
        {
            foreach (var boss in DefaultAdventureContent.Bosses())
            {
                Assert.IsTrue(boss.firstWinRewards.Exists(r => r.id == ResourceId.AdvancedShard && r.amount == 10), boss.id);
                Assert.IsFalse(boss.repeatWinRewards.Exists(r => r.id == ResourceId.AdvancedShard), boss.id);
            }
        }
    }
}
