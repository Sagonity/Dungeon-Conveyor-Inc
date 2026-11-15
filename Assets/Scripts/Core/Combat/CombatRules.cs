using System;
using System.Collections.Generic;
using Deepshaft.Minions;
using UnityEngine;

namespace Deepshaft.Combat
{
    /// <summary>How one Adventure class fights (Game Design Summary v3.4 §3).</summary>
    [Serializable]
    public class RoleCombatStats
    {
        public MinionRole role = MinionRole.Fighter;

        [Tooltip("Seconds between actions.")]
        [Min(0.1f)] public float attackInterval = 1f;

        [Tooltip("Damage per hit = ATK × this.")]
        [Min(0)] public float damageMultiplier = 1f;

        [Tooltip("Damage this Minion takes × this. 0.75 = takes 25% less.")]
        [Min(0)] public float damageTakenMultiplier = 1f;

        [Tooltip("Enemies attack this Minion first while it's standing.")]
        public bool taunts;

        [Tooltip("Each attack hits every enemy instead of the front one.")]
        public bool hitsAllEnemies;

        [Tooltip("Heals the most hurt ally instead of attacking when someone is hurt.")]
        public bool healsAllies;

        public RoleCombatStats() { }

        public RoleCombatStats(MinionRole role, float attackInterval, float damageMultiplier,
            float damageTakenMultiplier = 1f, bool taunts = false, bool hitsAllEnemies = false, bool healsAllies = false)
        {
            this.role = role;
            this.attackInterval = attackInterval;
            this.damageMultiplier = damageMultiplier;
            this.damageTakenMultiplier = damageTakenMultiplier;
            this.taunts = taunts;
            this.hitsAllEnemies = hitsAllEnemies;
            this.healsAllies = healsAllies;
        }
    }

    /// <summary>
    /// Autobattle numbers for boss fights and Expedition first clears (SCRUM-7). Lives on the
    /// GameDatabase asset so it can be tuned in the Inspector. The defaults are a first pass,
    /// tuned so a team at the recommended power usually wins and a team 15% below usually loses;
    /// final numbers come from SCRUM-2 / SCRUM-19.
    ///
    /// Team power (the number shown next to "Rec.") =
    ///   Σ team (ATK + HP ÷ <see cref="powerHpDivisor"/>) × (1 + <see cref="supportTeamBonus"/> if the team has a Support).
    /// </summary>
    [Serializable]
    public class CombatRules
    {
        [Tooltip("A fight that isn't won by then is lost. Bosses are tuned to take about 30–50 s.")]
        [Min(1)] public float timeLimitSeconds = 60f;

        [Tooltip("Each hit is randomly up to this much stronger or weaker. 0.1 = ±10%.")]
        [Range(0, 0.5f)] public float damageVariance = 0.1f;

        [Tooltip("A team with a Support deals this much more damage, and its power counts this much more.")]
        [Min(0)] public float supportTeamBonus = 0.1f;

        [Tooltip("A heal restores the healer's ATK × this.")]
        [Min(0)] public float healMultiplier = 0.9f;

        [Tooltip("Healers only heal allies below this share of their max HP.")]
        [Range(0, 1)] public float healBelowHpFraction = 0.8f;

        [Tooltip("An enemy's area attack hits every Minion for ATK × this.")]
        [Min(0)] public float areaAttackMultiplier = 0.5f;

        [Tooltip("Team power counts HP ÷ this.")]
        [Min(0.01f)] public float powerHpDivisor = 10f;

        public List<RoleCombatStats> roles = new List<RoleCombatStats>();

        static readonly RoleCombatStats Fallback = new RoleCombatStats(MinionRole.Fighter, 1f, 1f);

        public static CombatRules CreateDefault() => new CombatRules
        {
            roles = new List<RoleCombatStats>
            {
                new RoleCombatStats(MinionRole.Fighter, 1.0f, 1.0f),
                new RoleCombatStats(MinionRole.Tank, 1.0f, 0.7f, damageTakenMultiplier: 0.75f, taunts: true),
                new RoleCombatStats(MinionRole.Archer, 0.8f, 0.85f),
                new RoleCombatStats(MinionRole.Mage, 1.25f, 0.8f, hitsAllEnemies: true),
                new RoleCombatStats(MinionRole.Support, 1.25f, 0.7f, healsAllies: true),
            },
        };

        /// <summary>The stats for <paramref name="role"/>, or plain Fighter stats if it has no row.</summary>
        public RoleCombatStats GetRole(MinionRole role)
        {
            if (roles != null)
            {
                foreach (RoleCombatStats r in roles)
                {
                    if (r != null && r.role == role) return r;
                }
            }
            return Fallback;
        }

        /// <summary>The power one Minion adds to the team, before the Support bonus.</summary>
        public float GetPower(float hp, float atk) => atk + hp / Math.Max(0.01f, powerHpDivisor);
    }
}
