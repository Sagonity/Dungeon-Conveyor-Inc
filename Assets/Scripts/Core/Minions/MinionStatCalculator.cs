using System;
using Deepshaft.Content;
using Deepshaft.Equipment;

namespace Deepshaft.Minions
{
    /// <summary>
    /// A Minion's stats with its level and gear, in one place, so the Minion screen, combat and
    /// mining all show and use the same numbers.
    /// </summary>
    public static class MinionStatCalculator
    {
        /// <summary>Mining power from level only. The tool is applied as <see cref="Mining.MinerInput.ToolBonus"/>.</summary>
        public static float GetMiningPower(MinionDefinition definition, int level) =>
            definition != null ? definition.GetMiningPower(level) : 0f;

        /// <summary>Extra mining output from the Miner's tool, e.g. 0.1 = +10%.</summary>
        public static float GetToolBonus(OwnedMinion minion, EquipmentRules equipment) =>
            equipment?.GetBonus(minion, EquipmentSlot.Tool) ?? 0f;

        /// <summary>HP at <paramref name="level"/> with the Minion's armor.</summary>
        public static float GetHp(MinionDefinition definition, OwnedMinion minion, int level, EquipmentRules equipment)
        {
            if (definition == null) return 0f;
            return definition.GetHp(level) * (1f + (equipment?.GetBonus(minion, EquipmentSlot.Armor) ?? 0f));
        }

        /// <summary>ATK at <paramref name="level"/> with the Minion's weapon.</summary>
        public static float GetAtk(MinionDefinition definition, OwnedMinion minion, int level, EquipmentRules equipment)
        {
            if (definition == null) return 0f;
            return definition.GetAtk(level) * (1f + (equipment?.GetBonus(minion, EquipmentSlot.Weapon) ?? 0f));
        }

        public static float GetHp(GameDatabase database, OwnedMinion minion) =>
            GetHp(Find(database, minion), minion, minion.level, database.Equipment);

        public static float GetAtk(GameDatabase database, OwnedMinion minion) =>
            GetAtk(Find(database, minion), minion, minion.level, database.Equipment);

        static MinionDefinition Find(GameDatabase database, OwnedMinion minion)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (minion == null) throw new ArgumentNullException(nameof(minion));
            return database.FindMinion(minion.definitionId);
        }
    }
}
