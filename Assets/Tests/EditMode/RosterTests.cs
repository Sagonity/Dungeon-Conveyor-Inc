using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Roster;
using Deepshaft.Save;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    public class RosterTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        const double Tolerance = 1e-4;

        ManualClock clock;
        MinionDefinition miner;    // 10 MP, no trait: 100 Gold/h at tier 1
        MinionDefinition tank;
        MinionDefinition mage;
        GameDatabase database;
        RosterRules rules;
        GameState state;
        FakeBusy busy;

        sealed class FakeBusy : ITeamBusySource
        {
            public bool busy;
            public bool IsAdventureTeamBusy => busy;
        }

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            miner = MakeDefinition("miner", MinionKind.Miner, MinionRole.None);
            tank = MakeDefinition("tank", MinionKind.Adventure, MinionRole.Tank);
            mage = MakeDefinition("mage", MinionKind.Adventure, MinionRole.Mage);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { miner, tank, mage });
            database.EditorSetMining(MiningRules.CreateDefault());
            rules = new RosterRules { minerSlots = 3, adventureSlots = 3 };
            database.EditorSetRoster(rules);
            state = GameState.CreateNew(clock);
            busy = new FakeBusy();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MinionDefinition d in new[] { miner, tank, mage }) UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(database);
        }

        static MinionDefinition MakeDefinition(string id, MinionKind kind, MinionRole role)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, kind, role, Rarity.Common);
            return d;
        }

        MiningService mining;

        RosterService NewRoster()
        {
            mining = new MiningService(state, database, clock);
            return new RosterService(state, database, mining, busy);
        }

        double GoldPerHour() => MiningMath.Get(mining.GetHourlyRates(), ResourceId.Gold);

        // ---------- Swaps (SCRUM-34) ----------

        [Test]
        public void SwapIntoTeam_TakesTheSameSlot_InOneStep()
        {
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("tank");
            OwnedMinion b = roster.AddMinion("mage");
            OwnedMinion c = roster.AddMinion("tank");
            OwnedMinion benched = roster.AddMinion("mage"); // team full

            Assert.AreEqual(AssignResult.NoFreeSlot, roster.AssignToTeam(benched.instanceId));
            Assert.AreEqual(AssignResult.Ok, roster.SwapIntoTeam(benched.instanceId, b.instanceId));

            CollectionAssert.AreEqual(new[] { a, benched, c }, roster.GetTeam(), "The new Minion takes b's slot.");
            Assert.IsFalse(roster.IsAssigned(b.instanceId));
        }

        [Test]
        public void SwapIntoTeam_IsRefused_WhileBusy_OrWithBadIds()
        {
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("tank");
            OwnedMinion benchedMiner = roster.AddMinion("miner");
            roster.UnassignMiner(benchedMiner.instanceId);
            OwnedMinion b = roster.AddMinion("mage");
            roster.RemoveFromTeam(b.instanceId);

            Assert.AreEqual(AssignResult.WrongKind, roster.SwapIntoTeam(benchedMiner.instanceId, a.instanceId));
            Assert.AreEqual(AssignResult.AlreadyAssigned, roster.SwapIntoTeam(a.instanceId, a.instanceId));
            Assert.AreEqual(AssignResult.NotAssigned, roster.SwapIntoTeam(b.instanceId, benchedMiner.instanceId));
            Assert.AreEqual(AssignResult.NotOwned, roster.SwapIntoTeam(999, a.instanceId));

            busy.busy = true;
            Assert.AreEqual(AssignResult.TeamBusy, roster.SwapIntoTeam(b.instanceId, a.instanceId));
            Assert.IsTrue(roster.IsAssigned(a.instanceId));
        }

        [Test]
        public void SwapMiner_BanksTheOldRate_AndKeepsTheSlot()
        {
            rules.minerSlots = 1;
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            OwnedMinion b = roster.AddMinion("miner"); // no free slot
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(AssignResult.Ok, roster.SwapMiner(b.instanceId, a.instanceId));

            CollectionAssert.AreEqual(new[] { b }, roster.GetMiners());
            Assert.AreEqual(100, mining.GetPending(ResourceId.Gold), Tolerance, "Hour 1 was mined by a and banked.");
        }

        // ---------- Miner slots ----------

        [Test]
        public void NewMiners_FillFreeSlots_ExtrasWaitUnassigned()
        {
            RosterService roster = NewRoster();
            for (int i = 0; i < 4; i++) roster.AddMinion("miner");

            Assert.AreEqual(3, roster.GetMiners().Count);
            Assert.AreEqual(1, roster.GetUnassigned(MinionKind.Miner).Count);
            Assert.AreEqual(300, GoldPerHour(), Tolerance);
        }

        [Test]
        public void OnlyAssignedMinersMine_AndUnassigningBanksTheOldRate()
        {
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            roster.AddMinion("miner");
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(AssignResult.Ok, roster.UnassignMiner(a.instanceId));
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(200 + 100, mining.GetPending(ResourceId.Gold), Tolerance);
            Assert.IsFalse(roster.IsAssigned(a.instanceId));
        }

        [Test]
        public void AssigningAMiner_BanksTheOldRate()
        {
            rules.minerSlots = 1;
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            OwnedMinion b = roster.AddMinion("miner"); // no free slot
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(AssignResult.NoFreeSlot, roster.AssignMiner(b.instanceId));
            rules.minerSlots = 2;
            Assert.AreEqual(AssignResult.Ok, roster.AssignMiner(b.instanceId));
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(100 + 200, mining.GetPending(ResourceId.Gold), Tolerance);
            CollectionAssert.AreEqual(new[] { a, b }, roster.GetMiners());
        }

        [Test]
        public void SwappingAMiner_KeepsTheSlotCount()
        {
            rules.minerSlots = 1;
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            OwnedMinion b = roster.AddMinion("miner");

            roster.UnassignMiner(a.instanceId);
            Assert.AreEqual(AssignResult.Ok, roster.AssignMiner(b.instanceId));

            CollectionAssert.AreEqual(new[] { b }, roster.GetMiners());
            CollectionAssert.AreEqual(new[] { a }, roster.GetUnassigned(MinionKind.Miner));
        }

        // ---------- Kinds never mix ----------

        [Test]
        public void MinersOnlyMine_AdventurersOnlyFight()
        {
            RosterService roster = NewRoster();
            OwnedMinion m = state.AddMinion("miner");
            OwnedMinion t = state.AddMinion("tank");

            Assert.AreEqual(AssignResult.WrongKind, roster.AssignToTeam(m.instanceId));
            Assert.AreEqual(AssignResult.WrongKind, roster.AssignMiner(t.instanceId));
            CollectionAssert.DoesNotContain(roster.GetUnassigned(MinionKind.Miner), t);
            CollectionAssert.DoesNotContain(roster.GetUnassigned(MinionKind.Adventure), m);
        }

        [Test]
        public void InvalidRequests_SayWhy()
        {
            RosterService roster = NewRoster();
            OwnedMinion m = roster.AddMinion("miner");
            OwnedMinion t = roster.AddMinion("tank");
            OwnedMinion removed = state.AddMinion("removed_from_game");

            Assert.AreEqual(AssignResult.AlreadyAssigned, roster.AssignMiner(m.instanceId));
            Assert.AreEqual(AssignResult.AlreadyAssigned, roster.AssignToTeam(t.instanceId));
            Assert.AreEqual(AssignResult.NotOwned, roster.AssignMiner(9999));
            Assert.AreEqual(AssignResult.NotOwned, roster.AssignToTeam(removed.instanceId));

            roster.UnassignMiner(m.instanceId);
            Assert.AreEqual(AssignResult.NotAssigned, roster.UnassignMiner(m.instanceId));
            Assert.AreEqual(AssignResult.NotAssigned, roster.RemoveFromTeam(m.instanceId));
        }

        // ---------- Adventure team ----------

        [Test]
        public void NewAdventurers_FillTheTeam_InSlotOrder()
        {
            rules.adventureSlots = 2;
            RosterService roster = NewRoster();
            OwnedMinion t = roster.AddMinion("tank");
            OwnedMinion mg = roster.AddMinion("mage");
            OwnedMinion extra = roster.AddMinion("tank");

            CollectionAssert.AreEqual(new[] { t, mg }, roster.GetTeam());
            CollectionAssert.AreEqual(new[] { extra }, roster.GetUnassigned(MinionKind.Adventure));
            Assert.AreEqual(AssignResult.NoFreeSlot, roster.AssignToTeam(extra.instanceId));
        }

        [Test]
        public void AdventureTeam_DoesNotAffectMining()
        {
            RosterService roster = NewRoster();
            roster.AddMinion("miner");
            OwnedMinion t = roster.AddMinion("tank");
            clock.Advance(TimeSpan.FromHours(1));
            roster.RemoveFromTeam(t.instanceId);
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(200, mining.GetPending(ResourceId.Gold), Tolerance);
        }

        [Test]
        public void BusyTeam_CannotBeEdited_ButMinersCan()
        {
            RosterService roster = NewRoster();
            OwnedMinion t = roster.AddMinion("tank");
            OwnedMinion mg = state.AddMinion("mage");
            OwnedMinion m = roster.AddMinion("miner");
            busy.busy = true; // an automated Expedition is running

            Assert.IsTrue(roster.IsTeamBusy);
            Assert.AreEqual(AssignResult.TeamBusy, roster.AssignToTeam(mg.instanceId));
            Assert.AreEqual(AssignResult.TeamBusy, roster.RemoveFromTeam(t.instanceId));
            OwnedMinion summoned = roster.AddMinion("mage");
            CollectionAssert.AreEqual(new[] { t }, roster.GetTeam());
            CollectionAssert.Contains(roster.GetUnassigned(MinionKind.Adventure), summoned);

            Assert.AreEqual(AssignResult.Ok, roster.UnassignMiner(m.instanceId));

            busy.busy = false; // Expedition stopped and collected
            Assert.AreEqual(AssignResult.Ok, roster.AssignToTeam(mg.instanceId));
        }

        // ---------- Slot counts are data ----------

        [Test]
        public void LoweringTheSlotCount_OnlyTheFirstSlotsWork()
        {
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            roster.AddMinion("miner");
            roster.AddMinion("tank");
            roster.AddMinion("mage");

            rules.minerSlots = 1;
            rules.adventureSlots = 1;

            CollectionAssert.AreEqual(new[] { a }, roster.GetMiners());
            Assert.AreEqual(1, roster.GetTeam().Count);
            Assert.AreEqual(100, GoldPerHour(), Tolerance);
        }

        [Test]
        public void RemovedDefinition_DoesNotTakeASlot()
        {
            rules.minerSlots = 1;
            state.AddMinion("removed_from_game");
            OwnedMinion m = state.AddMinion("miner");
            state.roster.miners.Add(1); // the removed one, from an old save
            state.roster.miners.Add(m.instanceId);
            state.roster.initialized = true;

            RosterService roster = NewRoster();

            CollectionAssert.AreEqual(new[] { m }, roster.GetMiners());
            Assert.AreEqual(100, GoldPerHour(), Tolerance);
        }

        // ---------- Save ----------

        [Test]
        public void Assignment_IsSaved()
        {
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            OwnedMinion b = roster.AddMinion("miner");
            roster.AddMinion("tank");
            OwnedMinion mg = roster.AddMinion("mage");
            roster.UnassignMiner(a.instanceId);
            roster.RemoveFromTeam(mg.instanceId);
            var storage = new MemorySaveStorage();
            new SaveService(storage, clock).Save(state);

            state = new SaveService(storage, clock).Load().State;
            RosterService loaded = NewRoster();

            Assert.AreEqual(1, loaded.GetMiners().Count);
            Assert.AreEqual(b.instanceId, loaded.GetMiners()[0].instanceId);
            Assert.AreEqual(1, loaded.GetTeam().Count);
            Assert.AreEqual("tank", loaded.GetTeam()[0].definitionId);
        }

        [Test]
        public void SaveFromBeforeAssignment_KeepsItsMinersWorking()
        {
            for (int i = 0; i < 4; i++) state.AddMinion("miner");
            state.AddMinion("tank");
            state.roster = null; // as if loaded from a save made before SCRUM-6

            RosterService roster = NewRoster();

            Assert.AreEqual(3, roster.GetMiners().Count); // the first three, in roster order
            Assert.AreEqual(1, roster.GetMiners()[0].instanceId);
            Assert.AreEqual(1, roster.GetTeam().Count);
            Assert.AreEqual(300, GoldPerHour(), Tolerance);
        }

        [Test]
        public void AutoFillOfAnOldSave_RunsOnlyOnce()
        {
            RosterService roster = NewRoster();
            OwnedMinion a = roster.AddMinion("miner");
            roster.UnassignMiner(a.instanceId);

            RosterService again = NewRoster();

            Assert.AreEqual(0, again.GetMiners().Count);
        }

        [Test]
        public void Changed_IsRaised_OnlyOnSuccess()
        {
            RosterService roster = NewRoster();
            OwnedMinion m = roster.AddMinion("miner");
            int raised = 0;
            roster.Changed += () => raised++;

            roster.AssignMiner(m.instanceId); // already assigned
            Assert.AreEqual(0, raised);
            roster.UnassignMiner(m.instanceId);
            Assert.AreEqual(1, raised);
        }
    }
}
