using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Mining;
using Deepshaft.State;

namespace Deepshaft.Progression
{
    public enum TierUnlockStatus
    {
        /// <summary>Every requirement is met (or, after <see cref="TierProgressionService.TryUnlockNext"/>, the tier was unlocked).</summary>
        Ok,
        /// <summary>There's no next tier in the content (tier 3 in the prototype).</summary>
        MaxTierReached,
        BossNotDefeated,
        ExpeditionNotCleared,
        CantAfford,
    }

    /// <summary>Whether the next mine tier can be unlocked, and what's missing, so the UI can explain it.</summary>
    public sealed class TierUnlockCheck
    {
        public TierUnlockCheck(int tier, TierUnlockStatus status, List<string> missingBosses,
            List<string> missingFirstClears, List<ResourceAmount> shortfall, IReadOnlyList<ResourceAmount> cost)
        {
            Tier = tier;
            Status = status;
            MissingBosses = missingBosses;
            MissingFirstClears = missingFirstClears;
            Shortfall = shortfall;
            Cost = cost;
        }

        /// <summary>The tier this check is for.</summary>
        public int Tier { get; }
        public TierUnlockStatus Status { get; }
        public bool CanUnlock => Status == TierUnlockStatus.Ok;

        /// <summary>Ids of required bosses not beaten yet.</summary>
        public IReadOnlyList<string> MissingBosses { get; }

        /// <summary>Ids of required Expeditions not first-cleared yet.</summary>
        public IReadOnlyList<string> MissingFirstClears { get; }

        /// <summary>How much more of each resource is needed. Empty if affordable or if no wallet was given.</summary>
        public IReadOnlyList<ResourceAmount> Shortfall { get; }

        public IReadOnlyList<ResourceAmount> Cost { get; }
    }

    /// <summary>
    /// Mine tier progression (Game Design Summary v3.4 §8, SCRUM-7). The next tier unlocks when
    /// its <see cref="TierRequirement"/> is met: bosses beaten, Expeditions first-cleared, and an
    /// optional cost. The rules are data (GameDatabase → Tier Requirements); SCRUM-2 sets the final ones.
    ///
    /// - A tier without a cost unlocks by itself as soon as it's met (<see cref="TryAutoUnlock"/>,
    ///   called after every boss win and first clear).
    /// - A tier with a cost waits for the player to pay (<see cref="TryUnlockNext"/>).
    /// - Unlocking goes through <see cref="MiningService.UnlockTier"/>, so the Mine banks at the old
    ///   tier's rate first and the Miners move down at once.
    /// </summary>
    public class TierProgressionService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly MiningService mining;

        public TierProgressionService(GameState state, GameDatabase database, MiningService mining)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.mining = mining ?? throw new ArgumentNullException(nameof(mining));
            state.EnsureValid();
        }

        /// <summary>Raised after a tier is unlocked. Arg: the new highest unlocked tier.</summary>
        public event Action<int> TierUnlocked;

        public int HighestUnlockedTier => Math.Max(1, state.highestUnlockedTier);
        public int NextTier => HighestUnlockedTier + 1;

        /// <summary>
        /// Checks the next tier. Pass the wallet to include the cost; without one the cost
        /// isn't checked (only what else is missing).
        /// </summary>
        public TierUnlockCheck CheckNext(Wallet wallet = null) => Check(NextTier, wallet);

        /// <summary>Unlocks the next tier if every requirement is met, paying its cost from <paramref name="wallet"/>.</summary>
        public TierUnlockCheck TryUnlockNext(Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            TierUnlockCheck check = Check(NextTier, wallet);
            if (!check.CanUnlock) return check;
            if (!wallet.TrySpend(check.Cost)) return Check(NextTier, wallet);
            Unlock(check.Tier);
            return check;
        }

        /// <summary>
        /// Unlocks every next tier that is met and costs nothing. Returns the tiers unlocked
        /// (usually none or one).
        /// </summary>
        public List<int> TryAutoUnlock()
        {
            var unlocked = new List<int>();
            while (true)
            {
                TierUnlockCheck check = Check(NextTier, null);
                if (!check.CanUnlock || HasCost(check.Cost)) return unlocked;
                Unlock(check.Tier);
                unlocked.Add(check.Tier);
            }
        }

        TierUnlockCheck Check(int tier, Wallet wallet)
        {
            var missingBosses = new List<string>();
            var missingClears = new List<string>();
            var shortfall = new List<ResourceAmount>();
            TierRequirement requirement = database.FindTierRequirement(tier);
            if (requirement == null)
            {
                return new TierUnlockCheck(tier, TierUnlockStatus.MaxTierReached, missingBosses, missingClears,
                    shortfall, new List<ResourceAmount>());
            }

            var cost = new List<ResourceAmount>(requirement.cost ?? new List<ResourceAmount>());
            if (requirement.requiredBosses != null)
            {
                foreach (string id in requirement.requiredBosses)
                {
                    if (!string.IsNullOrEmpty(id) && !state.bosses.defeated.Contains(id)) missingBosses.Add(id);
                }
            }
            if (requirement.requiredFirstClears != null)
            {
                foreach (string id in requirement.requiredFirstClears)
                {
                    if (!string.IsNullOrEmpty(id) && !state.expeditions.cleared.Contains(id)) missingClears.Add(id);
                }
            }
            if (wallet != null) shortfall = wallet.GetShortfall(cost);

            TierUnlockStatus status = TierUnlockStatus.Ok;
            if (missingBosses.Count > 0) status = TierUnlockStatus.BossNotDefeated;
            else if (missingClears.Count > 0) status = TierUnlockStatus.ExpeditionNotCleared;
            else if (shortfall.Count > 0) status = TierUnlockStatus.CantAfford;
            return new TierUnlockCheck(tier, status, missingBosses, missingClears, shortfall, cost);
        }

        void Unlock(int tier)
        {
            mining.UnlockTier(tier);
            TierUnlocked?.Invoke(HighestUnlockedTier);
        }

        static bool HasCost(IReadOnlyList<ResourceAmount> cost)
        {
            foreach (ResourceAmount c in cost) if (c.amount > 0) return true;
            return false;
        }
    }
}
