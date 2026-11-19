using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Save;
using Deepshaft.Session;
using Deepshaft.Smith;
using Deepshaft.State;
using Deepshaft.Time;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>
    /// The Smith and HQ production (SCRUM-9), with the default <see cref="EquipmentRules"/>:
    /// gear cap 4, +4 per breakthrough; Smith ranks taught by Foreman Sigil, Golem Heart, Wyrm Scale.
    /// </summary>
    public class SmithTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        ManualClock clock;
        MinionDefinition miner;
        MinionDefinition fighter;
        GameDatabase database;
        GameState state;
        Wallet wallet;
        MiningService mining;
        SmithService smith;

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
            smith = new SmithService(state, database, mining);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(miner);
            UnityEngine.Object.DestroyImmediate(fighter);
            UnityEngine.Object.DestroyImmediate(database);
        }

        void UpgradeTo(OwnedMinion m, EquipmentSlot slot, int level)
        {
            while (EquipmentRules.GetLevel(m, slot) < level)
            {
                wallet.Add(smith.CheckUpgrade(m.instanceId, slot).Cost);
                Assert.AreEqual(SmithStatus.Ok, smith.TryUpgrade(m.instanceId, slot, wallet).Status);
            }
        }

        static bool Costs(SmithCheck check, ResourceId id)
        {
            foreach (ResourceAmount c in check.Cost) if (c.id == id && c.amount > 0) return true;
            return false;
        }

        // ---------- Ordinary upgrades ----------

        [Test]
        public void PickaxeUpgrade_CostsGoldAndIron_AndRaisesMining()
        {
            OwnedMinion m = mining.AddMinion("miner");
            SmithCheck check = smith.CheckUpgrade(m.instanceId, EquipmentSlot.Tool, wallet);
            CollectionAssert.AreEquivalent(new[] { ResourceId.Gold, ResourceId.Iron }, new List<ResourceAmount>(check.Cost).ConvertAll(c => c.id));
            Assert.AreEqual(SmithStatus.CantAfford, check.Status);

            wallet.Add(check.Cost);
            SmithCheck result = smith.TryUpgrade(m.instanceId, EquipmentSlot.Tool, wallet);

            Assert.AreEqual(SmithStatus.Ok, result.Status);
            Assert.AreEqual(2, result.Level);
            Assert.AreEqual(0, wallet.Get(ResourceId.Gold));
            Assert.AreEqual(110.0, MiningMath.Get(mining.GetHourlyRates(), ResourceId.Gold), 1e-4, "+10% from the level-2 pickaxe.");
        }

        [Test]
        public void ToolUpgrade_BanksTheOldRateFirst()
        {
            OwnedMinion m = mining.AddMinion("miner");
            clock.Advance(TimeSpan.FromHours(1));
            UpgradeTo(m, EquipmentSlot.Tool, 2);
            clock.Advance(TimeSpan.FromHours(1));

            Assert.AreEqual(210.0, mining.GetPending(ResourceId.Gold), 1e-4);
        }

        [Test]
        public void Upgrades_StopAtTheCap_OfFour()
        {
            OwnedMinion m = state.AddMinion("fighter");
            UpgradeTo(m, EquipmentSlot.Weapon, 4);
            wallet.Add(ResourceId.Gold, 100000);
            wallet.Add(ResourceId.Iron, 100000);

            SmithCheck check = smith.TryUpgrade(m.instanceId, EquipmentSlot.Weapon, wallet);

            Assert.AreEqual(SmithStatus.AtLevelCap, check.Status);
            Assert.AreEqual(4, EquipmentRules.GetLevel(m, EquipmentSlot.Weapon));
            Assert.AreEqual(1, EquipmentRules.GetLevel(m, EquipmentSlot.Armor), "Each item levels on its own.");
        }

        [Test]
        public void WrongSlot_AndUnknownMinion_AreRefused()
        {
            OwnedMinion m = mining.AddMinion("miner");
            Assert.AreEqual(SmithStatus.WrongSlot, smith.CheckUpgrade(m.instanceId, EquipmentSlot.Weapon).Status);
            Assert.AreEqual(SmithStatus.NotFound, smith.CheckUpgrade(999, EquipmentSlot.Tool).Status);
        }

        [Test]
        public void HighLevels_AlsoCostSilver()
        {
            EquipmentRules rules = EquipmentRules.CreateDefault();
            Assert.IsFalse(rules.GetLevelUpCost(8).Exists(c => c.id == ResourceId.Silver));
            Assert.IsTrue(rules.GetLevelUpCost(9).Exists(c => c.id == ResourceId.Silver));
        }

        // ---------- Smith ranks (boss materials) ----------

        [Test]
        public void SmithRanks_AreLearnedOnce_WithEachBossMaterial_InOrder()
        {
            var materials = new[] { ResourceId.ForemanSigil, ResourceId.GolemHeart, ResourceId.WyrmScale };
            Assert.AreEqual(0, smith.Rank);
            Assert.AreEqual(3, smith.MaxRank);

            for (int i = 0; i < materials.Length; i++)
            {
                SmithCheck check = smith.CheckRankUp(wallet);
                Assert.AreEqual(SmithStatus.CantAfford, check.Status);
                Assert.IsTrue(Costs(check, materials[i]));

                wallet.Add(materials[i], 1);
                Assert.AreEqual(SmithStatus.Ok, smith.TryRankUp(wallet).Status);
                Assert.AreEqual(i + 1, smith.Rank);
                Assert.AreEqual(0, wallet.Get(materials[i]), "The boss material is used up.");
            }

            Assert.AreEqual(SmithStatus.NoMore, smith.CheckRankUp(wallet).Status);
        }

        // ---------- Gear breakthroughs ----------

        [Test]
        public void GearBreakthrough_NeedsTheCap_TheSmithRank_AndTheExpeditionMaterial()
        {
            OwnedMinion m = mining.AddMinion("miner");

            Assert.AreEqual(SmithStatus.BelowLevelCap, smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Tool).Status);

            UpgradeTo(m, EquipmentSlot.Tool, 4);
            SmithCheck noRank = smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Tool, wallet);
            Assert.AreEqual(SmithStatus.SmithRankTooLow, noRank.Status);
            Assert.AreEqual(1, noRank.RequiredRank);
            Assert.IsTrue(Costs(noRank, ResourceId.SporeSilk), "Breakthrough 1 uses the Tier 1 Expedition material.");

            wallet.Add(ResourceId.ForemanSigil, 1);
            smith.TryRankUp(wallet);
            Assert.AreEqual(SmithStatus.CantAfford, smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Tool, wallet).Status);

            wallet.Add(noRank.Cost);
            SmithCheck done = smith.TryBreakthrough(m.instanceId, EquipmentSlot.Tool, wallet);

            Assert.AreEqual(SmithStatus.Ok, done.Status);
            Assert.AreEqual(8, done.MaxLevel, "Cap 4 → 8.");
            UpgradeTo(m, EquipmentSlot.Tool, 5);
            Assert.AreEqual(5, EquipmentRules.GetLevel(m, EquipmentSlot.Tool), "Pickaxe 4 → 5 after the breakthrough (v3.4 §5).");
        }

        [Test]
        public void SecondGearBreakthrough_NeedsRankTwo_AndEmberCore()
        {
            OwnedMinion m = state.AddMinion("fighter");
            wallet.Add(ResourceId.ForemanSigil, 1);
            smith.TryRankUp(wallet);
            UpgradeTo(m, EquipmentSlot.Armor, 4);
            wallet.Add(smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Armor).Cost);
            smith.TryBreakthrough(m.instanceId, EquipmentSlot.Armor, wallet);
            UpgradeTo(m, EquipmentSlot.Armor, 8);

            SmithCheck check = smith.CheckBreakthrough(m.instanceId, EquipmentSlot.Armor, wallet);

            Assert.AreEqual(SmithStatus.SmithRankTooLow, check.Status);
            Assert.AreEqual(2, check.RequiredRank);
            Assert.IsTrue(Costs(check, ResourceId.EmberCore));
        }

        // ---------- Saving ----------

        [Test]
        public void SmithRank_IsSaved_AndOldSavesStartAtZero()
        {
            var saves = new SaveService(new MemorySaveStorage(), clock);
            wallet.Add(ResourceId.ForemanSigil, 1);
            smith.TryRankUp(wallet);
            saves.Save(state);

            GameState loaded = saves.Load().State;
            Assert.AreEqual(1, loaded.smith.rank);

            loaded.smith = null; // a save from before SCRUM-9
            loaded.EnsureValid();
            Assert.AreEqual(0, loaded.smith.rank);
        }

        // ---------- Where to get it ----------

        [Test]
        public void ResourceSources_PointToTheMine_TheExpedition_OrTheBoss()
        {
            database.EditorSetMining(MiningRules.CreateDefault());

            ResourceSource iron = ResourceSources.Find(database, ResourceId.Iron);
            ResourceSource silver = ResourceSources.Find(database, ResourceId.Silver);
            ResourceSource silk = ResourceSources.Find(database, ResourceId.SporeSilk);
            ResourceSource heart = ResourceSources.Find(database, ResourceId.GolemHeart);

            Assert.AreEqual(ResourceSourceKind.Mine, iron.Kind);
            Assert.AreEqual(1, iron.Tier);
            Assert.AreEqual(3, silver.Tier, "Silver is first mined at Tier 3.");
            Assert.AreEqual(ResourceSourceKind.AutomatedExpedition, silk.Kind);
            Assert.AreEqual(DefaultAdventureContent.Tier1Expedition, silk.Id);
            Assert.AreEqual(ResourceSourceKind.Boss, heart.Kind);
            Assert.AreEqual(DefaultAdventureContent.Tier2Boss, heart.Id);
        }
    }
}
