using System;
using System.Collections.Generic;
using System.Globalization;

namespace Deepshaft.Combat
{
    public enum BattleOutcome
    {
        Won,
        /// <summary>Every team Minion was knocked out.</summary>
        TeamDefeated,
        /// <summary>The enemies were still standing at <see cref="CombatRules.timeLimitSeconds"/>.</summary>
        TimeRanOut,
    }

    public enum BattleSide
    {
        Team,
        Enemies,
    }

    public enum BattleEventType
    {
        /// <summary>A single-target hit, or one target of a Mage's hit-all attack.</summary>
        Attack,
        /// <summary>One target of an enemy's area attack.</summary>
        AreaAttack,
        Heal,
        /// <summary>The target was knocked out. Follows the hit that did it.</summary>
        Defeated,
    }

    /// <summary>
    /// One thing that happened in a fight, at <see cref="Time"/> seconds. The UI replays
    /// <see cref="BattleResult.Events"/> in order to show the fight on screen.
    /// Indices point into <see cref="BattleResult.Team"/> or <see cref="BattleResult.Enemies"/>.
    /// </summary>
    public readonly struct BattleEvent
    {
        public BattleEvent(float time, BattleEventType type, BattleSide actorSide, int actorIndex,
            BattleSide targetSide, int targetIndex, float amount, float targetHpAfter)
        {
            Time = time;
            Type = type;
            ActorSide = actorSide;
            ActorIndex = actorIndex;
            TargetSide = targetSide;
            TargetIndex = targetIndex;
            Amount = amount;
            TargetHpAfter = targetHpAfter;
        }

        public float Time { get; }
        public BattleEventType Type { get; }
        public BattleSide ActorSide { get; }
        public int ActorIndex { get; }
        public BattleSide TargetSide { get; }
        public int TargetIndex { get; }

        /// <summary>Damage dealt or HP healed. 0 for <see cref="BattleEventType.Defeated"/>.</summary>
        public float Amount { get; }

        public float TargetHpAfter { get; }

        public override string ToString() =>
            Time.ToString("0.00", CultureInfo.InvariantCulture) + "s " + Type + " " + ActorSide + "[" + ActorIndex +
            "] → " + TargetSide + "[" + TargetIndex + "] " + Amount.ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The whole fight, worked out at once by <see cref="CombatSimulator"/>. Rewards are given
    /// straight away; the UI plays the <see cref="Events"/> back over <see cref="DurationSeconds"/>.
    /// </summary>
    public sealed class BattleResult
    {
        public BattleResult(BattleOutcome outcome, float durationSeconds, IReadOnlyList<Combatant> team,
            IReadOnlyList<Combatant> enemies, IReadOnlyList<BattleEvent> events, float enemyHpLeftFraction,
            float teamPower, float recommendedPower)
        {
            Outcome = outcome;
            DurationSeconds = durationSeconds;
            Team = team ?? throw new ArgumentNullException(nameof(team));
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            EnemyHpLeftFraction = enemyHpLeftFraction;
            TeamPower = teamPower;
            RecommendedPower = recommendedPower;
        }

        public BattleOutcome Outcome { get; }
        public bool Won => Outcome == BattleOutcome.Won;
        public float DurationSeconds { get; }
        public IReadOnlyList<Combatant> Team { get; }
        public IReadOnlyList<Combatant> Enemies { get; }
        public IReadOnlyList<BattleEvent> Events { get; }

        /// <summary>Enemy HP still left at the end, as a share of their total max HP (0–1).</summary>
        public float EnemyHpLeftFraction { get; }

        public float TeamPower { get; }
        public float RecommendedPower { get; }
        public bool IsBelowRecommendedPower => TeamPower < RecommendedPower;

        /// <summary>
        /// Why the fight was lost, for the result screen, e.g.
        /// "Out of time. Boss HP left: 32%. Team power 54 is below the recommended 70."
        /// Empty if the fight was won. Grey-box English text until localization (SCRUM-25).
        /// </summary>
        public string DescribeLoss(string enemyLabel = "Enemy")
        {
            if (Won) return "";
            string reason = Outcome == BattleOutcome.TimeRanOut ? "Out of time." : "Your team was knocked out.";
            int hpLeft = (int)Math.Ceiling(EnemyHpLeftFraction * 100 - 1e-4);
            string text = reason + " " + enemyLabel + " HP left: " + hpLeft + "%.";
            string power = Math.Floor(TeamPower).ToString(CultureInfo.InvariantCulture);
            string recommended = Math.Ceiling(RecommendedPower).ToString(CultureInfo.InvariantCulture);
            if (IsBelowRecommendedPower)
            {
                text += " Team power " + power + " is below the recommended " + recommended +
                        ". Level up your Adventure Minions or improve their gear.";
            }
            else
            {
                text += " Team power " + power + " meets the recommended " + recommended +
                        ". Try again, or try a different team.";
            }
            return text;
        }
    }
}
