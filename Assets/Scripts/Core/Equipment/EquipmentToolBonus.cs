using System;
using Deepshaft.Minions;
using Deepshaft.Mining;

namespace Deepshaft.Equipment
{
    /// <summary>Feeds each Miner's own tool into mining (<see cref="MinerInput.ToolBonus"/>).</summary>
    public sealed class EquipmentToolBonus : IMinerToolBonus
    {
        readonly Func<EquipmentRules> rules;

        public EquipmentToolBonus(Func<EquipmentRules> rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public float GetToolBonus(OwnedMinion miner) => rules()?.GetBonus(miner, EquipmentSlot.Tool) ?? 0f;
    }
}
