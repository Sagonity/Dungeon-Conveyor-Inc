namespace Deepshaft.Economy
{
    /// <summary>
    /// Every stackable resource in the game (Game Design Summary v3.4 §2, §5, §6; scope doc §4.7).
    /// The numbers are stored in save files: never reorder or reuse them.
    /// Retired numbers (removed in v3.4, dropped from old saves by SaveMigrations):
    /// 1 = Upgrade Materials, 10 = Copper.
    /// </summary>
    public enum ResourceId
    {
        // From Mining. Each mine tier keeps every earlier resource and adds more (v3.4 §2).
        Gold = 0,
        Iron = 11,     // From Tier 1
        Mithril = 12,  // From Tier 5 (after the prototype's 3 tiers)
        Silver = 13,   // From Tier 3

        // Summoning shards (v3.4 §6). Duplicates turn into Minion Essence.
        BasicShard = 20,     // Common 75% / Uncommon 23% / Rare 2% (proposal)
        AdvancedShard = 21,  // Uncommon 85% / Rare 15% (proposal)
        MinionEssence = 22,  // From duplicate summons; optional Minion improvements

        // Rare Expedition materials, for breakthroughs (v3.4 §5). Placeholder names (SCRUM-13).
        SporeSilk = 30,  // Tier 1 Expeditions
        EmberCore = 31,  // Tier 2 Expeditions
        PrismDust = 32,  // Tier 3 Expeditions

        // Unique boss materials: guaranteed on a Mine Tier Boss win, unlock advanced
        // progression and Smith capabilities (v3.4 §4, §5). Placeholder names (SCRUM-13).
        ForemanSigil = 40,  // Tier 1 boss
        GolemHeart = 41,    // Tier 2 boss
        WyrmScale = 42,     // Tier 3 boss
    }
}
