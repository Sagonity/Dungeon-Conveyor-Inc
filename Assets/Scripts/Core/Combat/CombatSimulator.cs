using System;
using System.Collections.Generic;
using Deepshaft.Minions;

namespace Deepshaft.Combat
{
    /// <summary>
    /// The autobattle (Game Design Summary v3.4 §4, Scope §4.2–4.3). Plain C#, no clock and
    /// no UnityEngine.Random: the same team, enemies and seed always give the same fight, so
    /// it's easy to test (Technical Architecture §7).
    ///
    /// Rules:
    /// - Everyone acts on their own timer (attack interval). The first action is one interval in.
    /// - Team Minions hit the front enemy that's still standing. Mages hit every enemy.
    ///   Supports heal the most hurt ally (below <see cref="CombatRules.healBelowHpFraction"/>)
    ///   instead of attacking. A team with a Support deals <see cref="CombatRules.supportTeamBonus"/> more damage.
    /// - Enemies hit a standing Tank first, otherwise a random team Minion. Some enemies hit
    ///   the whole team every Nth attack.
    /// - The fight ends when one side is knocked out, or at the time limit (a loss).
    /// </summary>
    public static class CombatSimulator
    {
        sealed class Unit
        {
            public Combatant stats;
            public float hp;
            public double nextActionAt;
            public int attacks;
            public bool Alive => hp > 0;
        }

        public static BattleResult Simulate(CombatRules rules, IReadOnlyList<Combatant> team,
            IReadOnlyList<Combatant> enemies, float recommendedPower, Random rng)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (team == null) throw new ArgumentNullException(nameof(team));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var teamCopy = new List<Combatant>(team);
            var enemyCopy = new List<Combatant>(enemies);
            List<Unit> us = MakeUnits(teamCopy);
            List<Unit> them = MakeUnits(enemyCopy);
            var events = new List<BattleEvent>();
            float teamPower = GetTeamPower(teamCopy, rules);
            float damageBonus = HasSupport(teamCopy) ? 1f + rules.supportTeamBonus : 1f;
            float limit = Math.Max(1f, rules.timeLimitSeconds);

            void TeamAction(Unit actor, int actorIndex, float now)
            {
                if (actor.stats.HealsAllies)
                {
                    int hurt = MostHurt(us, rules.healBelowHpFraction);
                    if (hurt >= 0)
                    {
                        Unit ally = us[hurt];
                        float heal = actor.stats.Atk * rules.healMultiplier * Roll();
                        heal = Math.Min(heal, ally.stats.MaxHp - ally.hp);
                        ally.hp += heal;
                        events.Add(new BattleEvent(now, BattleEventType.Heal, BattleSide.Team, actorIndex,
                            BattleSide.Team, hurt, heal, ally.hp));
                        return;
                    }
                }

                float baseDamage = actor.stats.Atk * actor.stats.DamageMultiplier * damageBonus;
                for (int i = 0; i < them.Count; i++)
                {
                    if (!them[i].Alive) continue;
                    Hit(BattleEventType.Attack, BattleSide.Team, actorIndex, BattleSide.Enemies, them, i,
                        baseDamage * Roll(), now);
                    if (!actor.stats.HitsAllEnemies) break;
                }
            }

            void EnemyAction(Unit actor, int actorIndex, float now)
            {
                actor.attacks++;
                int every = actor.stats.AreaAttackEvery;
                if (every > 0 && actor.attacks % every == 0)
                {
                    float areaDamage = actor.stats.Atk * actor.stats.DamageMultiplier * rules.areaAttackMultiplier;
                    for (int i = 0; i < us.Count; i++)
                    {
                        if (!us[i].Alive) continue;
                        Hit(BattleEventType.AreaAttack, BattleSide.Enemies, actorIndex, BattleSide.Team, us, i,
                            areaDamage * Roll(), now);
                    }
                    return;
                }

                int target = PickEnemyTarget(us, rng);
                Hit(BattleEventType.Attack, BattleSide.Enemies, actorIndex, BattleSide.Team, us, target,
                    actor.stats.Atk * actor.stats.DamageMultiplier * Roll(), now);
            }

            void Hit(BattleEventType type, BattleSide actorSide, int actorIndex, BattleSide targetSide,
                List<Unit> targets, int targetIndex, float rawDamage, float now)
            {
                Unit target = targets[targetIndex];
                float damage = Math.Min(target.hp, rawDamage * target.stats.DamageTakenMultiplier);
                target.hp -= damage;
                if (target.hp < 1e-4f) target.hp = 0f;
                events.Add(new BattleEvent(now, type, actorSide, actorIndex, targetSide, targetIndex, damage, target.hp));
                if (!target.Alive)
                {
                    events.Add(new BattleEvent(now, BattleEventType.Defeated, actorSide, actorIndex, targetSide,
                        targetIndex, 0f, 0f));
                }
            }

