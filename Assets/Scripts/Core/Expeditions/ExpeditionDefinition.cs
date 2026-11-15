using System;
using System.Collections.Generic;
using Deepshaft.Combat;
using Deepshaft.Economy;
using UnityEngine;

namespace Deepshaft.Expeditions
{
    /// <summary>
    /// One Expedition (Game Design Summary v3.4 §4). Listed on the GameDatabase.
    /// Unlocked by beating <see cref="unlockedByBoss"/>. The first clear is a real autobattle that
    /// can fail; once cleared, it can run automated forever at <see cref="automatedPerHour"/>.
    /// </summary>
    [Serializable]
    public class ExpeditionDefinition
    {
        [Tooltip("Stable id stored in save files, e.g. \"spore_caverns\". Never change it after release.")]
        public string id = "";
        public string displayName = "Expedition";

        [Tooltip("For display and sorting: the mine tier this Expedition belongs to.")]
        [Min(1)] public int tier = 1;

        [Tooltip("Id of the Mine Tier Boss whose first win unlocks this Expedition.")]
        public string unlockedByBoss = "";

        [Tooltip("Shown to the player for the first clear. At this team power it's usually won.")]
        [Min(0)] public float recommendedPower = 100f;

        [Tooltip("The enemies fought in the first clear.")]
        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();

        [Tooltip("One-time bonus for the first clear.")]
        public List<ResourceAmount> firstClearRewards = new List<ResourceAmount>();

        [Tooltip("What the automated Expedition earns per hour. No time cap, no diminishing returns.")]
        public List<ResourceAmount> automatedPerHour = new List<ResourceAmount>();
    }
}
