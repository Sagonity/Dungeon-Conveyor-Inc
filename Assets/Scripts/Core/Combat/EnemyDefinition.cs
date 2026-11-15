using System;
using UnityEngine;

namespace Deepshaft.Combat
{
    /// <summary>One enemy in a boss fight or an Expedition first clear.</summary>
    [Serializable]
    public class EnemyDefinition
    {
        public string displayName = "Enemy";
        [Min(1)] public float hp = 100f;
        [Min(0)] public float atk = 10f;

        [Tooltip("Seconds between attacks.")]
        [Min(0.1f)] public float attackInterval = 2f;

        [Tooltip("Every Nth attack hits the whole team (see CombatRules.areaAttackMultiplier). 0 = never.")]
        [Min(0)] public int areaAttackEvery;

        public EnemyDefinition() { }

        public EnemyDefinition(string displayName, float hp, float atk, float attackInterval, int areaAttackEvery = 0)
        {
            this.displayName = displayName;
            this.hp = hp;
            this.atk = atk;
            this.attackInterval = attackInterval;
            this.areaAttackEvery = areaAttackEvery;
        }
    }
}
