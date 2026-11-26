using System;
using System.Linq;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.Roster;
using Deepshaft.Save;
using Deepshaft.Session;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>
    /// The next goal (SCRUM-36): one clear next step, following Tier 1 boss → Expedition first clear →
    /// automate → next boss, and how to get stronger when the next fight is too hard.
    /// Uses the default bosses and Expeditions.
    /// </summary>
    public class NextGoalTests
    {
        const int Seed = 1234;

        MinionDefinition miner, tank, fighter, support, mage;
        GameDatabase database;
        GameSession game;

        [SetUp]
        public void SetUp()
        {
            miner = Make("miner", MinionKind.Miner, MinionRole.None);
            tank = Make("tank", MinionKind.Adventure, MinionRole.Tank);
            fighter = Make("fighter", MinionKind.Adventure, MinionRole.Fighter);
            support = Make("support", MinionKind.Adventure, MinionRole.Support);
            mage = Make("mage", MinionKind.Adventure, MinionRole.Mage);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { miner, tank, fighter, support, mage });
            database.EditorSetRoster(new RosterRules { minerSlots = 3, adventureSlots = 3 });
            database.EditorSetStarterMinions(new[] { "miner", "tank", "fighter", "support" });
            database.EditorSetNewGameResources(new ResourceAmount[0]);
            game = new GameSession(database, new MemorySaveStorage(), new ManualClock(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)), Seed);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in new[] { miner, tank, fighter, support, mage }) UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(database);
        }

        static MinionDefinition Make(string id, MinionKind kind, MinionRole role)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, role, Rarity.Common);
            return d;
        }

        void Rich()
        {
            game.Wallet.Add(ResourceId.Gold, 1000000);
            game.Wallet.Add(ResourceId.Iron, 1000000);
        }

        void LevelTeamTo(int level)
        {
            foreach (OwnedMinion m in game.Roster.GetTeam())
            {
                while (m.level < level && game.Minions.CheckLevelUp(m.instanceId, game.Wallet).CanDo) game.LevelUpMinion(m.instanceId);
            }
        }

        string Boss(int tier) => database.FindBossForTier(tier).id;
        string Expedition(int tier) => database.Expeditions.First(e => e.tier == tier).id;

        [Test]
        public void NewGame_TooWeakAndNothingToSpend_SaysToComeBackLater()
        {
            Assume.That(game.Bosses.GetTeamPower(), Is.LessThan(database.FindBossForTier(1).recommendedPower * NextGoal.WithinReach));

            NextGoal goal = NextGoal.For(game);

            Assert.AreEqual(GoalKind.Wait, goal.Kind);
            Assert.AreEqual(Boss(1), goal.TargetId, "The goal is still about the Tier 1 boss.");
        }

        [Test]
        public void TooWeak_WithGoldToSpend_SaysToGetStronger_OnTheMinionsTab()
        {
            Assume.That(game.Bosses.GetTeamPower(), Is.LessThan(database.FindBossForTier(1).recommendedPower * NextGoal.WithinReach));
            Rich();

            NextGoal goal = NextGoal.For(game);

            Assert.AreEqual(GoalKind.GetStronger, goal.Kind);
            Assert.AreEqual(GoalPlace.Minions, goal.Place);
            StringAssert.Contains("/ " + database.FindBossForTier(1).recommendedPower.ToString("0"), goal.Detail, "Shows power vs recommended.");
        }

        [Test]
        public void TooWeak_NothingToSpend_ButTheMineHasSomething_SaysToCollect()
        {
            Assume.That(game.Bosses.GetTeamPower(), Is.LessThan(database.FindBossForTier(1).recommendedPower * NextGoal.WithinReach));
            ((ManualClock)game.Clock).Advance(TimeSpan.FromMinutes(30));

            Assert.AreEqual(GoalKind.CollectMine, NextGoal.For(game).Kind);
        }

        [Test]
        public void FollowsThePath_Boss_FirstClear_Automate()
        {
            Rich();
            LevelTeamTo(10);

            NextGoal goal = NextGoal.For(game);
            Assert.AreEqual(GoalKind.BeatBoss, goal.Kind);
            Assert.AreEqual(GoalPlace.Boss, goal.Place);
            Assert.AreEqual(Boss(1), goal.TargetId);
            Assert.IsTrue(game.FightBoss(Boss(1)).Won);

            goal = NextGoal.For(game);
            Assert.AreEqual(GoalKind.ClearExpedition, goal.Kind, "Next is the Tier 1 Expedition, not the Tier 2 boss.");
            Assert.AreEqual(Expedition(1), goal.TargetId);
            Assert.IsTrue(game.FirstClearExpedition(Expedition(1)).Won);

            // The Tier 2 boss out of reach: farm the cleared Expedition meanwhile.
            database.FindBossForTier(2).recommendedPower = 100000f;
            goal = NextGoal.For(game);
            Assert.AreEqual(GoalKind.Automate, goal.Kind);
            Assert.AreEqual(GoalPlace.Expeditions, goal.Place);
            Assert.AreEqual(Expedition(1), goal.TargetId);

            game.StartExpedition(Expedition(1));
            goal = NextGoal.For(game);
            Assert.AreNotEqual(GoalKind.Automate, goal.Kind, "Once it runs, the goal moves on.");
            Assert.AreEqual(Boss(2), goal.TargetId);
        }

        [Test]
        public void NextFightInReach_WhileTheTeamIsOnAnExpedition_SaysToStopIt()
        {
            Rich();
            LevelTeamTo(10);
            game.FightBoss(Boss(1));
            game.FirstClearExpedition(Expedition(1));
            game.StartExpedition(Expedition(1));
            database.FindBossForTier(2).recommendedPower = 1f;

            NextGoal goal = NextGoal.For(game);

            Assert.AreEqual(GoalKind.StopExpedition, goal.Kind);
            Assert.AreEqual(Boss(2), goal.TargetId);
        }

        [Test]
        public void StrongerMinionOnTheBench_ComesFirst_WithTheWeakestToSwapOut()
        {
            Rich();
            OwnedMinion benched = game.Roster.AddMinion("mage");
            Assume.That(game.Roster.IsAssigned(benched.instanceId), Is.False, "The team is full, so the Mage goes to the bench.");
            while (benched.level < 10) game.LevelUpMinion(benched.instanceId);

            NextGoal goal = NextGoal.For(game);

            Assert.AreEqual(GoalKind.SwapIn, goal.Kind);
            Assert.AreEqual(benched.instanceId.ToString(), goal.TargetId);
            OwnedMinion weakest = game.Roster.GetTeam().OrderBy(m => NextGoal.MemberPower(game, m)).First();
            Assert.AreEqual(weakest.instanceId, goal.SwapOutId);

            Assert.AreEqual(AssignResult.Ok, game.SwapIntoTeam(benched.instanceId, goal.SwapOutId));
            Assert.AreNotEqual(GoalKind.SwapIn, NextGoal.For(game).Kind, "The weaker Minion now on the bench isn't suggested back.");
        }

        [Test]
        public void WeakerMinionOnTheBench_IsNotSuggested()
        {
            Rich();
            LevelTeamTo(5);
            game.Roster.AddMinion("mage");

            Assert.AreNotEqual(GoalKind.SwapIn, NextGoal.For(game).Kind);
        }

        [Test]
        public void AfterALostFight_SaysToGetStronger_UntilTheTeamImproved()
        {
            Rich();
            LevelTeamTo(10);
            database.FindBossForTier(1).recommendedPower = game.Bosses.GetTeamPower(); // "ready" on paper…
            database.FindBossForTier(1).enemy.hp = 1000000f;                            // …but it can't be won
            Assume.That(NextGoal.For(game).Kind, Is.EqualTo(GoalKind.BeatBoss));

            Assert.IsFalse(game.FightBoss(Boss(1)).Won);
            NextGoal goal = NextGoal.For(game);
            Assert.AreEqual(GoalKind.GetStronger, goal.Kind, "Not the same fight again at the same power.");
            Assert.AreEqual(Boss(1), goal.TargetId);

            LevelTeamTo(11);
            Assert.AreEqual(GoalKind.BeatBoss, NextGoal.For(game).Kind, "Stronger than when it lost: try again.");
        }

        [TestCase(66f, 135f, true)]
        [TestCase(181f, 240f, false)]
        [TestCase(66f, 80f, false)]
        [TestCase(66f, 85f, false)]
        public void FarBelow_IsUnderThreeQuartersOfTheRecommendedPower(float power, float recommended, bool expected)
        {
            Assert.AreEqual(expected, NextGoal.IsFarBelow(power, recommended));
        }
    }
}
