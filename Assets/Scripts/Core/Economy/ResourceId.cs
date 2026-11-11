namespace Deepshaft.Economy
{
    /// <summary>
    /// Every stackable resource in the game (scope doc §4.7).
    /// The numbers are stored in save files: never reorder or reuse them.
    /// Guardian Tickets are not here because they regenerate over time; they get their own system.
    /// </summary>
    public enum ResourceId
    {
        // From Mining
        Gold = 0,
        UpgradeMaterials = 1,
        Copper = 10,   // Tier 1 mineral
        Iron = 11,     // Tier 2 mineral
        Mithril = 12,  // Tier 3 mineral

        // From Expeditions
        SummonShards = 20,
        SporeSilk = 30,  // Tier 1 rare material
        EmberCore = 31,  // Tier 2 rare material
        PrismDust = 32,  // Tier 3 rare material
    }
}
