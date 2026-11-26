using System;
using System.Collections.Generic;
using Deepshaft.Combat;
using Deepshaft.Economy;
using UnityEngine;

namespace Deepshaft.Bosses
{
    /// <summary>
    /// One Mine Tier Boss (Game Design Summary v3.4 §4). One per tier; listed on the GameDatabase.
    /// Beating it guarantees its unique material and unlocks the Expeditions whose
    /// <see cref="Expeditions.ExpeditionDefinition.unlockedByBoss"/> is this id.
    /// </summary>
    [Serializable]
    public class BossDefinition
    {
        [Tooltip("Stable id stored in save files, e.g. \"spore_foreman\". Never change it after release.")]
        public string id = "";
        public string displayName = "Boss";

        [Tooltip("The mine tier this boss guards. It can be fought once this tier is unlocked.")]
        [Min(1)] public int tier = 1;

        [Tooltip("Shown to the player. At this team power the fight is usually won.")]
        [Min(0)] public float recommendedPower = 100f;

        public EnemyDefinition enemy = new EnemyDefinition();

        [Tooltip("Given on the first win: the unique boss material plus any one-time bonus.")]
        public List<ResourceAmount> firstWinRewards = new List<ResourceAmount>();

        [Tooltip("Given on every later win. Attempts are free and unlimited, so keep this small (or empty).")]
        public List<ResourceAmount> repeatWinRewards = new List<ResourceAmount>();

        [Header("Art (SCRUM-37)")]
        [Tooltip("Sprite sheet in a Resources folder, without the extension, e.g. \"Art/Bosses/vein_warden_sheet\". " +
                 "Frames left to right, top to bottom, on an even grid. Empty: an animated placeholder.")]
        public string spriteSheet = "";
        [Min(1)] public int sheetColumns = 7;
        [Min(1)] public int sheetRows = 2;
        [Tooltip("Frames used, counted from the top left. Can be fewer than columns × rows.")]
        [Min(1)] public int sheetFrames = 14;
        [Min(1)] public float sheetFramesPerSecond = 10f;
    }
}
