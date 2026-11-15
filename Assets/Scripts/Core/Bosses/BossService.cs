using System;
using System.Collections.Generic;
using Deepshaft.Combat;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Expeditions;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.State;

namespace Deepshaft.Bosses
{
    /// <summary>Why a boss fight can or can't start, so the UI can explain it.</summary>
    public enum BossCheck
    {
        Ok,
        /// <summary>No boss with that id in the GameDatabase.</summary>
        Unknown,
        /// <summary>The boss's mine tier isn't unlocked yet.</summary>
        TierLocked,
        /// <summary>The Adventure team has no Minions.</summary>
        NoTeam,
        /// <summary>The team is on an automated Expedition. Stop it and collect first.</summary>
        TeamBusy,
    }

    /// <summary>The result of <see cref="BossService.Fight"/>.</summary>
    public sealed class BossFightResult
    {
        public BossFightResult(BossCheck check, BattleResult battle, bool firstWin, List<ResourceAmount> rewards,
            List<string> unlockedExpeditions, List<int> unlockedTiers)
        {
            Check = check;
            Battle = battle;
            FirstWin = firstWin;
            Rewards = rewards;
            UnlockedExpeditions = unlockedExpeditions;
            UnlockedTiers = unlockedTiers;
        }

        /// <summary>Ok if the fight happened (won or lost). Anything else means no fight.</summary>
        public BossCheck Check { get; }

        /// <summary>The fight to show. Null if <see cref="Check"/> isn't Ok.</summary>
        public BattleResult Battle { get; }

        public bool Won => Battle != null && Battle.Won;

        /// <summary>True the first time this boss is beaten.</summary>
        public bool FirstWin { get; }

        /// <summary>Already in the wallet. Includes the guaranteed boss material. Empty on a loss.</summary>
        public IReadOnlyList<ResourceAmount> Rewards { get; }

        /// <summary>Ids of Expeditions this win unlocked (first win only).</summary>
        public IReadOnlyList<string> UnlockedExpeditions { get; }

        /// <summary>Mine tiers this win unlocked.</summary>
        public IReadOnlyList<int> UnlockedTiers { get; }

        /// <summary>Why the fight was lost, e.g. "… Boss HP left: 32%. Team power 54 is below the recommended 70 …". Empty on a win.</summary>
        public string DescribeLoss() => Battle == null ? "" : Battle.DescribeLoss("Boss");
    }

    /// <summary>
    /// Mine Tier Bosses (Game Design Summary v3.4 §4, Scope §4.2, SCRUM-7).
    ///
    /// - One boss per tier, fought by the Adventure team in an autobattle (~30–60 s on screen).
    /// - Attempts are free and unlimited (Guardian Tickets were scrapped). The only gate is power.
    /// - A win always gives the boss's unique material. The first win also unlocks the boss's
    ///   Expeditions for good, and may unlock the next mine tier (<see cref="TierProgressionService"/>).
    /// - A loss costs nothing; <see cref="BossFightResult.DescribeLoss"/> says why it was lost.
    /// - The team can't fight while it's on an automated Expedition.
    /// </summary>
    public class BossService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly ITeamBusySource busy;
        readonly TierProgressionService tiers;

        /// <param name="busy">Usually the <see cref="ExpeditionService"/>.</param>
        /// <param name="tiers">Optional: unlocks the next tier after a win when its requirements are met.</param>
        public BossService(GameState state, GameDatabase database, ITeamBusySource busy,
            TierProgressionService tiers = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.busy = busy ?? NoExpeditions.Instance;
            this.tiers = tiers;
            state.EnsureValid();
            RosterLogic.EnsureInitialized(state, database);
        }

        /// <summary>Raised after a boss fight is won.</summary>
        public event Action<BossFightResult> BossDefeated;

        /// <summary>The boss of the highest unlocked tier (the next one to beat, or already beaten). Null if none.</summary>
        public BossDefinition CurrentBoss => database.FindBossForTier(Math.Max(1, state.highestUnlockedTier));

        public bool IsDefeated(string bossId) => !string.IsNullOrEmpty(bossId) && state.bosses.defeated.Contains(bossId);

        /// <summary>The team's power, to show next to the recommended power.</summary>
        public float GetTeamPower() => AdventureTeam.GetPower(state, database);

        public BossCheck CanFight(string bossId)
        {
            BossDefinition boss = database.FindBoss(bossId);
            if (boss == null) return BossCheck.Unknown;
            if (boss.tier > Math.Max(1, state.highestUnlockedTier)) return BossCheck.TierLocked;
            if (busy.IsAdventureTeamBusy) return BossCheck.TeamBusy;
            if (AdventureTeam.Build(state, database).Count == 0) return BossCheck.NoTeam;
            return BossCheck.Ok;
        }

        /// <summary>
        /// Fights the boss with the Adventure team. Rewards go straight into <paramref name="wallet"/>;
        /// the UI then plays <see cref="BossFightResult.Battle"/> back.
        /// </summary>
        public BossFightResult Fight(string bossId, Wallet wallet, Random rng)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var rewards = new List<ResourceAmount>();
            var unlockedExpeditions = new List<string>();
            var unlockedTiers = new List<int>();

            BossCheck check = CanFight(bossId);
            if (check != BossCheck.Ok)
            {
                return new BossFightResult(check, null, false, rewards, unlockedExpeditions, unlockedTiers);
            }

            BossDefinition boss = database.FindBoss(bossId);
            var enemies = new List<Combatant>();
            if (boss.enemy != null) enemies.Add(Combatant.ForEnemy(boss.enemy));
            BattleResult battle = CombatSimulator.Simulate(database.Combat, AdventureTeam.Build(state, database),
                enemies, boss.recommendedPower, rng);
            if (!battle.Won)
            {
                return new BossFightResult(check, battle, false, rewards, unlockedExpeditions, unlockedTiers);
            }

            bool firstWin = !IsDefeated(bossId);
            List<ResourceAmount> reward = firstWin ? boss.firstWinRewards : boss.repeatWinRewards;
            if (reward != null)
            {
                foreach (ResourceAmount r in reward)
                {
                    if (r.amount <= 0) continue;
                    wallet.Add(r.id, r.amount);
                    rewards.Add(r);
                }
            }

            if (firstWin)
            {
                state.bosses.defeated.Add(bossId);
                foreach (ExpeditionDefinition e in database.Expeditions)
                {
                    if (e != null && e.unlockedByBoss == bossId) unlockedExpeditions.Add(e.id);
                }
                if (tiers != null) unlockedTiers = tiers.TryAutoUnlock();
            }

            var result = new BossFightResult(check, battle, firstWin, rewards, unlockedExpeditions, unlockedTiers);
            BossDefeated?.Invoke(result);
            return result;
        }
    }
}
