using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Save;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    public class MiningTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        const double Tolerance = 1e-4; // bonuses are floats in the Inspector

        ManualClock clock;
        MinionDefinition plainMiner;   // 10 MP at level 1, +2 per level, no trait
        MinionDefinition goldMiner;
        MinionDefinition oreMiner;
        MinionDefinition speedMiner;
        MinionDefinition fighter;
        GameDatabase database;
        GameState state;
        Wallet wallet;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            plainMiner = MakeDefinition("plain_miner", MinionKind.Miner, MinerTrait.None);
            goldMiner = MakeDefinition("gold_miner", MinionKind.Miner, MinerTrait.Gold);
            oreMiner = MakeDefinition("ore_miner", MinionKind.Miner, MinerTrait.Ore);
            speedMiner = MakeDefinition("speed_miner", MinionKind.Miner, MinerTrait.Speed);
            fighter = MakeDefinition("fighter", MinionKind.Adventure, MinerTrait.None);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { plainMiner, goldMiner, oreMiner, speedMiner, fighter });
            database.EditorSetMining(MiningRules.CreateDefault());
            state = GameState.CreateNew(clock);
            wallet = new Wallet(state.wallet);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in new[] { plainMiner, goldMiner, oreMiner, speedMiner, fighter })
            {
                UnityEngine.Object.DestroyImmediate(d);
            }
            UnityEngine.Object.DestroyImmediate(database);
        }

        static MinionDefinition MakeDefinition(string id, MinionKind kind, MinerTrait trait)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, MinionRole.None, Rarity.Common, trait);
            return d;
        }

        MiningService NewService(IMinerToolBonus tools = null) => new MiningService(state, database, clock, tools);

        static void AssertRate(List<MinedAmount> rates, ResourceId id, double expected) =>
            Assert.AreEqual(expected, MiningMath.Get(rates, id), Tolerance, id.ToString());

        // ---------- Rates per tier ----------

        [TestCase(1, 100, 40, 0)]
        [TestCase(2, 150, 65, 0)]
        [TestCase(3, 220, 90, 10)]
        public void OneReferenceMiner_MinesTheTierTable(int tier, double gold, double iron, double silver)
        {
            state.highestUnlockedTier = tier;
            state.AddMinion("plain_miner");

            List<MinedAmount> rates = NewService().GetHourlyRates();

            AssertRate(rates, ResourceId.Gold, gold);
            AssertRate(rates, ResourceId.Iron, iron);
            AssertRate(rates, ResourceId.Silver, silver);
            AssertRate(rates, ResourceId.Mithril, 0);
        }

        [Test]
        public void EveryTier_KeepsEarlierResources_AndProducesMore()
        {
            MiningRules rules = MiningRules.CreateDefault();
            var miner = new[] { new MinerInput(10f, MinerTrait.None) };
            for (int tier = 2; tier <= 6; tier++)
            {
                List<MinedAmount> before = MiningMath.GetHourlyRates(rules, tier - 1, miner);
                List<MinedAmount> after = MiningMath.GetHourlyRates(rules, tier, miner);
                foreach (MinedAmount r in before)
                {
                    if (r.amount <= 0) continue;
                    Assert.Greater(MiningMath.Get(after, r.id), r.amount, "Tier " + tier + " " + r.id);
                }
            }
        }

        [Test]
        public void MinersAlwaysMineTheHighestUnlockedTier()
        {
            state.AddMinion("plain_miner");
            MiningService mining = NewService();
            Assert.AreEqual(1, mining.CurrentTier);

            mining.UnlockTier(3);

            Assert.AreEqual(3, mining.CurrentTier);
            AssertRate(mining.GetHourlyRates(), ResourceId.Silver, 10);
        }

        [Test]
        public void TierWithoutRates_UsesDeepestConfiguredTierBelowIt()
        {
            var rules = new MiningRules
            {
                tiers = { new MiningTierRates(1, new ResourceAmount(ResourceId.Gold, 100)),
                          new MiningTierRates(3, new ResourceAmount(ResourceId.Gold, 220)) },
            };
            var miner = new[] { new MinerInput(10f, MinerTrait.None) };

            AssertRate(MiningMath.GetHourlyRates(rules, 2, miner), ResourceId.Gold, 100);
            AssertRate(MiningMath.GetHourlyRates(rules, 9, miner), ResourceId.Gold, 220);
        }

        // ---------- Miners, traits, tools ----------

        [Test]
        public void NoMiners_ProduceNothing()
        {
            state.AddMinion("fighter");
            MiningService mining = NewService();
            clock.Advance(TimeSpan.FromHours(5));

            AssertRate(mining.GetHourlyRates(), ResourceId.Gold, 0);
            Assert.AreEqual(0, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void AdventureMinions_AndUnknownDefinitions_DoNotMine()
        {
            state.AddMinion("plain_miner");
            state.AddMinion("fighter");
            state.AddMinion("removed_from_game");

            MiningService mining = NewService();

            Assert.AreEqual(1, mining.GetWorkingMiners().Count);
            AssertRate(mining.GetHourlyRates(), ResourceId.Gold, 100);
        }

        [Test]
        public void OutputScalesWithNumberOfMiners_AndMiningPower()
        {
            state.AddMinion("plain_miner");
            state.AddMinion("plain_miner").level = 6; // 10 + 5 × 2 = 20 MP = double output

            AssertRate(NewService().GetHourlyRates(), ResourceId.Gold, 100 + 200);
            AssertRate(NewService().GetHourlyRates(), ResourceId.Iron, 40 + 80);
        }

        [Test]
        public void GoldTrait_BoostsGoldOnly()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("gold_miner");

            List<MinedAmount> rates = NewService().GetHourlyRates();

            AssertRate(rates, ResourceId.Gold, 220 * 1.25);
            AssertRate(rates, ResourceId.Iron, 90);
            AssertRate(rates, ResourceId.Silver, 10);
        }

        [Test]
        public void OreTrait_BoostsOresButNotGold()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("ore_miner");

            List<MinedAmount> rates = NewService().GetHourlyRates();

            AssertRate(rates, ResourceId.Gold, 220);
            AssertRate(rates, ResourceId.Iron, 90 * 1.25);
            AssertRate(rates, ResourceId.Silver, 10 * 1.25);
        }

        [Test]
        public void SpeedTrait_BoostsEverything()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("speed_miner");

            List<MinedAmount> rates = NewService().GetHourlyRates();

            AssertRate(rates, ResourceId.Gold, 220 * 1.15);
            AssertRate(rates, ResourceId.Iron, 90 * 1.15);
            AssertRate(rates, ResourceId.Silver, 10 * 1.15);
        }

        sealed class FixedTools : IMinerToolBonus
        {
            public float bonus;
            public float GetToolBonus(OwnedMinion miner) => bonus;
        }

        [Test]
        public void Tools_IncreaseOutput()
        {
            state.AddMinion("gold_miner");
            var tools = new FixedTools { bonus = 0.2f };

            List<MinedAmount> rates = NewService(tools).GetHourlyRates();

            AssertRate(rates, ResourceId.Gold, 100 * 1.2 * 1.25);
            AssertRate(rates, ResourceId.Iron, 40 * 1.2);
        }

        // ---------- Running over time ----------

        [Test]
        public void Output_AddsUpOverTime_AndCollectMovesItToTheWallet()
        {
            state.AddMinion("plain_miner");
            MiningService mining = NewService();

            clock.Advance(TimeSpan.FromMinutes(90));
            Assert.AreEqual(150, mining.GetPending(ResourceId.Gold), Tolerance);

            List<ResourceAmount> collected = mining.Collect(wallet);

            Assert.AreEqual(150, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(60, wallet.Get(ResourceId.Iron));
            Assert.AreEqual(2, collected.Count);
            Assert.AreEqual(0, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void VeryLongAbsence_HasNoCap()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("plain_miner");
            state.AddMinion("plain_miner");
            MiningService mining = NewService();

            TimeSpan tenYears = TimeSpan.FromDays(3652.5);
            clock.Advance(tenYears);
            double hours = tenYears.TotalHours; // 87,660

            Assert.AreEqual(2 * 220 * hours, mining.GetPending(ResourceId.Gold), 1e-3);
            mining.Collect(wallet);

            Assert.AreEqual((long)(2 * 220 * hours), wallet.Get(ResourceId.Gold));
            Assert.AreEqual((long)(2 * 90 * hours), wallet.Get(ResourceId.Iron));
            Assert.AreEqual((long)(2 * 10 * hours), wallet.Get(ResourceId.Silver));
        }

        [Test]
        public void LongAbsence_GivesTheSameAsCollectingOften()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("ore_miner");
            MiningService mining = NewService();
            var otherState = GameState.CreateNew(clock);
            otherState.highestUnlockedTier = 3;
            otherState.AddMinion("ore_miner");
            var otherWallet = new Wallet(otherState.wallet);
            var otherMining = new MiningService(otherState, database, clock);

            // One collects every 7 minutes for two days; the other only once at the end.
            for (int i = 0; i < 2 * 24 * 60 / 7; i++)
            {
                clock.Advance(TimeSpan.FromMinutes(7));
                otherMining.Collect(otherWallet);
            }
            mining.Collect(wallet);
            otherMining.Collect(otherWallet);

            foreach (ResourceId id in new[] { ResourceId.Gold, ResourceId.Iron, ResourceId.Silver })
            {
                Assert.AreEqual(wallet.Get(id), otherWallet.Get(id), 1, id.ToString());
            }
        }

        [Test]
        public void Fractions_AreKeptBetweenCollects()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("plain_miner"); // 10 Silver per hour = 0.5 per 3 minutes
            MiningService mining = NewService();

            for (int i = 0; i < 20; i++)
            {
                clock.Advance(TimeSpan.FromMinutes(3));
                mining.Collect(wallet);
            }

            Assert.AreEqual(10, wallet.Get(ResourceId.Silver));
        }

        [Test]
        public void KeepsRunningWhileTheAppIsClosed()
        {
            state.AddMinion("plain_miner");
            NewService().Bank();
            var storage = new MemorySaveStorage();
            new SaveService(storage, clock).Save(state);

            // App closed for 8 hours, then opened again.
            clock.Advance(TimeSpan.FromHours(8));
            GameState loaded = new SaveService(storage, clock).Load().State;
            var mining = new MiningService(loaded, database, clock);

            Assert.AreEqual(800, mining.GetPending(ResourceId.Gold), Tolerance);
            Assert.AreEqual(320, mining.GetPending(ResourceId.Iron), Tolerance);
        }

        [Test]
        public void PendingFractions_SurviveSaveAndLoad()
        {
            state.highestUnlockedTier = 3;
            state.AddMinion("plain_miner");
            MiningService mining = NewService();
            clock.Advance(TimeSpan.FromMinutes(9)); // 1.5 Silver
            mining.Collect(wallet);
            var storage = new MemorySaveStorage();
            new SaveService(storage, clock).Save(state);

            GameState loaded = new SaveService(storage, clock).Load().State;

            Assert.AreEqual(0.5, new MiningService(loaded, database, clock).GetPending(ResourceId.Silver), Tolerance);
        }

        [Test]
        public void SaveFromBeforeMining_StartsCountingNow()
        {
            state.mining = null; // as if loaded from an older save
            state.AddMinion("plain_miner");

            MiningService mining = NewService();
            Assert.AreEqual(0, mining.GetPending(ResourceId.Gold), Tolerance);

            clock.Advance(TimeSpan.FromHours(1));
            Assert.AreEqual(100, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void ClockGoingBackwards_EarnsNothing_AndIsNotPaidTwice()
        {
            state.AddMinion("plain_miner");
            MiningService mining = NewService();
            clock.Advance(TimeSpan.FromHours(2));
            mining.Collect(wallet); // 200

            clock.Set(Start); // phone clock set back 2 hours
            Assert.AreEqual(0, mining.GetPending(ResourceId.Gold), Tolerance);
            mining.Collect(wallet);

            clock.Set(Start.AddHours(3)); // only 1 hour past the last collect
            mining.Collect(wallet);

            Assert.AreEqual(300, wallet.Get(ResourceId.Gold));
        }

        // ---------- Rate changes bank first ----------

        [Test]
        public void NewMiner_BanksWhatWasEarnedAtTheOldRate()
        {
            MiningService mining = NewService();
            mining.AddMinion("plain_miner");
            clock.Advance(TimeSpan.FromHours(2));

            mining.AddMinion("plain_miner");
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(2 * 100 + 1 * 200, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void AddingAnAdventureMinion_DoesNotChangeMining()
        {
            MiningService mining = NewService();
            mining.AddMinion("plain_miner");
            clock.Advance(TimeSpan.FromHours(1));
            mining.AddMinion("fighter");
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(200, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void NewTier_BanksWhatWasEarnedAtTheOldTier()
        {
            MiningService mining = NewService();
            mining.AddMinion("plain_miner");
            clock.Advance(TimeSpan.FromHours(1));

            mining.UnlockTier(3);
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(100 + 220, mining.GetPending(ResourceId.Gold), Tolerance);
            Assert.AreEqual(40 + 90, mining.GetPending(ResourceId.Iron), Tolerance);
            Assert.AreEqual(10, mining.GetPending(ResourceId.Silver), Tolerance);
        }

        [Test]
        public void UnlockTier_NeverLowersTheTier()
        {
            state.highestUnlockedTier = 3;
            MiningService mining = NewService();
            mining.UnlockTier(2);
            Assert.AreEqual(3, mining.CurrentTier);
        }

        [Test]
        public void Upgrade_ThroughChangeRate_BanksAtTheOldRate()
        {
            MiningService mining = NewService();
            OwnedMinion miner = mining.AddMinion("plain_miner");
            var tools = new FixedTools();
            mining = NewService(tools);
            clock.Advance(TimeSpan.FromHours(1));

            mining.ChangeRate(() => miner.level = 6);           // 2× power
            clock.Advance(TimeSpan.FromHours(1));
            mining.ChangeRate(() => tools.bonus = 0.5f);        // Smith tool upgrade
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(100 + 200 + 300, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void Collect_RaisesEvent_OnlyWhenSomethingWasCollected()
        {
            state.AddMinion("plain_miner");
            MiningService mining = NewService();
            int raised = 0;
            mining.Collected += _ => raised++;

            mining.Collect(wallet);
            Assert.AreEqual(0, raised);

            clock.Advance(TimeSpan.FromHours(1));
            mining.Collect(wallet);
            Assert.AreEqual(1, raised);
        }
    }
}
