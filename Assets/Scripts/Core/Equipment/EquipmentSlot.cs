namespace Deepshaft.Equipment
{
    /// <summary>
    /// The gear slots a Minion has (Scope §4.5): Miners carry a tool, Adventure Minions a weapon
    /// and armor. Every Minion has its own gear (team decision 2026-11-19).
    /// The numbers are stored in save files: never reorder or reuse them.
    /// </summary>
    public enum EquipmentSlot
    {
        Tool = 0,    // Miners: more mining output
        Weapon = 1,  // Adventure Minions: more ATK
        Armor = 2,   // Adventure Minions: more HP
    }
}
