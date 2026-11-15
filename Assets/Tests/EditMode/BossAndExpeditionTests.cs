using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Deepshaft.Bosses;
using Deepshaft.Combat;
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
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deepshaft.Tests
{
    /// <summary>
    /// Mine Tier Bosses, Expedition first clears, automated Expeditions, the busy-team rule and
    /// tier progression (SCRUM-7). Uses test content whose fights can't go the other way, so
    /// these tests check the rules, not the balance (that's in <see cref="CombatTests"/>).
    /// </summary>
    public class BossAndExpeditionTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        const double Tolerance = 1e-4;

        // Content
        const string EasyBoss = "easy_boss";      // tier 1, can't be lost
        const string HardBoss = "hard_boss";      // tier 2, can't be won
        const string EasyExpedition = "easy_exp"; // unlocked by EasyBoss, can't be lost, 2 Spore Silk/h
        const string HardExpedition = "hard_exp"; // unlocked by EasyBoss, can't be won
        const string OtherExpedition = "other_exp"; // unlocked by EasyBoss, can't be lost, 3 Ember Core/h
        const string LateExpedition = "late_exp"; // unlocked by HardBoss

        ManualClock clock;
        MinionDefinition miner;
        MinionDefinition tank;
        MinionDefinition fighter;
        MinionDefinition support;
        GameDatabase database;
        GameState state;
        Wallet wallet;
        System.Random rng;

        MiningService mining;
        TierProgressionService tiers;
        ExpeditionService expeditions;
        RosterService roster;
        BossService bosses;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            miner = MakeDefinition("miner", MinionKind.Miner, MinionRole.None);
            tank = MakeDefinition("tank", MinionKind.Adventure, MinionRole.Tank);
            fighter = MakeDefinition("fighter", MinionKind.Adventure, MinionRole.Fighter);
            support = MakeDefinition("support", MinionKind.Adventure, MinionRole.Support);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { miner, tank, fighter, support });
            database.EditorSetMining(MiningRules.CreateDefault());
            database.EditorSetRoster(new RosterRules { minerSlots = 3, adventureSlots = 3 });
            database.EditorSetCombat(CombatRules.CreateDefault());
            database.EditorSetBosses(new[]
            {
                Boss(EasyBoss, 1, Weak(), First(R(ResourceId.ForemanSigil, 1), R(ResourceId.BasicShard, 20)),
                    First(R(ResourceId.ForemanSigil, 1))),
                Boss(HardBoss, 2, Unbeatable(), First(R(ResourceId.GolemHeart, 1)), First(R(ResourceId.GolemHeart, 1)), 500),
            });
            database.EditorSetExpeditions(new[]
            {
                Expedition(EasyExpedition, EasyBoss, Weak(), First(R(ResourceId.Gold, 500)), First(R(ResourceId.SporeSilk, 2))),
                Expedition(HardExpedition, EasyBoss, Unbeatable(), First(R(ResourceId.Gold, 999)), First(R(ResourceId.SporeSilk, 9))),
                Expedition(OtherExpedition, EasyBoss, Weak(), First(), First(R(ResourceId.EmberCore, 3))),
                Expedition(LateExpedition, HardBoss, Weak(), First(), First(R(ResourceId.PrismDust, 1))),
            });
            database.EditorSetTierRequirements(new[]
            {
                new TierRequirement { tier = 2, requiredBosses = new List<string> { EasyBoss } },
                new TierRequirement { tier = 3, requiredBosses = new List<string> { HardBoss } },
            });
            state = GameState.CreateNew(clock);
            rng = new System.Random(1);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in new[] { miner, tank, fighter, support }) UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(database);
        }

        // ---------- Helpers ----------

        static MinionDefinition MakeDefinition(string id, MinionKind kind, MinionRole role)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, role, Rarity.Common);
            return d;
        }

        static ResourceAmount R(ResourceId id, long amount) => new ResourceAmount(id, amount);
        static List<ResourceAmount> First(params ResourceAmount[] items) => new List<ResourceAmount>(items);
        static EnemyDefinition Weak() => new EnemyDefinition("Dummy", 1, 0, 2f);
        static EnemyDefinition Unbeatable() => new EnemyDefinition("Wall", 1_000_000, 1_000_000, 2f);

        static BossDefinition Boss(string id, int tier, EnemyDefinition enemy, List<ResourceAmount> firstWin,
            List<ResourceAmount> repeatWin, float recommended = 50) => new BossDefinition
        {
            id = id,
            displayName = id,
            tier = tier,
            recommendedPower = recommended,
            enemy = enemy,
            firstWinRewards = firstWin,
            repeatWinRewards = repeatWin,
        };

        static ExpeditionDefinition Expedition(string id, string boss, EnemyDefinition enemy,
            List<ResourceAmount> firstClear, List<ResourceAmount> perHour) => new ExpeditionDefinition
        {
            id = id,
            displayName = id,
            unlockedByBoss = boss,
            recommendedPower = 50,
            enemies = new List<EnemyDefinition> { enemy },
            firstClearRewards = firstClear,
            automatedPerHour = perHour,
        };

        /// <summary>Wires the services the way the game does (GameSession, SCRUM-17).</summary>
        void StartServices()
        {
            mining = new MiningService(state, database, clock);
            tiers = new TierProgressionService(state, database, mining);
            expeditions = new ExpeditionService(state, database, clock, tiers);
            roster = new RosterService(state, database, mining, expeditions);
            bosses = new BossService(state, database, expeditions, tiers);
            wallet = new Wallet(state.wallet);
        }

        void StartWithTeam()
        {
            StartServices();
            roster.AddMinion("tank");
            roster.AddMinion("fighter");
            roster.AddMinion("support");
        }

        void BeatEasyBoss() => Assert.IsTrue(bosses.Fight(EasyBoss, wallet, rng).Won);

        void ClearAndStart(string id)
        {
            Assert.IsTrue(expeditions.TryFirstClear(id, wallet, rng).Won);
            Assert.AreEqual(ExpeditionCheck.Ok, expeditions.Start(id));
        }

        GameState SaveAndLoad()
        {
            var storage = new MemorySaveStorage();
            var saves = new SaveService(storage, clock);
            Assert.IsTrue(saves.Save(state));
            LoadResult loaded = saves.Load();
            Assert.AreEqual(LoadStatus.Loaded, loaded.Status);
            return loaded.State;
        }

        // ---------- Mine Tier Bosses ----------

        [Test]
        public void ABossWin_GivesItsMaterial_UnlocksItsExpeditions_AndTheNextTier()
        {
            StartWithTeam();
            Assert.AreEqual(ExpeditionStatus.Locked, expeditions.GetStatus(EasyExpedition));

            BossFightResult r = bosses.Fight(EasyBoss, wallet, rng);

            Assert.AreEqual(BossCheck.Ok, r.Check);
            Assert.IsTrue(r.Won);
            Assert.IsTrue(r.FirstWin);
            Assert.AreEqual(1, wallet.Get(ResourceId.ForemanSigil));
            Assert.AreEqual(20, wallet.Get(ResourceId.BasicShard));
            CollectionAssert.AreEquivalent(new[] { EasyExpedition, HardExpedition, OtherExpedition }, r.UnlockedExpeditions);
            CollectionAssert.AreEqual(new[] { 2 }, r.UnlockedTiers);
            Assert.AreEqual(2, state.highestUnlockedTier);
            Assert.IsTrue(bosses.IsDefeated(EasyBoss));
            Assert.AreEqual(ExpeditionStatus.FirstClearAvailable, expeditions.GetStatus(EasyExpedition));
            Assert.AreEqual(ExpeditionStatus.Locked, expeditions.GetStatus(LateExpedition));
        }

        [Test]
        public void EveryLaterWin_StillGuaranteesTheMaterial_ButNotTheFirstWinBonus()
        {
            StartWithTeam();
            BeatEasyBoss();

            BossFightResult again = bosses.Fight(EasyBoss, wallet, rng);

            Assert.IsTrue(again.Won);
            Assert.IsFalse(again.FirstWin);
            Assert.AreEqual(2, wallet.Get(ResourceId.ForemanSigil));
            Assert.AreEqual(20, wallet.Get(ResourceId.BasicShard));
            Assert.AreEqual(0, again.UnlockedExpeditions.Count);
            Assert.AreEqual(0, again.UnlockedTiers.Count);
        }

        [Test]
        public void BossAttempts_AreFreeAndUnlimited()
        {
            StartWithTeam();
            BeatEasyBoss(); // unlocks tier 2
            wallet.Add(ResourceId.Gold, 100);
            List<ResourceAmount> before = new List<ResourceAmount>(state.wallet.balances);

            for (int i = 0; i < 25; i++)
            {
                BossFightResult r = bosses.Fight(HardBoss, wallet, rng);
                Assert.AreEqual(BossCheck.Ok, r.Check, "attempt " + i);
                Assert.IsFalse(r.Won);
            }

            CollectionAssert.AreEquivalent(before, state.wallet.balances);
        }

        [Test]
        public void ABossLoss_ExplainsWhy_AndChangesNothing()
        {
            StartWithTeam();
            BeatEasyBoss();

            BossFightResult r = bosses.Fight(HardBoss, wallet, rng);

            Assert.IsFalse(r.Won);
            Assert.AreEqual(0, r.Rewards.Count);
            Assert.AreEqual(0, wallet.Get(ResourceId.GolemHeart));
            Assert.IsFalse(bosses.IsDefeated(HardBoss));
            Assert.AreEqual(2, state.highestUnlockedTier);
            Assert.AreEqual(ExpeditionStatus.Locked, expeditions.GetStatus(LateExpedition));
            string why = r.DescribeLoss();
            StringAssert.Contains("Boss HP left: 100%", why);
            StringAssert.Contains("below the recommended 500", why);
        }

        [Test]
        public void ABossOfALockedTier_CantBeFought()
        {
            StartWithTeam();

            BossFightResult r = bosses.Fight(HardBoss, wallet, rng);

            Assert.AreEqual(BossCheck.TierLocked, r.Check);
            Assert.IsNull(r.Battle);
        }

        [Test]
        public void ABossFight_NeedsATeam_AndAKnownBoss()
        {
            StartServices();
            roster.AddMinion("miner"); // Miners never fight

            Assert.AreEqual(BossCheck.NoTeam, bosses.CanFight(EasyBoss));
            Assert.AreEqual(BossCheck.Unknown, bosses.CanFight("nope"));
            Assert.AreEqual(BossCheck.NoTeam, bosses.Fight(EasyBoss, wallet, rng).Check);
        }

        [Test]
        public void CurrentBoss_IsTheBossOfTheHighestUnlockedTier()
        {
            StartWithTeam();
            Assert.AreEqual(EasyBoss, bosses.CurrentBoss.id);
            BeatEasyBoss();
            Assert.AreEqual(HardBoss, bosses.CurrentBoss.id);
        }

        [Test]
        public void TeamPower_IsShownForTheRecommendedLine()
        {
            StartWithTeam(); // three level-1 Minions: 20 power each, + 10% for the Support
            Assert.AreEqual(66f, bosses.GetTeamPower(), 1e-3);
            Assert.AreEqual(66f, expeditions.GetTeamPower(), 1e-3);
        }

        // ---------- Expedition first clear ----------

        [Test]
        public void AnExpedition_IsLockedUntilItsBossIsBeaten()
        {
            StartWithTeam();

            Assert.AreEqual(ExpeditionCheck.Locked, expeditions.CanFirstClear(EasyExpedition));
            Assert.AreEqual(ExpeditionCheck.Locked, expeditions.CanStart(EasyExpedition));
            Assert.AreEqual(ExpeditionCheck.Unknown, expeditions.CanFirstClear("nope"));
            Assert.AreEqual(0, expeditions.GetUnlocked().Count);
        }

        [Test]
        public void AFirstClearWin_GivesTheBonusOnce_AndUnlocksAutomationForGood()
        {
            StartWithTeam();
            BeatEasyBoss();

            FirstClearResult r = expeditions.TryFirstClear(EasyExpedition, wallet, rng);

            Assert.AreEqual(ExpeditionCheck.Ok, r.Check);
            Assert.IsTrue(r.Won);
            Assert.AreEqual(500, wallet.Get(ResourceId.Gold));
            Assert.IsTrue(expeditions.IsCleared(EasyExpedition));
            Assert.AreEqual(ExpeditionStatus.ReadyToAutomate, expeditions.GetStatus(EasyExpedition));
            Assert.AreEqual(ExpeditionCheck.Ok, expeditions.CanStart(EasyExpedition));

            FirstClearResult again = expeditions.TryFirstClear(EasyExpedition, wallet, rng);
            Assert.AreEqual(ExpeditionCheck.AlreadyCleared, again.Check);
            Assert.IsNull(again.Battle);
            Assert.AreEqual(500, wallet.Get(ResourceId.Gold));
        }

        [Test]
        public void AFirstClear_CanFail_AndThenAutomationStaysLocked()
        {
            StartWithTeam();
            BeatEasyBoss();

            FirstClearResult r = expeditions.TryFirstClear(HardExpedition, wallet, rng);

            Assert.AreEqual(ExpeditionCheck.Ok, r.Check);
            Assert.IsFalse(r.Won);
            Assert.AreEqual(0, r.Rewards.Count);
            Assert.AreEqual(0, wallet.Get(ResourceId.Gold));
            Assert.IsFalse(expeditions.IsCleared(HardExpedition));
            Assert.AreEqual(ExpeditionCheck.NotCleared, expeditions.CanStart(HardExpedition));
            Assert.AreEqual(ExpeditionCheck.Ok, expeditions.CanFirstClear(HardExpedition), "try again any time");
            StringAssert.Contains("HP left", r.Battle.DescribeLoss());
        }

        [Test]
        public void FirstClearsAndBossWins_SurviveSaveAndLoad()
        {
            StartWithTeam();
            BeatEasyBoss();
            expeditions.TryFirstClear(EasyExpedition, wallet, rng);

            state = SaveAndLoad();
            StartServices();

            Assert.IsTrue(bosses.IsDefeated(EasyBoss));
            Assert.IsTrue(expeditions.IsCleared(EasyExpedition));
            Assert.AreEqual(ExpeditionCheck.Ok, expeditions.CanStart(EasyExpedition));
            Assert.AreEqual(2, state.highestUnlockedTier);
        }

        // ---------- Automated Expedition ----------

        [Test]
        public void AnAutomatedExpedition_CantFail_AndEarnsRateTimesTime_WithNoCap()
        {
            StartWithTeam();
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);

            clock.Advance(TimeSpan.FromHours(10));
            Assert.AreEqual(20, expeditions.GetPending(ResourceId.SporeSilk), Tolerance);

            clock.Advance(TimeSpan.FromHours(990)); // no time cap, no diminishing returns
            Assert.AreEqual(2000, expeditions.GetPending(ResourceId.SporeSilk), Tolerance);

            ExpeditionCollectResult r = expeditions.StopAndCollect(wallet);
            Assert.AreEqual(ExpeditionCheck.Ok, r.Check);
            Assert.AreEqual(EasyExpedition, r.ExpeditionId);
            Assert.AreEqual(1000, r.HoursRun, Tolerance);
            Assert.AreEqual(2000, wallet.Get(ResourceId.SporeSilk));
            CollectionAssert.AreEqual(new[] { R(ResourceId.SporeSilk, 2000) }, r.Collected);
            Assert.IsFalse(expeditions.IsRunning);
        }

        [Test]
        public void AnAutomatedExpedition_KeepsRunningWhileTheAppIsClosed()
        {
            StartWithTeam();
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);
            clock.Advance(TimeSpan.FromHours(1));

            state = SaveAndLoad();          // app closed...
            clock.Advance(TimeSpan.FromHours(5)); // ...for 5 hours
            StartServices();

            Assert.IsTrue(expeditions.IsRunning);
            Assert.AreEqual(12, expeditions.GetPending(ResourceId.SporeSilk), Tolerance);
            expeditions.StopAndCollect(wallet);
            Assert.AreEqual(12, wallet.Get(ResourceId.SporeSilk));
        }

        [Test]
        public void Fractions_CarryOverToTheNextRun()
        {
            StartWithTeam();
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);

            clock.Advance(TimeSpan.FromMinutes(75)); // 2.5 Spore Silk
            expeditions.StopAndCollect(wallet);
            Assert.AreEqual(2, wallet.Get(ResourceId.SporeSilk));
            Assert.AreEqual(0.5, expeditions.GetPending(ResourceId.SporeSilk), Tolerance);

            expeditions.Start(EasyExpedition);
            clock.Advance(TimeSpan.FromMinutes(15)); // + 0.5
            expeditions.StopAndCollect(wallet);
            Assert.AreEqual(3, wallet.Get(ResourceId.SporeSilk));
        }

        [Test]
        public void OnlyOneExpedition_RunsAtATime()
        {
            StartWithTeam();
            BeatEasyBoss();
            Assert.IsTrue(expeditions.TryFirstClear(OtherExpedition, wallet, rng).Won);
            ClearAndStart(EasyExpedition);

            Assert.AreEqual(ExpeditionCheck.AnotherRunning, expeditions.Start(OtherExpedition));
            Assert.AreEqual(ExpeditionCheck.AlreadyRunning, expeditions.Start(EasyExpedition));
            Assert.AreEqual(EasyExpedition, expeditions.Active.id);
            Assert.AreEqual(ExpeditionStatus.Running, expeditions.GetStatus(EasyExpedition));

            expeditions.StopAndCollect(wallet);
            Assert.AreEqual(ExpeditionCheck.Ok, expeditions.Start(OtherExpedition));
        }

        [Test]
        public void StoppingWhenNothingRuns_DoesNothing()
        {
            StartWithTeam();

            ExpeditionCollectResult r = expeditions.StopAndCollect(wallet);

            Assert.AreEqual(ExpeditionCheck.NotRunning, r.Check);
            Assert.AreEqual(0, r.Collected.Count);
        }

        [Test]
        public void AClockSetBackwards_EarnsNothing_UntilItCatchesUp()
        {
            StartWithTeam();
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);

            clock.Set(Start - TimeSpan.FromHours(3));
            Assert.AreEqual(0, expeditions.GetPending(ResourceId.SporeSilk), Tolerance);
            Assert.AreEqual(0, expeditions.HoursRunning, Tolerance);

            clock.Set(Start + TimeSpan.FromHours(1));
            Assert.AreEqual(2, expeditions.GetPending(ResourceId.SporeSilk), Tolerance);
        }

        [Test]
        public void StartingAnExpedition_NeedsATeam()
        {
            StartServices();
            roster.AddMinion("tank");
            BeatEasyBoss();
            Assert.IsTrue(expeditions.TryFirstClear(EasyExpedition, wallet, rng).Won);
            roster.RemoveFromTeam(state.minions[0].instanceId);

            Assert.AreEqual(ExpeditionCheck.NoTeam, expeditions.Start(EasyExpedition));
        }

        [Test]
        public void ARunningExpeditionWhoseContentWasRemoved_IsStopped()
        {
            state.expeditions.activeId = "removed_expedition";
            LogAssert.Expect(LogType.Warning, new Regex("removed_expedition"));

            StartServices();

            Assert.IsFalse(expeditions.IsRunning);
            Assert.IsFalse(roster.IsTeamBusy);
        }

        // ---------- The busy team ----------

        [Test]
        public void WhileAnExpeditionRuns_TheTeamIsBusy_AndCantBeChanged()
        {
            StartWithTeam();
            OwnedMinion spare = state.AddMinion("fighter");
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);

            Assert.IsTrue(roster.IsTeamBusy);
            Assert.AreEqual(AssignResult.TeamBusy, roster.RemoveFromTeam(state.minions[0].instanceId));
            Assert.AreEqual(AssignResult.TeamBusy, roster.AssignToTeam(spare.instanceId));
        }

        [Test]
        public void WhileAnExpeditionRuns_NoBossFightOrFirstClear()
        {
            StartWithTeam();
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);

            Assert.AreEqual(BossCheck.TeamBusy, bosses.CanFight(EasyBoss));
            BossFightResult fight = bosses.Fight(EasyBoss, wallet, rng);
            Assert.AreEqual(BossCheck.TeamBusy, fight.Check);
            Assert.IsNull(fight.Battle);
            Assert.AreEqual(1, wallet.Get(ResourceId.ForemanSigil), "no reward without a fight");

            Assert.AreEqual(ExpeditionCheck.TeamBusy, expeditions.CanFirstClear(HardExpedition));
            Assert.AreEqual(ExpeditionCheck.TeamBusy, expeditions.TryFirstClear(OtherExpedition, wallet, rng).Check);
        }

        [Test]
        public void StoppingTheExpedition_FreesTheTeam()
        {
            StartWithTeam();
            BeatEasyBoss();
            ClearAndStart(EasyExpedition);

            expeditions.StopAndCollect(wallet);

            Assert.IsFalse(roster.IsTeamBusy);
            Assert.AreEqual(BossCheck.Ok, bosses.CanFight(EasyBoss));
            Assert.AreEqual(ExpeditionCheck.Ok, expeditions.CanFirstClear(HardExpedition));
        }

        [Test]
        public void MinersKeepMining_WhileAnExpeditionRuns()
        {
            StartWithTeam();
            roster.AddMinion("miner");
            BeatEasyBoss(); // tier 2: 150 Gold/h per reference Miner
            mining.Collect(wallet);
            long goldBefore = wallet.Get(ResourceId.Gold);
            ClearAndStart(EasyExpedition); // +500 Gold first-clear bonus

            clock.Advance(TimeSpan.FromHours(2));

            Assert.AreEqual(300, mining.GetPending(ResourceId.Gold), Tolerance);
            Assert.AreEqual(goldBefore + 500, wallet.Get(ResourceId.Gold));
        }

        // ---------- Tier progression ----------

        [Test]
        public void TheNextTier_WaitsForItsBoss()
        {
            StartWithTeam();

            TierUnlockCheck check = tiers.CheckNext(wallet);

            Assert.AreEqual(2, check.Tier);
            Assert.AreEqual(TierUnlockStatus.BossNotDefeated, check.Status);
            CollectionAssert.AreEqual(new[] { EasyBoss }, check.MissingBosses);
            Assert.AreEqual(TierUnlockStatus.BossNotDefeated, tiers.TryUnlockNext(wallet).Status);
            Assert.AreEqual(1, state.highestUnlockedTier);
        }

        [Test]
        public void UnlockingATier_BanksMiningAtTheOldRateFirst()
        {
            StartWithTeam();
            roster.AddMinion("miner");
            clock.Advance(TimeSpan.FromHours(1)); // tier 1: 100 Gold

            BeatEasyBoss();
            clock.Advance(TimeSpan.FromHours(1)); // tier 2: 150 Gold

            Assert.AreEqual(250, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void ATierWithACost_WaitsForThePlayerToPay()
        {
            database.EditorSetTierRequirements(new[]
            {
                new TierRequirement
                {
                    tier = 2,
                    requiredBosses = new List<string> { EasyBoss },
                    cost = new List<ResourceAmount> { R(ResourceId.Iron, 1000) },
                },
            });
            StartWithTeam();
            int unlockedEvents = 0;
            tiers.TierUnlocked += _ => unlockedEvents++;

            BossFightResult win = bosses.Fight(EasyBoss, wallet, rng);
            Assert.AreEqual(0, win.UnlockedTiers.Count);
            Assert.AreEqual(1, state.highestUnlockedTier);

            TierUnlockCheck poor = tiers.TryUnlockNext(wallet);
            Assert.AreEqual(TierUnlockStatus.CantAfford, poor.Status);
            CollectionAssert.AreEqual(new[] { R(ResourceId.Iron, 1000) }, poor.Shortfall);

            wallet.Add(ResourceId.Iron, 1200);
            TierUnlockCheck paid = tiers.TryUnlockNext(wallet);
            Assert.AreEqual(TierUnlockStatus.Ok, paid.Status);
            Assert.AreEqual(2, state.highestUnlockedTier);
            Assert.AreEqual(200, wallet.Get(ResourceId.Iron));
            Assert.AreEqual(1, unlockedEvents);
        }

        [Test]
        public void ATierCanRequireAFirstClear()
        {
            database.EditorSetTierRequirements(new[]
            {
                new TierRequirement
                {
                    tier = 2,
                    requiredBosses = new List<string> { EasyBoss },
                    requiredFirstClears = new List<string> { EasyExpedition },
                },
            });
            StartWithTeam();

            BeatEasyBoss();
            Assert.AreEqual(1, state.highestUnlockedTier);
            TierUnlockCheck check = tiers.CheckNext();
            Assert.AreEqual(TierUnlockStatus.ExpeditionNotCleared, check.Status);
            CollectionAssert.AreEqual(new[] { EasyExpedition }, check.MissingFirstClears);

            FirstClearResult clear = expeditions.TryFirstClear(EasyExpedition, wallet, rng);
            CollectionAssert.AreEqual(new[] { 2 }, clear.UnlockedTiers);
            Assert.AreEqual(2, state.highestUnlockedTier);
        }

        [Test]
        public void ATierWithNoRequirementRow_IsTheEndOfTheContent()
        {
            StartWithTeam();
            state.highestUnlockedTier = 3;

            Assert.AreEqual(TierUnlockStatus.MaxTierReached, tiers.CheckNext().Status);
            Assert.AreEqual(0, tiers.TryAutoUnlock().Count);
            Assert.AreEqual(3, state.highestUnlockedTier);
        }

        // ---------- Default content ----------

        [Test]
        public void DefaultContent_HasOneBossAndOneExpeditionPerPrototypeTier()
        {
            var defaults = ScriptableObject.CreateInstance<GameDatabase>();
            try
            {
                var materials = new[] { ResourceId.ForemanSigil, ResourceId.GolemHeart, ResourceId.WyrmScale };
                for (int tier = 1; tier <= 3; tier++)
                {
                    BossDefinition boss = defaults.FindBossForTier(tier);
                    Assert.IsNotNull(boss, "boss for tier " + tier);
                    Assert.AreSame(boss, defaults.FindBoss(boss.id));
                    CollectionAssert.Contains(boss.firstWinRewards, R(materials[tier - 1], 1));
                    CollectionAssert.Contains(boss.repeatWinRewards, R(materials[tier - 1], 1));

                    int unlocked = 0;
                    foreach (ExpeditionDefinition e in defaults.Expeditions)
                    {
                        if (e.unlockedByBoss != boss.id) continue;
                        unlocked++;
                        Assert.AreSame(e, defaults.FindExpedition(e.id));
                        Assert.Greater(e.automatedPerHour.Count, 0);
                        Assert.Greater(e.recommendedPower, boss.recommendedPower, "a first clear comes after its boss");
                    }
                    Assert.AreEqual(1, unlocked, "Expeditions unlocked by " + boss.id);
                }

                Assert.IsNotNull(defaults.FindTierRequirement(2));
                Assert.IsNotNull(defaults.FindTierRequirement(3));
                Assert.IsNull(defaults.FindTierRequirement(4), "the prototype has 3 tiers");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaults);
            }
        }
    }
}
