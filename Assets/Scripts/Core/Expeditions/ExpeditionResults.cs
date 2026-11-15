using System.Collections.Generic;
using Deepshaft.Combat;
using Deepshaft.Economy;

namespace Deepshaft.Expeditions
{
    /// <summary>Why an Expedition action did or didn't happen, so the UI can explain it.</summary>
    public enum ExpeditionCheck
    {
        Ok,
        /// <summary>No Expedition with that id in the GameDatabase.</summary>
        Unknown,
        /// <summary>Its boss hasn't been beaten yet.</summary>
        Locked,
        /// <summary>The first clear is already done (it only happens once).</summary>
        AlreadyCleared,
        /// <summary>Automation needs the first clear first.</summary>
        NotCleared,
        /// <summary>The Adventure team has no Minions.</summary>
        NoTeam,
        /// <summary>The team is on an automated Expedition. Stop it and collect first.</summary>
        TeamBusy,
        /// <summary>This Expedition is already running.</summary>
        AlreadyRunning,
        /// <summary>Another Expedition is running. Only one at a time.</summary>
        AnotherRunning,
        /// <summary>No automated Expedition is running.</summary>
        NotRunning,
    }

    /// <summary>Where an Expedition is in its life, for the Expedition list.</summary>
    public enum ExpeditionStatus
    {
        Locked,
        /// <summary>Unlocked; the first clear (an autobattle that can fail) is next.</summary>
        FirstClearAvailable,
        /// <summary>Cleared; automation can be started.</summary>
        ReadyToAutomate,
        /// <summary>Running automated. The Adventure team is busy.</summary>
        Running,
    }

    /// <summary>The result of <see cref="ExpeditionService.TryFirstClear"/>.</summary>
    public sealed class FirstClearResult
    {
        public FirstClearResult(ExpeditionCheck check, BattleResult battle, List<ResourceAmount> rewards,
            List<int> unlockedTiers)
        {
            Check = check;
            Battle = battle;
            Rewards = rewards;
            UnlockedTiers = unlockedTiers;
        }

        /// <summary>Ok if the fight happened (won or lost). Anything else means no fight.</summary>
        public ExpeditionCheck Check { get; }

        /// <summary>The fight to show. Null if <see cref="Check"/> isn't Ok.</summary>
        public BattleResult Battle { get; }

        public bool Won => Battle != null && Battle.Won;

        /// <summary>The one-time bonus, already in the wallet. Empty on a loss.</summary>
        public IReadOnlyList<ResourceAmount> Rewards { get; }

        /// <summary>Mine tiers this clear unlocked (only if the tier rules require first clears).</summary>
        public IReadOnlyList<int> UnlockedTiers { get; }
    }

    /// <summary>The result of <see cref="ExpeditionService.StopAndCollect"/>.</summary>
    public sealed class ExpeditionCollectResult
    {
        public ExpeditionCollectResult(ExpeditionCheck check, string expeditionId, double hoursRun,
            List<ResourceAmount> collected)
        {
            Check = check;
            ExpeditionId = expeditionId;
            HoursRun = hoursRun;
            Collected = collected;
        }

        /// <summary>Ok, or <see cref="ExpeditionCheck.NotRunning"/>.</summary>
        public ExpeditionCheck Check { get; }
        public string ExpeditionId { get; }

        /// <summary>How long it ran in total, for the summary ("8 h 12 min").</summary>
        public double HoursRun { get; }

        /// <summary>Whole units moved into the wallet. Fractions stay for the next run.</summary>
        public IReadOnlyList<ResourceAmount> Collected { get; }
    }
}
