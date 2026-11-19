using System;
using Deepshaft.Equipment;
using Deepshaft.Minions;

namespace Deepshaft.Combat
{
    /// <summary>
    /// One fighter as the <see cref="CombatSimulator"/> sees it: a team Minion or an enemy,
    /// with its stats already worked out. Read-only; the simulator tracks HP itself.
    /// </summary>
    public sealed class Combatant
    {
        public Combatant(string name, MinionRole role, float maxHp, float atk, float attackInterval,
            float damageMultiplier = 1f, float damageTakenMultiplier = 1f, bool taunts = false,
            bool hitsAllEnemies = false, bool healsAllies = false, int areaAttackEvery = 0, int instanceId = 0)
        {
            Name = name ?? "";
            Role = role;
            MaxHp = Math.Max(1f, maxHp);
            Atk = Math.Max(0f, atk);
            AttackInterval = Math.Max(0.1f, attackInterval);
            DamageMultiplier = Math.Max(0f, damageMultiplier);
            DamageTakenMultiplier = Math.Max(0f, damageTakenMultiplier);
            Taunts = taunts;
            HitsAllEnemies = hitsAllEnemies;
            HealsAllies = healsAllies;
            AreaAttackEvery = Math.Max(0, areaAttackEvery);
            InstanceId = instanceId;
        }

        public string Name { get; }

        /// <summary>Adventure class. <see cref="MinionRole.None"/> for enemies.</summary>
        public MinionRole Role { get; }

        public float MaxHp { get; }
        public float Atk { get; }
        public float AttackInterval { get; }
        public float DamageMultiplier { get; }
        public float DamageTakenMultiplier { get; }
        public bool Taunts { get; }
        public bool HitsAllEnemies { get; }
        public bool HealsAllies { get; }

        /// <summary>Enemies only: every Nth attack hits the whole team. 0 = never.</summary>
        public int AreaAttackEvery { get; }

        /// <summary>The <see cref="OwnedMinion.instanceId"/> for team Minions, 0 for enemies.</summary>
        public int InstanceId { get; }

        /// <summary>
        /// A team Minion: stats from its definition, level and gear (weapon → ATK, armor → HP),
        /// behaviour from its class. Without <paramref name="equipment"/> rules, gear is ignored.
        /// </summary>
        public static Combatant ForMinion(MinionDefinition definition, OwnedMinion minion, CombatRules rules,
            EquipmentRules equipment = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (minion == null) throw new ArgumentNullException(nameof(minion));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            RoleCombatStats role = rules.GetRole(definition.Role);
            return new Combatant(definition.DisplayName, definition.Role,
                MinionStatCalculator.GetHp(definition, minion, minion.level, equipment),
                MinionStatCalculator.GetAtk(definition, minion, minion.level, equipment), role.attackInterval,
                role.damageMultiplier, role.damageTakenMultiplier, role.taunts, role.hitsAllEnemies,
                role.healsAllies, 0, minion.instanceId);
        }

        public static Combatant ForEnemy(EnemyDefinition enemy)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            return new Combatant(enemy.displayName, MinionRole.None, enemy.hp, enemy.atk, enemy.attackInterval,
                areaAttackEvery: enemy.areaAttackEvery);
        }
    }
}
