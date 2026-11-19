using System;
using System.Collections.Generic;
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
    /// Gear numbers (Scope §4.5), on the GameDatabase asset so they can be tuned in the Inspector.
    /// SCRUM-10 builds the slots and their effect on stats; upgrade costs come with the Smith (SCRUM-9).
    /// Numbers are a first pass until SCRUM-2 / SCRUM-19.
    /// </summary>
    [Serializable]
    public class EquipmentRules
    {
        public List<EquipmentSlotRules> slots = new List<EquipmentSlotRules>();

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
        };

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
