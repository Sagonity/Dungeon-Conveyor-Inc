namespace Deepshaft.Minions
{
    // The numbers are stored in save files and assets: never reorder or reuse them.

    /// <summary>
    /// Which job a Minion does (Game Design Summary v3.4 §3). The two never overlap.
    /// It is also the summoning pool: the player chooses Miner or Adventure when summoning (v3.4 §6).
    /// </summary>
    public enum MinionKind
    {
        Miner = 0,      // Resource production in the Mine only
        Adventure = 1,  // Expeditions and boss fights only
    }

    /// <summary>Combat class for Adventure Minions (v3.4 §3). Miners use <see cref="None"/>.</summary>
    public enum MinionRole
    {
        None = 0,
        Fighter = 1,
        Tank = 2,
        Support = 3,
        Archer = 4,
        Mage = 5,
    }

    /// <summary>
    /// Fixed specialization of a named Miner Minion (v3.4 §3, §6). Adventure Minions use <see cref="None"/>.
    /// </summary>
    public enum MinerTrait
    {
        None = 0,
        Gold = 1,   // Extra Gold yield
        Ore = 2,    // Extra ore yield (Iron, Silver, Mithril)
        Speed = 3,  // Faster mining overall
    }

    /// <summary>
    /// Prototype rarities (v3.4 §6). Epic/Legendary may be added later with new numbers.
    /// Renumbered in v3.4 (was Common/Rare/Epic) before any Minion assets existed.
    /// </summary>
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
    }
}
