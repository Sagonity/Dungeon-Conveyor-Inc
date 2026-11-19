using System;
using System.Collections.Generic;
using Deepshaft.Economy;
using Deepshaft.Minions;
using UnityEngine;

namespace Deepshaft.Equipment
{
    /// <summary>The numbers for one gear slot.</summary>
    [Serializable]
    public class EquipmentSlotRules
    {
        public EquipmentSlot slot;
        [Tooltip("Shown in the game, e.g. \"Pickaxe\". Placeholder names until SCRUM-13.")]
        public string displayName = "";

        [Tooltip("Bonus for each level above 1. Tool: extra mining output; Weapon: extra ATK; Armor: extra HP. 0.1 = +10%.")]
        [Min(0)] public float bonusPerLevel = 0.1f;

        [Tooltip("Highest level before the first breakthrough. Scope §4.5: levels 1 → 4, then a breakthrough.")]
        [Min(1)] public int maxLevelBeforeBreakthrough = 4;
        [Tooltip("Each breakthrough raises the level cap by this much.")]
        [Min(0)] public int maxLevelPerBreakthrough = 4;

        public EquipmentSlotRules() { }

        public EquipmentSlotRules(EquipmentSlot slot, string displayName, float bonusPerLevel)
        {
            this.slot = slot;
            this.displayName = displayName;
            this.bonusPerLevel = bonusPerLevel;
        }

        /// <summary>Bonus of an item at <paramref name="level"/>: 0 at level 1.</summary>
        public float GetBonus(int level) => bonusPerLevel * (Math.Max(1, level) - 1);

        public int GetMaxLevel(int breakthroughs) =>
            maxLevelBeforeBreakthrough + Math.Max(0, breakthroughs) * maxLevelPerBreakthrough;
    }

    /// <summary>
    /// One Smith rank (SCRUM-9; team decision 2026-11-19): the Smith learns it once, paid with a
    /// unique boss material, and from then on can do that gear breakthrough (rank 1 → gear
    /// breakthrough 1, and so on).
    /// </summary>
    [Serializable]
    public class SmithRank
    {
        [Tooltip("Shown in the game, e.g. \"Sigil Forging\".")]
        public string displayName = "";
        public List<ResourceAmount> cost = new List<ResourceAmount>();

        public SmithRank() { }

        public SmithRank(string displayName, params ResourceAmount[] cost)
        {
            this.displayName = displayName;
            this.cost = new List<ResourceAmount>(cost);
        }
    }

    /// <summary>
    /// Gear and Smith numbers (Scope §4.5), on the GameDatabase asset so they can be tuned in the
    /// Inspector. First pass until SCRUM-2 / SCRUM-19.
    ///
    /// - Slots and their bonuses (SCRUM-10).
    /// - Upgrades (SCRUM-9): one level-cost table for every slot, Gold + Iron, Silver from level 9.
    /// - Gear breakthroughs at the cap: Gold + ores + that tier's Expedition material (Spore Silk,
    ///   Ember Core, Prism Dust), and the Smith must know the matching rank.
    /// - Smith ranks: learned once with a unique boss material (Foreman Sigil, Golem Heart, Wyrm Scale).
    /// </summary>
    [Serializable]
    public class EquipmentRules
    {
        public List<EquipmentSlotRules> slots = new List<EquipmentSlotRules>();

        [Tooltip("Cost of one gear level at the Smith, from the item's current level.")]
        public List<LevelCostTerm> levelCost = new List<LevelCostTerm>();
        [Tooltip("Gear breakthroughs in order. The number of rows is how many an item can have.")]
        public List<BreakthroughCost> breakthroughs = new List<BreakthroughCost>();
        [Tooltip("Smith ranks in order. Rank N is needed for gear breakthrough N.")]
        public List<SmithRank> smithRanks = new List<SmithRank>();

        static readonly EquipmentSlot[] MinerSlots = { EquipmentSlot.Tool };
        static readonly EquipmentSlot[] AdventureSlots = { EquipmentSlot.Weapon, EquipmentSlot.Armor };

