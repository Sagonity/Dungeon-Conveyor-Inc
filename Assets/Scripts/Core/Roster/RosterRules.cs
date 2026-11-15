using System;
using UnityEngine;

namespace Deepshaft.Roster
{
    /// <summary>
    /// How many Minions work at the same time (Game Design Summary v3.4 §8: still open).
    /// Lives on the GameDatabase asset so it can change after the playtest (SCRUM-8)
    /// without code changes. The defaults are placeholders.
    /// </summary>
    [Serializable]
    public class RosterRules
    {
        [Tooltip("How many Miners can work in the Mine at the same time. Placeholder until the playtest.")]
        [Min(1)] public int minerSlots = 3;

        [Tooltip("How many Adventure Minions fight in the team at the same time. Placeholder until the playtest.")]
        [Min(1)] public int adventureSlots = 3;

        public static RosterRules CreateDefault() => new RosterRules();

        public int MinerSlots => Math.Max(1, minerSlots);
        public int AdventureSlots => Math.Max(1, adventureSlots);
    }
}
