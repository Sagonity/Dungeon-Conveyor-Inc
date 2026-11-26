using System;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Roster;
using Deepshaft.Save;
using Deepshaft.Session;
using Deepshaft.Smith;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deepshaft.Tests
{
    /// <summary>
    /// GameSession (SCRUM-30): new game, starter Minions, save and reload, offline progress,
    /// and the whole Tier 1 loop through the session's actions. Uses the default bosses and
    /// Expeditions; the team is levelled up with paid level-ups (SCRUM-10) so the fights can't be lost.
    /// </summary>
    public class GameSessionTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        const int Seed = 1234;

        ManualClock clock;
        MemorySaveStorage storage;
        MinionDefinition miner;
        MinionDefinition tank;
        MinionDefinition fighter;
        MinionDefinition support;
        MinionDefinition mage;
        GameDatabase database;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            storage = new MemorySaveStorage();
            miner = MakeDefinition("miner", MinionKind.Miner, MinionRole.None);
            tank = MakeDefinition("tank", MinionKind.Adventure, MinionRole.Tank);
            fighter = MakeDefinition("fighter", MinionKind.Adventure, MinionRole.Fighter);
            support = MakeDefinition("support", MinionKind.Adventure, MinionRole.Support);
            mage = MakeDefinition("mage", MinionKind.Adventure, MinionRole.Mage);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { miner, tank, fighter, support, mage });
            database.EditorSetRoster(new RosterRules { minerSlots = 3, adventureSlots = 3 });
            database.EditorSetStarterMinions(new[] { "miner", "tank", "fighter", "support", "mage" });
            // These tests count resources from 0; starting resources have their own test below.
            database.EditorSetNewGameResources(new ResourceAmount[0]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in new[] { miner, tank, fighter, support, mage }) UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(database);
        }

        GameSession NewSession() => new GameSession(database, storage, clock, Seed);

        static MinionDefinition MakeDefinition(string id, MinionKind kind, MinionRole role)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, role, Rarity.Common);
            return d;
        }

        // ---------- New game ----------

        [Test]
        public void NewGame_GetsStarterMinions_AndFillsTheSlots()
        {
            GameSession session = NewSession();

            Assert.AreEqual(LoadStatus.NewGame, session.LoadStatus);
            Assert.AreEqual(5, session.State.minions.Count);
            Assert.AreEqual(1, session.Roster.GetMiners().Count);
            Assert.AreEqual(3, session.Roster.GetTeam().Count, "Tank, Fighter and Support fill the 3 team slots.");
            Assert.AreEqual(1, session.Roster.GetUnassigned(MinionKind.Adventure).Count, "The Mage waits on the bench.");
            Assert.AreEqual(0.0, session.Offline.HoursAway);
        }

        [Test]
        public void NewGame_IsSavedAtOnce()
        {
            NewSession();
            Assert.IsTrue(storage.TryReadPrimary(out string text));
            Assert.IsFalse(string.IsNullOrEmpty(text));
        }

        [Test]
        public void Reload_DoesNotGiveStarterMinionsAgain()
        {
            NewSession();
            GameSession reloaded = NewSession();

            Assert.AreEqual(LoadStatus.Loaded, reloaded.LoadStatus);
            Assert.AreEqual(5, reloaded.State.minions.Count);
        }

        [Test]
        public void MissingStarterMinion_IsSkippedWithAWarning()
        {
            database.EditorSetStarterMinions(new[] { "miner", "removed_minion" });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("removed_minion"));

            GameSession session = NewSession();

            Assert.AreEqual(1, session.State.minions.Count);
        }

        // ---------- Mining, saving and offline progress ----------

        [Test]
        public void Mining_PaysForTimeAway_AfterReload()
        {
            NewSession();
            clock.Advance(TimeSpan.FromHours(2));

            GameSession reloaded = NewSession();

            Assert.AreEqual(2.0, reloaded.Offline.HoursAway, 1e-6);
            Assert.AreEqual(200.0, MiningMath.Get(reloaded.Offline.Mine, ResourceId.Gold), 1e-6, "1 Miner at the reference power mines 100 Gold/h in Tier 1.");

            reloaded.CollectMine();
            Assert.AreEqual(200, reloaded.Wallet.Get(ResourceId.Gold));
        }

        [Test]
        public void CollectedResources_AreKeptAfterReload()
        {
            GameSession session = NewSession();
            clock.Advance(TimeSpan.FromHours(1));
            session.CollectMine();

            GameSession reloaded = NewSession();

            Assert.AreEqual(100, reloaded.Wallet.Get(ResourceId.Gold));
            Assert.AreEqual(0.0, reloaded.Mining.GetPending(ResourceId.Gold), 1e-6);
        }

        [Test]
        public void TeamChanges_AreKeptAfterReload()
        {
            GameSession session = NewSession();
            int tankId = session.Roster.GetTeam()[0].instanceId;
            int mageId = session.Roster.GetUnassigned(MinionKind.Adventure)[0].instanceId;

            Assert.AreEqual(AssignResult.Ok, session.RemoveFromTeam(tankId));
            Assert.AreEqual(AssignResult.Ok, session.AssignToTeam(mageId));

            GameSession reloaded = NewSession();
            Assert.IsFalse(reloaded.Roster.IsAssigned(tankId));
            Assert.IsTrue(reloaded.Roster.IsAssigned(mageId));
        }

        [Test]
        public void Actions_RaiseChanged()
        {
            GameSession session = NewSession();
            int count = 0;
            session.Changed += () => count++;

            session.Wallet.Add(ResourceId.Gold, 1000);
            session.Wallet.Add(ResourceId.Iron, 1000);
            session.CollectMine();
            session.LevelUpMinion(session.State.minions[0].instanceId);

            Assert.AreEqual(2, count);
        }

        // ---------- The Tier 1 loop ----------

        [Test]
        public void Tier1Loop_BossExpeditionAndNextTier_SurviveReload()
        {
            GameSession session = NewSession();
            foreach (OwnedMinion m in session.Roster.GetTeam()) LevelTo(session, m.instanceId, 16);

            var boss = session.FightBoss(DefaultAdventureContent.Tier1Boss);
            Assert.IsTrue(boss.Won, boss.DescribeLoss());
            Assert.AreEqual(2, session.Tiers.HighestUnlockedTier, "Beating the Tier 1 boss unlocks Tier 2.");
            Assert.AreEqual(1, session.Wallet.Get(ResourceId.ForemanSigil));

            var clear = session.FirstClearExpedition(DefaultAdventureContent.Tier1Expedition);
            Assert.IsTrue(clear.Won);
            Assert.AreEqual(ExpeditionCheck.Ok, session.StartExpedition(DefaultAdventureContent.Tier1Expedition));
            Assert.IsTrue(session.Roster.IsTeamBusy);

            clock.Advance(TimeSpan.FromHours(3));
            GameSession reloaded = NewSession();

            Assert.AreEqual(2, reloaded.Tiers.HighestUnlockedTier);
            Assert.IsTrue(reloaded.Bosses.IsDefeated(DefaultAdventureContent.Tier1Boss));
            Assert.AreEqual(ExpeditionStatus.Running, reloaded.Expeditions.GetStatus(DefaultAdventureContent.Tier1Expedition));
            Assert.AreEqual(3.0, reloaded.Offline.HoursAway, 1e-6);

            long silkBefore = reloaded.Wallet.Get(ResourceId.SporeSilk);
            var collected = reloaded.StopExpedition();
            Assert.AreEqual(ExpeditionCheck.Ok, collected.Check);
            Assert.AreEqual(silkBefore + 6, reloaded.Wallet.Get(ResourceId.SporeSilk), "2 Spore Silk/h for 3 h.");
            Assert.IsFalse(reloaded.Roster.IsTeamBusy);
        }

        // ---------- Minion levels (SCRUM-10) ----------

        [Test]
        public void LevelUpMiner_PaysAndRaisesTheMiningRate_AndSurvivesReload()
        {
            GameSession session = NewSession();
            int minerId = session.Roster.GetMiners()[0].instanceId;
            double goldPerHourBefore = MiningMath.Get(session.Mining.GetHourlyRates(), ResourceId.Gold);
            session.Wallet.Add(ResourceId.Gold, 100);
            session.Wallet.Add(ResourceId.Iron, 100);

            MinionUpgradeCheck result = session.LevelUpMinion(minerId);

            Assert.AreEqual(MinionUpgradeStatus.Ok, result.Status);
            Assert.AreEqual(2, result.Level);
            Assert.AreEqual(75, session.Wallet.Get(ResourceId.Gold), "Level 1 → 2 costs 25 Gold by default.");
            Assert.AreEqual(90, session.Wallet.Get(ResourceId.Iron), "Level 1 → 2 costs 10 Iron by default.");
            Assert.Greater(MiningMath.Get(session.Mining.GetHourlyRates(), ResourceId.Gold), goldPerHourBefore);

            GameSession reloaded = NewSession();
            Assert.AreEqual(2, reloaded.State.FindMinion(minerId).level);
        }

        [Test]
        public void LevelUp_WithoutResources_ChangesNothing()
        {
            GameSession session = NewSession();
            int minerId = session.Roster.GetMiners()[0].instanceId;

            MinionUpgradeCheck result = session.LevelUpMinion(minerId);

            Assert.AreEqual(MinionUpgradeStatus.CantAfford, result.Status);
            Assert.AreEqual(1, session.State.FindMinion(minerId).level);
        }

        // ---------- Playtest fixes (SCRUM-32) ----------

        [Test]
        public void NewGame_StartsWithTheStartingResources_OnlyOnce()
        {
            database.EditorSetNewGameResources(GameDatabase.DefaultNewGameResources());

            GameSession session = NewSession();
            Assert.AreEqual(400, session.Wallet.Get(ResourceId.Gold));
            Assert.AreEqual(150, session.Wallet.Get(ResourceId.Iron));

            session.Wallet.TrySpend(ResourceId.Gold, 400);
            session.Save();
            GameSession reloaded = NewSession();
            Assert.AreEqual(0, reloaded.Wallet.Get(ResourceId.Gold), "A loaded save doesn't get them again.");
        }

        [Test]
        public void StartingResources_BuySeveralLevelUpsStraightAway()
        {
            database.EditorSetNewGameResources(GameDatabase.DefaultNewGameResources());
            GameSession session = NewSession();
            int levels = 0;
            foreach (OwnedMinion m in session.Roster.GetTeam()) levels += session.LevelUpMinionMax(m.instanceId).Levels;
            Assert.GreaterOrEqual(levels, 6, "The first session should have upgrades to buy.");
        }

        [Test]
        public void Tier1BossFirstWin_AlsoGivesGold()
        {
            var boss = DefaultAdventureContent.Bosses()[0];
            Assert.IsTrue(boss.firstWinRewards.Exists(r => r.id == ResourceId.Gold && r.amount == 200));
            Assert.IsFalse(boss.repeatWinRewards.Exists(r => r.id == ResourceId.Gold), "Only the first win.");
        }

        [Test]
        public void LevelUpMax_BuysEverythingAffordable_InOneSaveAndOneLogLine()
        {
            GameSession session = NewSession();
            int minerId = session.Roster.GetMiners()[0].instanceId;
            session.Wallet.Add(ResourceId.Gold, 100); // 25 + 28 + 31 = 84 for 3 levels; the 4th costs 35
            session.Wallet.Add(ResourceId.Iron, 1000);
            int logBefore = session.Playtest.Events.Count;

            BulkPurchase preview = session.Minions.CheckLevelUpMax(minerId, session.Wallet);
            BulkPurchase bought = session.LevelUpMinionMax(minerId);

            Assert.AreEqual(3, preview.Levels);
            Assert.AreEqual(3, bought.Levels);
            Assert.AreEqual(4, bought.ToLevel);
            Assert.AreEqual(4, session.State.FindMinion(minerId).level);
            Assert.AreEqual(16, session.Wallet.Get(ResourceId.Gold));
            Assert.AreEqual(logBefore + 1, session.Playtest.Events.Count);
            Assert.AreEqual("level_up_max", session.Playtest.Events[logBefore].type);
            Assert.AreEqual(4, NewSession().State.FindMinion(minerId).level, "Saved.");
        }

        [Test]
        public void LevelUpMax_StopsAtTheLevelCap()
        {
            GameSession session = NewSession();
            int minerId = session.Roster.GetMiners()[0].instanceId;
            session.Wallet.Add(ResourceId.Gold, 10_000_000);
            session.Wallet.Add(ResourceId.Iron, 10_000_000);

            Assert.AreEqual(19, session.LevelUpMinionMax(minerId).Levels);
            Assert.AreEqual(20, session.State.FindMinion(minerId).level);
            Assert.AreEqual(0, session.LevelUpMinionMax(minerId).Levels, "At the cap nothing more is bought.");
        }

        [Test]
        public void UpgradeGearMax_StopsAtTheItemCap_AndWithoutMoney()
        {
            GameSession session = NewSession();
            int tankId = session.Roster.GetTeam()[0].instanceId;
            Assert.AreEqual(0, session.UpgradeGearMax(tankId, EquipmentSlot.Weapon).Levels, "Nothing affordable.");
            Assert.AreEqual(0, session.Smith.CheckUpgradeMax(tankId, EquipmentSlot.Tool, session.Wallet).Levels, "Wrong slot.");

            session.Wallet.Add(ResourceId.Gold, 100_000);
            session.Wallet.Add(ResourceId.Iron, 100_000);
            BulkPurchase bought = session.UpgradeGearMax(tankId, EquipmentSlot.Weapon);

            Assert.AreEqual(3, bought.Levels, "Level 1 → 4, the cap before the first breakthrough.");
            Assert.AreEqual(4, EquipmentRules.GetLevel(session.State.FindMinion(tankId), EquipmentSlot.Weapon));
        }

        // ---------- Where to get rare materials (SCRUM-33) ----------

        [Test]
        public void RareMaterialHint_FollowsTheExpeditionsProgress()
        {
            GameSession session = NewSession();
            string Hint() => SourceHints.ForRareMaterial(session, ResourceId.SporeSilk);

            StringAssert.StartsWith("beat Vein Warden to open Spore Caverns", Hint());

            foreach (OwnedMinion m in session.Roster.GetTeam()) LevelTo(session, m.instanceId, 16);
            Assert.IsTrue(session.FightBoss(DefaultAdventureContent.Tier1Boss).Won);
            StringAssert.StartsWith("first-clear Spore Caverns", Hint());

            Assert.IsTrue(session.FirstClearExpedition(DefaultAdventureContent.Tier1Expedition).Won);
            StringAssert.Contains("collected when you stop it", Hint());

            session.StartExpedition(DefaultAdventureContent.Tier1Expedition);
            StringAssert.Contains("stop the Expedition to collect", Hint());
        }

        [Test]
        public void BossMaterialHint_SaysWhichBoss_AndThatRepeatWinsCount()
        {
            GameSession session = NewSession();
            StringAssert.StartsWith("reach Tier 2", SourceHints.ForRareMaterial(session, ResourceId.GolemHeart));
            Assert.AreEqual("beat Vein Warden", SourceHints.ForRareMaterial(session, ResourceId.ForemanSigil));

            foreach (OwnedMinion m in session.Roster.GetTeam()) LevelTo(session, m.instanceId, 16);
            Assert.IsTrue(session.FightBoss(DefaultAdventureContent.Tier1Boss).Won);
            StringAssert.Contains("again", SourceHints.ForRareMaterial(session, ResourceId.ForemanSigil));
        }

        [Test]
        public void RareMaterialHint_IsNullForOrdinaryResources()
        {
            GameSession session = NewSession();
            Assert.IsNull(SourceHints.ForRareMaterial(session, ResourceId.Gold));
            Assert.IsNull(SourceHints.ForRareMaterial(session, ResourceId.BasicShard));
        }

        // ---------- Smith and HQ (SCRUM-9) ----------

        [Test]
        public void UpgradeGear_ThroughTheSession_IsSaved()
        {
            GameSession session = NewSession();
            int minerId = session.Roster.GetMiners()[0].instanceId;
            session.Wallet.Add(session.Smith.CheckUpgrade(minerId, EquipmentSlot.Tool).Cost);

            Assert.AreEqual(SmithStatus.Ok, session.UpgradeGear(minerId, EquipmentSlot.Tool).Status);

            GameSession reloaded = NewSession();
            Assert.AreEqual(2, EquipmentRules.GetLevel(reloaded.State.FindMinion(minerId), EquipmentSlot.Tool));
        }

        [Test]
        public void ProductionPerHour_AddsTheMineAndTheAutomatedExpedition()
        {
            GameSession session = NewSession();
            foreach (OwnedMinion m in session.Roster.GetTeam()) LevelTo(session, m.instanceId, 16);
            Assert.IsTrue(session.FightBoss(DefaultAdventureContent.Tier1Boss).Won);
            Assert.IsTrue(session.FirstClearExpedition(DefaultAdventureContent.Tier1Expedition).Won);
            session.StartExpedition(DefaultAdventureContent.Tier1Expedition);

            var total = session.GetProductionPerHour();

            Assert.AreEqual(MiningMath.Get(session.Mining.GetHourlyRates(), ResourceId.Gold), MiningMath.Get(total, ResourceId.Gold), 1e-6);
            Assert.AreEqual(2.0, MiningMath.Get(total, ResourceId.SporeSilk), 1e-6, "2 Spore Silk/h from the Expedition.");
        }

        // ---------- Playtest log (SCRUM-8) ----------

        [Test]
        public void PlaytestLog_RecordsActions_AndSurvivesReload()
        {
            GameSession session = NewSession();
            session.FightBoss(DefaultAdventureContent.Tier1Boss);
            session.LogView("Minions");
            session.LevelUpMinion(session.Roster.GetMiners()[0].instanceId); // no Gold yet

            GameSession reloaded = NewSession();
            var types = new System.Collections.Generic.List<string>();
            foreach (PlaytestEvent e in reloaded.Playtest.Events) types.Add(e.type);

            CollectionAssert.AreEqual(new[] { "new_game", "boss_fight", "view", "level_up", "session_start" }, types);
            StringAssert.Contains("spore_foreman", reloaded.Playtest.Events[1].detail);
            StringAssert.Contains("rec=80", reloaded.Playtest.Events[1].detail);
            StringAssert.Contains("CantAfford", reloaded.Playtest.Events[3].detail);
        }

        [Test]
        public void PlaytestLog_Csv_HasTheMinutesAndQuotesCommas()
        {
            GameSession session = NewSession();
            clock.Advance(TimeSpan.FromMinutes(90));
            session.LogView("a, \"quoted\" thing");

            string[] lines = session.Playtest.ToCsv().Split('\n');

            Assert.AreEqual("utc,minutes,type,detail", lines[0]);
            StringAssert.EndsWith(",90.0,view,\"a, \"\"quoted\"\" thing\"", lines[2]);
        }

        [Test]
        public void PlaytestLog_KeepsOnlyTheNewestEvents()
        {
            GameSession session = NewSession();
            for (int i = 0; i < PlaytestLog.MaxEvents + 50; i++) session.LogView("tab " + i);

            Assert.AreEqual(PlaytestLog.MaxEvents, session.Playtest.Events.Count);
            Assert.AreEqual("tab " + (PlaytestLog.MaxEvents + 49), session.Playtest.Events[PlaytestLog.MaxEvents - 1].detail);
        }

        /// <summary>Gives exactly what the level-ups cost and buys them through the session.</summary>
        static void LevelTo(GameSession session, int instanceId, int level)
        {
            while (session.State.FindMinion(instanceId).level < level)
            {
                MinionUpgradeCheck check = session.Minions.CheckLevelUp(instanceId);
                session.Wallet.Add(check.Cost);
                Assert.AreEqual(MinionUpgradeStatus.Ok, session.LevelUpMinion(instanceId).Status);
            }
        }
    }
}
