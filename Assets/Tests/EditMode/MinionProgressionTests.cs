using System;
using System.Collections.Generic;
using Deepshaft.Combat;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Save;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>
    /// Minion levels, breakthroughs and per-Minion gear (SCRUM-10). Uses the default
    /// <see cref="MinionProgressionRules"/> and <see cref="EquipmentRules"/>, and the default
    /// <see cref="MinionDefinition"/> numbers: level cap 20, +10 per breakthrough.
    /// </summary>
    public class MinionProgressionTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        ManualClock clock;
        MinionDefinition miner;
        MinionDefinition fighter;
        GameDatabase database;
        GameState state;
        Wallet wallet;
        MiningService mining;
        MinionProgressionService progression;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock(Start);
            miner = ScriptableObject.CreateInstance<MinionDefinition>();
            miner.EditorSetup("miner", MinionKind.Miner, MinionRole.None, Rarity.Common);
            fighter = ScriptableObject.CreateInstance<MinionDefinition>();
            fighter.EditorSetup("fighter", MinionKind.Adventure, MinionRole.Fighter, Rarity.Common);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.EditorSetMinions(new[] { miner, fighter });
            state = GameState.CreateNew(clock);
            wallet = new Wallet(state.wallet);
            mining = new MiningService(state, database, clock, new EquipmentToolBonus(() => database.Equipment));
            progression = new MinionProgressionService(state, database, mining);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(miner);
            UnityEngine.Object.DestroyImmediate(fighter);
            UnityEngine.Object.DestroyImmediate(database);
        }

        OwnedMinion AddMiner() => mining.AddMinion("miner");

        /// <summary>Levels up by paying exactly the cost each time.</summary>
        void LevelTo(OwnedMinion m, int level)
        {
            while (m.level < level)
            {
                wallet.Add(progression.CheckLevelUp(m.instanceId).Cost);
                Assert.AreEqual(MinionUpgradeStatus.Ok, progression.TryLevelUp(m.instanceId, wallet).Status);
            }
        }

        // ---------- Levels ----------

        [Test]
        public void LevelUpCost_IsGoldAndIron_AndGrowsWithLevel()
        {
            MinionProgressionRules rules = MinionProgressionRules.CreateDefault();

            List<ResourceAmount> first = rules.GetLevelUpCost(1);
            List<ResourceAmount> later = rules.GetLevelUpCost(10);

            CollectionAssert.AreEquivalent(new[] { ResourceId.Gold, ResourceId.Iron }, first.ConvertAll(c => c.id));
            Assert.AreEqual(25, first.Find(c => c.id == ResourceId.Gold).amount);
            Assert.Greater(later.Find(c => c.id == ResourceId.Gold).amount, 25);
        }

        [Test]
        public void LevelUp_Pays_AndRaisesTheLevel()
        {
            OwnedMinion m = AddMiner();
            wallet.Add(ResourceId.Gold, 30);
            wallet.Add(ResourceId.Iron, 10);

            MinionUpgradeCheck result = progression.TryLevelUp(m.instanceId, wallet);

            Assert.AreEqual(MinionUpgradeStatus.Ok, result.Status);
            Assert.AreEqual(2, m.level);
            Assert.AreEqual(5, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(0, wallet.Get(ResourceId.Iron));
        }

        [Test]
        public void LevelUp_CantAfford_SaysWhatIsMissing_AndSpendsNothing()
        {
            OwnedMinion m = AddMiner();
            wallet.Add(ResourceId.Gold, 100);

            MinionUpgradeCheck result = progression.TryLevelUp(m.instanceId, wallet);

            Assert.AreEqual(MinionUpgradeStatus.CantAfford, result.Status);
            Assert.AreEqual(1, m.level);
            Assert.AreEqual(100, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(1, result.Shortfall.Count);
            Assert.AreEqual(ResourceId.Iron, result.Shortfall[0].id);
            Assert.AreEqual(10, result.Shortfall[0].amount);
        }

        [Test]
        public void LevelUp_StopsAtTheLevelCap()
        {
            OwnedMinion m = AddMiner();
            LevelTo(m, 20);
            wallet.Add(ResourceId.Gold, 1_000_000);
            wallet.Add(ResourceId.Iron, 1_000_000);

            MinionUpgradeCheck result = progression.TryLevelUp(m.instanceId, wallet);

            Assert.AreEqual(MinionUpgradeStatus.AtLevelCap, result.Status);
            Assert.AreEqual(20, m.level);
            Assert.AreEqual(20, result.MaxLevel);
            Assert.AreEqual(1_000_000, wallet.Get(ResourceId.Gold), "Nothing is spent at the cap.");
        }

        [Test]
        public void UnknownMinion_IsNotFound()
        {
            Assert.AreEqual(MinionUpgradeStatus.NotFound, progression.CheckLevelUp(999).Status);
            Assert.AreEqual(MinionUpgradeStatus.NotFound, progression.TryBreakthrough(999, wallet).Status);
        }

        [Test]
        public void MinerLevelUp_BanksTheOldRateFirst()
        {
            OwnedMinion m = AddMiner();
            clock.Advance(TimeSpan.FromHours(1));
            wallet.Add(progression.CheckLevelUp(m.instanceId).Cost);

            progression.TryLevelUp(m.instanceId, wallet);
            clock.Advance(TimeSpan.FromHours(1));

            // Hour 1 at 10 MP (100 Gold), hour 2 at 12 MP (120 Gold).
            Assert.AreEqual(220.0, mining.GetPending(ResourceId.Gold), 1e-6);
        }

        // ---------- Breakthroughs ----------

        [Test]
        public void Breakthrough_NeedsTheLevelCap_ButShowsItsCostEarly()
        {
            OwnedMinion m = AddMiner();

            MinionUpgradeCheck check = progression.CheckBreakthrough(m.instanceId, wallet);

            Assert.AreEqual(MinionUpgradeStatus.BelowLevelCap, check.Status);
            Assert.IsTrue(HasCost(check, ResourceId.SporeSilk));
        }

        [Test]
        public void Breakthroughs_UseEachTiersExpeditionMaterial_InOrder()
        {
            var expected = new[] { ResourceId.SporeSilk, ResourceId.EmberCore, ResourceId.PrismDust };
            OwnedMinion m = AddMiner();

            for (int i = 0; i < expected.Length; i++)
            {
                LevelTo(m, progression.GetMaxLevel(m));
                MinionUpgradeCheck check = progression.CheckBreakthrough(m.instanceId, wallet);
                Assert.IsTrue(HasCost(check, expected[i]), "Breakthrough " + (i + 1) + " costs " + expected[i]);

                wallet.Add(check.Cost);
                MinionUpgradeCheck done = progression.TryBreakthrough(m.instanceId, wallet);

                Assert.AreEqual(MinionUpgradeStatus.Ok, done.Status);
                Assert.AreEqual(i + 1, m.breakthroughs);
                Assert.AreEqual(20 + 10 * (i + 1), done.MaxLevel, "Each breakthrough raises the cap by 10.");
            }

            LevelTo(m, progression.GetMaxLevel(m));
            Assert.AreEqual(50, m.level);
            Assert.AreEqual(MinionUpgradeStatus.NoMoreBreakthroughs, progression.CheckBreakthrough(m.instanceId, wallet).Status);
            Assert.AreEqual(MinionUpgradeStatus.AtLevelCap, progression.CheckLevelUp(m.instanceId, wallet).Status);
        }

        [Test]
        public void Breakthrough_WithoutTheMaterial_ChangesNothing()
        {
            OwnedMinion m = AddMiner();
            LevelTo(m, 20);
            wallet.Add(ResourceId.Gold, 1_000_000);
            wallet.Add(ResourceId.Iron, 1_000_000);

            MinionUpgradeCheck result = progression.TryBreakthrough(m.instanceId, wallet);

            Assert.AreEqual(MinionUpgradeStatus.CantAfford, result.Status);
            Assert.AreEqual(0, m.breakthroughs);
            Assert.AreEqual(1_000_000, wallet.Get(ResourceId.Gold));
        }

        static bool HasCost(MinionUpgradeCheck check, ResourceId id)
        {
            foreach (ResourceAmount c in check.Cost) if (c.id == id && c.amount > 0) return true;
            return false;
        }

        // ---------- Gear (per Minion) ----------

        [Test]
        public void Slots_MinersHaveATool_AdventurersAWeaponAndArmor()
        {
            CollectionAssert.AreEqual(new[] { EquipmentSlot.Tool }, EquipmentRules.SlotsFor(MinionKind.Miner));
            CollectionAssert.AreEqual(new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }, EquipmentRules.SlotsFor(MinionKind.Adventure));
        }

        [Test]
        public void BasicGear_IsLevel1_AndGivesNoBonus()
        {
            OwnedMinion m = AddMiner();

            Assert.AreEqual(1, EquipmentRules.GetLevel(m, EquipmentSlot.Tool));
            Assert.AreEqual(0f, database.Equipment.GetBonus(m, EquipmentSlot.Tool));
            Assert.AreEqual(100.0, MiningMath.Get(mining.GetHourlyRates(), ResourceId.Gold), 1e-6);
        }

        [Test]
        public void ToolLevel_RaisesOnlyThatMinersOutput()
        {
            OwnedMinion a = AddMiner();
            AddMiner();

            mining.ChangeRate(() => EquipmentRules.GetOrAdd(a, EquipmentSlot.Tool).level = 3);

            // Miner A: 100 × (1 + 2 × 10%) = 120; miner B: 100.
            Assert.AreEqual(220.0, MiningMath.Get(mining.GetHourlyRates(), ResourceId.Gold), 1e-4);
        }

        [Test]
        public void WeaponAndArmor_RaiseCombatStats()
        {
            OwnedMinion m = state.AddMinion("fighter");
            float atk = Combatant.ForMinion(fighter, m, database.Combat, database.Equipment).Atk;
            float hp = Combatant.ForMinion(fighter, m, database.Combat, database.Equipment).MaxHp;

            EquipmentRules.GetOrAdd(m, EquipmentSlot.Weapon).level = 4;
            EquipmentRules.GetOrAdd(m, EquipmentSlot.Armor).level = 2;
            Combatant geared = Combatant.ForMinion(fighter, m, database.Combat, database.Equipment);

            Assert.AreEqual(atk * 1.24f, geared.Atk, 1e-3, "Weapon level 4: +3 × 8%.");
            Assert.AreEqual(hp * 1.08f, geared.MaxHp, 1e-3, "Armor level 2: +8%.");
        }

        [Test]
        public void Gear_IsSaved_AndOldSavesWithoutGearLoad()
        {
            var storage = new MemorySaveStorage();
            var saves = new SaveService(storage, clock);
            OwnedMinion m = AddMiner();
            EquipmentRules.GetOrAdd(m, EquipmentSlot.Tool).level = 3;
            m.breakthroughs = 1;
            saves.Save(state);

            GameState loaded = saves.Load().State;
            OwnedMinion back = loaded.FindMinion(m.instanceId);
            Assert.AreEqual(3, EquipmentRules.GetLevel(back, EquipmentSlot.Tool));
            Assert.AreEqual(1, back.breakthroughs);

            back.equipment = null; // a save from before SCRUM-10
            loaded.EnsureValid();
            Assert.IsNotNull(back.equipment);
            Assert.AreEqual(1, EquipmentRules.GetLevel(back, EquipmentSlot.Tool));
        }

        // ---------- Pacing sanity check ----------

        [Test]
        public void ThreeMaxedAdventurers_CanBeatTheHardestPrototypeContent()
        {
            // Tank, Fighter, Support at level 50 with basic gear, default stats and power formula.
            var team = new List<Combatant>();
            foreach (MinionRole role in new[] { MinionRole.Tank, MinionRole.Fighter, MinionRole.Support })
            {
                var d = ScriptableObject.CreateInstance<MinionDefinition>();
                d.EditorSetup(role.ToString(), MinionKind.Adventure, role, Rarity.Common);
                team.Add(Combatant.ForMinion(d, new OwnedMinion { level = 50 }, database.Combat, database.Equipment));
                UnityEngine.Object.DestroyImmediate(d);
            }

            float highest = 0f;
            foreach (var e in DefaultAdventureContent.Expeditions()) highest = Math.Max(highest, e.recommendedPower);
            Assert.GreaterOrEqual(CombatSimulator.GetTeamPower(team, database.Combat), highest);
        }
    }
}