            float Roll()
            {
                float variance = Math.Max(0f, rules.damageVariance);
                return 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * variance;
            }

            BattleResult Finish(BattleOutcome outcome, float duration) =>
                new BattleResult(outcome, duration, teamCopy, enemyCopy, events, HpLeftFraction(them),
                    teamPower, recommendedPower);

            if (us.Count == 0) return Finish(BattleOutcome.TeamDefeated, 0f);
            if (them.Count == 0) return Finish(BattleOutcome.Won, 0f);

            while (true)
            {
                Unit next = NextActor(us, them, out BattleSide side, out int nextIndex);
                if (next.nextActionAt > limit) return Finish(BattleOutcome.TimeRanOut, limit);
                float time = (float)next.nextActionAt;

                if (side == BattleSide.Team) TeamAction(next, nextIndex, time);
                else EnemyAction(next, nextIndex, time);
                next.nextActionAt += next.stats.AttackInterval;

                if (!AnyAlive(them)) return Finish(BattleOutcome.Won, time);
                if (!AnyAlive(us)) return Finish(BattleOutcome.TeamDefeated, time);
            }
        }

        /// <summary>
        /// The team's power: Σ (ATK + HP ÷ powerHpDivisor), × (1 + supportTeamBonus) with a Support.
        /// Compare with a boss's or Expedition's recommended power.
        /// </summary>
        public static float GetTeamPower(IEnumerable<Combatant> team, CombatRules rules)
        {
            if (team == null) throw new ArgumentNullException(nameof(team));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            float sum = 0f;
            bool support = false;
            foreach (Combatant c in team)
            {
                if (c == null) continue;
                sum += rules.GetPower(c.MaxHp, c.Atk);
                if (c.Role == MinionRole.Support) support = true;
            }
            return support ? sum * (1f + rules.supportTeamBonus) : sum;
        }

        static bool HasSupport(List<Combatant> team)
        {
            foreach (Combatant c in team) if (c.Role == MinionRole.Support) return true;
            return false;
        }

        static List<Unit> MakeUnits(List<Combatant> combatants)
        {
            var units = new List<Unit>();
            foreach (Combatant c in combatants)
            {
                if (c == null) throw new ArgumentException("A fight can't include a null combatant.");
                units.Add(new Unit { stats = c, hp = c.MaxHp, nextActionAt = c.AttackInterval });
            }
            return units;
        }

        // The standing unit that acts soonest. Ties: team before enemies, then slot order.
        static Unit NextActor(List<Unit> us, List<Unit> them, out BattleSide side, out int index)
        {
            Unit best = null;
            side = BattleSide.Team;
            index = -1;
            for (int i = 0; i < us.Count; i++)
            {
                if (!us[i].Alive || (best != null && us[i].nextActionAt >= best.nextActionAt)) continue;
                best = us[i];
                side = BattleSide.Team;
                index = i;
            }
            for (int i = 0; i < them.Count; i++)
            {
                if (!them[i].Alive || (best != null && them[i].nextActionAt >= best.nextActionAt)) continue;
                best = them[i];
                side = BattleSide.Enemies;
                index = i;
            }
            return best;
        }

        // A standing Tank (first in slot order), otherwise a random standing Minion.
        static int PickEnemyTarget(List<Unit> us, Random rng)
        {
            for (int i = 0; i < us.Count; i++)
            {
                if (us[i].Alive && us[i].stats.Taunts) return i;
            }
            int alive = 0;
            foreach (Unit u in us) if (u.Alive) alive++;
            int pick = rng.Next(alive);
            for (int i = 0; i < us.Count; i++)
            {
                if (!us[i].Alive) continue;
                if (pick == 0) return i;
                pick--;
            }
            return 0;
        }

        // The standing ally with the lowest HP share below the threshold, or -1.
        static int MostHurt(List<Unit> us, float belowFraction)
        {
            int best = -1;
            float bestFraction = float.MaxValue;
            for (int i = 0; i < us.Count; i++)
            {
                Unit u = us[i];
                if (!u.Alive) continue;
                float fraction = u.hp / u.stats.MaxHp;
                if (fraction >= belowFraction || fraction >= bestFraction) continue;
                best = i;
                bestFraction = fraction;
            }
            return best;
        }

        static bool AnyAlive(List<Unit> units)
        {
            foreach (Unit u in units) if (u.Alive) return true;
            return false;
        }

        static float HpLeftFraction(List<Unit> enemies)
        {
            float left = 0f, max = 0f;
            foreach (Unit u in enemies)
            {
                left += Math.Max(0f, u.hp);
                max += u.stats.MaxHp;
            }
            return max > 0 ? left / max : 0f;
        }
    }
}
