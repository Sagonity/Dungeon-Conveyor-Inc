using System;

namespace Deepshaft.Equipment
{
    /// <summary>
    /// One piece of gear on one Minion. Plain save data, stored on <see cref="Minions.OwnedMinion.equipment"/>.
    /// Gear has fixed stats (no random rolls): only its level and breakthroughs change, at the Smith (SCRUM-9).
    /// A Minion with no entry for a slot has that slot's basic level-1 item.
    /// </summary>
    [Serializable]
    public class OwnedEquipment
    {
        public EquipmentSlot slot;
        public int level = 1;

        /// <summary>Breakthroughs done on this item. Each one raises its level cap.</summary>
        public int breakthroughs;
    }
}
