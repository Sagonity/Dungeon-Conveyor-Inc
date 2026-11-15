using System;
using System.Collections.Generic;
using Deepshaft.Economy;
using UnityEngine;

namespace Deepshaft.Progression
{
    /// <summary>
    /// What unlocks one mine tier (Game Design Summary v3.4 §8: exact prerequisites are still
    /// open, SCRUM-2). Data on the GameDatabase so SCRUM-2 can change it without code.
    /// A tier with no row can't be unlocked: that's the end of the content (tier 3 in the prototype).
    /// </summary>
    [Serializable]
    public class TierRequirement
    {
        [Tooltip("The tier this row unlocks. Tier 1 is always unlocked.")]
        [Min(2)] public int tier = 2;

        [Tooltip("Ids of bosses that must be beaten.")]
        public List<string> requiredBosses = new List<string>();

        [Tooltip("Ids of Expeditions whose first clear must be done.")]
        public List<string> requiredFirstClears = new List<string>();

        [Tooltip("Paid when the player unlocks the tier. Empty = the tier unlocks by itself once the rest is met.")]
        public List<ResourceAmount> cost = new List<ResourceAmount>();
    }
}
