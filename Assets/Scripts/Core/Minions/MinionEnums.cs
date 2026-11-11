namespace Deepshaft.Minions
{
    // The numbers are stored in save files and assets: never reorder or reuse them.

    /// <summary>Which job a Minion does (scope doc §4.3). The two never overlap.</summary>
    public enum MinionKind
    {
        Miner = 0,      // Works the Mine only
        Adventure = 1,  // Expeditions and Boss Battles only
    }

    /// <summary>Squad role for Adventure Minions. Miners use <see cref="None"/>.</summary>
    public enum MinionRole
    {
        None = 0,
        Fighter = 1,
        Tank = 2,
        Support = 3,
    }

    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
    }
}