        public static EquipmentRules CreateDefault() => new EquipmentRules
        {
            slots = new List<EquipmentSlotRules>
            {
                new EquipmentSlotRules(EquipmentSlot.Tool, "Pickaxe", 0.10f),
                new EquipmentSlotRules(EquipmentSlot.Weapon, "Weapon", 0.08f),
                new EquipmentSlotRules(EquipmentSlot.Armor, "Armor", 0.08f),
            },
            levelCost = new List<LevelCostTerm>
            {
                new LevelCostTerm(ResourceId.Gold, 40f, 1.25f),
                new LevelCostTerm(ResourceId.Iron, 20f, 1.25f),
                new LevelCostTerm(ResourceId.Silver, 10f, 1.25f, 9),
            },
            breakthroughs = new List<BreakthroughCost>
            {
                new BreakthroughCost(R(ResourceId.Gold, 300), R(ResourceId.Iron, 150), R(ResourceId.SporeSilk, 3)),
                new BreakthroughCost(R(ResourceId.Gold, 900), R(ResourceId.Iron, 450), R(ResourceId.EmberCore, 3)),
                new BreakthroughCost(R(ResourceId.Gold, 2500), R(ResourceId.Iron, 1200), R(ResourceId.Silver, 150), R(ResourceId.PrismDust, 3)),
            },
            smithRanks = new List<SmithRank>
            {
                new SmithRank("Sigil Forging", R(ResourceId.ForemanSigil, 1)),
                new SmithRank("Golem Tempering", R(ResourceId.GolemHeart, 1)),
                new SmithRank("Wyrmscale Smithing", R(ResourceId.WyrmScale, 1)),
            },
        };

        static ResourceAmount R(ResourceId id, long amount) => new ResourceAmount(id, amount);

        public int MaxBreakthroughs => breakthroughs?.Count ?? 0;
        public int MaxSmithRank => smithRanks?.Count ?? 0;

        /// <summary>Cost of one gear level from <paramref name="currentLevel"/>.</summary>
        public List<ResourceAmount> GetLevelUpCost(int currentLevel) => CostTables.LevelCost(levelCost, currentLevel);

        /// <summary>Cost of the gear breakthrough after <paramref name="done"/>, or null if there are no more.</summary>
        public List<ResourceAmount> GetBreakthroughCost(int done) => CostTables.Breakthrough(breakthroughs, done);

        /// <summary>The Smith rank learned as rank <paramref name="rank"/> (1-based), or null.</summary>
        public SmithRank GetSmithRank(int rank) =>
            smithRanks != null && rank >= 1 && rank <= smithRanks.Count ? smithRanks[rank - 1] : null;

        public static int GetBreakthroughs(OwnedMinion minion, EquipmentSlot slot) =>
            Math.Max(0, Find(minion, slot)?.breakthroughs ?? 0);

        /// <summary>The item's level cap with the breakthroughs it has.</summary>
        public int GetMaxLevel(OwnedMinion minion, EquipmentSlot slot) =>
            Get(slot).GetMaxLevel(GetBreakthroughs(minion, slot));

        /// <summary>The slots a Minion of this kind has: a tool for Miners, weapon and armor for Adventurers.</summary>
        public static IReadOnlyList<EquipmentSlot> SlotsFor(MinionKind kind) =>
            kind == MinionKind.Miner ? MinerSlots : AdventureSlots;

        /// <summary>The rules for a slot. Never null: a missing row gives no bonus.</summary>
        public EquipmentSlotRules Get(EquipmentSlot slot)
        {
            if (slots != null)
            {
                foreach (EquipmentSlotRules r in slots) if (r != null && r.slot == slot) return r;
            }
            return new EquipmentSlotRules(slot, slot.ToString(), 0f);
        }

        /// <summary>The item in <paramref name="slot"/>, or null if the Minion still has the basic level-1 one.</summary>
        public static OwnedEquipment Find(OwnedMinion minion, EquipmentSlot slot)
        {
            if (minion?.equipment == null) return null;
            foreach (OwnedEquipment e in minion.equipment) if (e != null && e.slot == slot) return e;
            return null;
        }

        /// <summary>The item in <paramref name="slot"/>, added at level 1 if the Minion had none yet.</summary>
        public static OwnedEquipment GetOrAdd(OwnedMinion minion, EquipmentSlot slot)
        {
            if (minion == null) throw new ArgumentNullException(nameof(minion));
            OwnedEquipment found = Find(minion, slot);
            if (found != null) return found;
            if (minion.equipment == null) minion.equipment = new List<OwnedEquipment>();
            var item = new OwnedEquipment { slot = slot, level = 1, breakthroughs = 0 };
            minion.equipment.Add(item);
            return item;
        }

        public static int GetLevel(OwnedMinion minion, EquipmentSlot slot) => Math.Max(1, Find(minion, slot)?.level ?? 1);

        /// <summary>The Minion's bonus from its gear in <paramref name="slot"/>, e.g. 0.2 = +20%.</summary>
        public float GetBonus(OwnedMinion minion, EquipmentSlot slot) => Get(slot).GetBonus(GetLevel(minion, slot));
    }
}
