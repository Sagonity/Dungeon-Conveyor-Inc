using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Mining;
using Deepshaft.State;

namespace Deepshaft.Minions
{
    public enum MinionUpgradeStatus
    {
        /// <summary>Can be done now (or, after a Try method, was done).</summary>
        Ok,
        /// <summary>No owned Minion with that id, or its definition was removed from the content.</summary>
        NotFound,
        /// <summary>Level-up: the Minion is at its level cap and needs a breakthrough first.</summary>
        AtLevelCap,
        /// <summary>Breakthrough: the Minion must reach its level cap first.</summary>
        BelowLevelCap,
        /// <summary>Breakthrough: every breakthrough in the content is done.</summary>
        NoMoreBreakthroughs,
        CantAfford,
    }

    /// <summary>Whether a level-up or breakthrough can be done, its cost and what's missing, so the UI can explain it.</summary>
    public sealed class MinionUpgradeCheck
    {
        public MinionUpgradeCheck(MinionUpgradeStatus status, IReadOnlyList<ResourceAmount> cost,
            IReadOnlyList<ResourceAmount> shortfall, int level, int maxLevel)
        {
            Status = status;
            Cost = cost ?? Array.Empty<ResourceAmount>();
            Shortfall = shortfall ?? Array.Empty<ResourceAmount>();
            Level = level;
            MaxLevel = maxLevel;
        }

        public MinionUpgradeStatus Status { get; }
        public bool CanDo => Status == MinionUpgradeStatus.Ok;

        /// <summary>The cost, or empty when there's nothing to buy (not found, at the cap, no more breakthroughs).</summary>
        public IReadOnlyList<ResourceAmount> Cost { get; }

        /// <summary>How much more of each resource is needed. Empty if affordable.</summary>
        public IReadOnlyList<ResourceAmount> Shortfall { get; }

        /// <summary>The Minion's level and level cap when checked (after the action, for a Try method that succeeded).</summary>
        public int Level { get; }
        public int MaxLevel { get; }
    }

    /// <summary>
    /// Minion development (SCRUM-10; Game Design Summary v3.4 §5): standard levels paid with Gold +
    /// ores, and breakthroughs paid with rare Expedition materials that raise the level cap.
    /// The numbers are <see cref="MinionProgressionRules"/> on the GameDatabase.
    ///
    /// - A level-up or breakthrough is allowed while the Adventure team is on an automated
    ///   Expedition: automation can't fail and doesn't depend on team power.
    /// - Changes go through <see cref="MiningService.ChangeRate"/>, so a Miner's new level only
    ///   counts from now on and what was mined at the old rate is banked first.
    /// - Ascension is not in the prototype (team decision 2026-11-19).
    /// </summary>
    public class MinionProgressionService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly MiningService mining;

        public MinionProgressionService(GameState state, GameDatabase database, MiningService mining)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.mining = mining ?? throw new ArgumentNullException(nameof(mining));
        }

        /// <summary>Raised after a level-up. Arg: the Minion.</summary>
        public event Action<OwnedMinion> LeveledUp;

        /// <summary>Raised after a breakthrough. Arg: the Minion.</summary>
        public event Action<OwnedMinion> BrokeThrough;

        MinionProgressionRules Rules => database.MinionProgression;

        /// <summary>The Minion's level cap with the breakthroughs it has.</summary>
        public int GetMaxLevel(OwnedMinion minion)
        {
            MinionDefinition d = minion != null ? database.FindMinion(minion.definitionId) : null;
            return d != null ? d.GetMaxLevel(minion.breakthroughs) : 0;
        }

        /// <summary>How many breakthroughs a Minion can have in the content.</summary>
        public int MaxBreakthroughs => Rules.MaxBreakthroughs;

        /// <summary>Can this Minion go up one level? Pass a wallet to check the cost too.</summary>
        public MinionUpgradeCheck CheckLevelUp(int instanceId, Wallet wallet = null)
        {
            if (!TryFind(instanceId, out OwnedMinion m, out MinionDefinition d)) return NotFound();
            int max = d.GetMaxLevel(m.breakthroughs);
            if (m.level >= max) return new MinionUpgradeCheck(MinionUpgradeStatus.AtLevelCap, null, null, m.level, max);
            return WithCost(Rules.GetLevelUpCost(m.level), wallet, m.level, max);
        }

        /// <summary>Can this Minion break through now? Pass a wallet to check the cost too.</summary>
        public MinionUpgradeCheck CheckBreakthrough(int instanceId, Wallet wallet = null)
        {
            if (!TryFind(instanceId, out OwnedMinion m, out MinionDefinition d)) return NotFound();
            int max = d.GetMaxLevel(m.breakthroughs);
            List<ResourceAmount> cost = Rules.GetBreakthroughCost(m.breakthroughs);
            if (cost == null) return new MinionUpgradeCheck(MinionUpgradeStatus.NoMoreBreakthroughs, null, null, m.level, max);
            if (m.level < max)
            {
                // Show the cost already, so the player knows what to farm while levelling.
                return new MinionUpgradeCheck(MinionUpgradeStatus.BelowLevelCap, cost,
                    wallet != null ? wallet.GetShortfall(cost) : null, m.level, max);
            }
            return WithCost(cost, wallet, m.level, max);
        }

        /// <summary>Pays and raises the Minion one level, or does nothing and says why.</summary>
        public MinionUpgradeCheck TryLevelUp(int instanceId, Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            MinionUpgradeCheck check = CheckLevelUp(instanceId, wallet);
            if (!check.CanDo || !wallet.TrySpend(check.Cost)) return check;

            OwnedMinion m = state.FindMinion(instanceId);
            mining.ChangeRate(() => m.level++);
            LeveledUp?.Invoke(m);
            return new MinionUpgradeCheck(MinionUpgradeStatus.Ok, check.Cost, null, m.level, GetMaxLevel(m));
        }

        /// <summary>Pays and does the next breakthrough (the level cap goes up), or does nothing and says why.</summary>
        public MinionUpgradeCheck TryBreakthrough(int instanceId, Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            MinionUpgradeCheck check = CheckBreakthrough(instanceId, wallet);
            if (!check.CanDo || !wallet.TrySpend(check.Cost)) return check;

            OwnedMinion m = state.FindMinion(instanceId);
            m.breakthroughs++;
            BrokeThrough?.Invoke(m);
            return new MinionUpgradeCheck(MinionUpgradeStatus.Ok, check.Cost, null, m.level, GetMaxLevel(m));
        }

        bool TryFind(int instanceId, out OwnedMinion minion, out MinionDefinition definition)
        {
            minion = state.FindMinion(instanceId);
            definition = minion != null ? database.FindMinion(minion.definitionId) : null;
            return definition != null;
        }

        static MinionUpgradeCheck NotFound() => new MinionUpgradeCheck(MinionUpgradeStatus.NotFound, null, null, 0, 0);

        static MinionUpgradeCheck WithCost(List<ResourceAmount> cost, Wallet wallet, int level, int max)
        {
            List<ResourceAmount> shortfall = wallet != null ? wallet.GetShortfall(cost) : new List<ResourceAmount>();
            MinionUpgradeStatus status = shortfall.Count > 0 ? MinionUpgradeStatus.CantAfford : MinionUpgradeStatus.Ok;
            return new MinionUpgradeCheck(status, cost, shortfall, level, max);
        }
    }
}
