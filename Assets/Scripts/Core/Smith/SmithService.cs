using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.State;

namespace Deepshaft.Smith
{
    public enum SmithStatus
    {
        /// <summary>Can be done now (or, after a Try method, was done).</summary>
        Ok,
        /// <summary>No owned Minion with that id, or its definition was removed from the content.</summary>
        NotFound,
        /// <summary>That kind of Minion has no such slot (Miners carry a tool, Adventurers a weapon and armor).</summary>
        WrongSlot,
        /// <summary>Gear upgrade: the item is at its level cap and needs a breakthrough first.</summary>
        AtLevelCap,
        /// <summary>Gear breakthrough: the item must reach its level cap first.</summary>
        BelowLevelCap,
        /// <summary>Gear breakthrough: the Smith doesn't know the rank for it yet (needs a boss material).</summary>
        SmithRankTooLow,
        /// <summary>Gear breakthrough or Smith rank: there are no more in the content.</summary>
        NoMore,
        CantAfford,
    }

    /// <summary>Whether a Smith action can be done, its cost and what's missing, so the UI can explain it.</summary>
    public sealed class SmithCheck
    {
        public SmithCheck(SmithStatus status, IReadOnlyList<ResourceAmount> cost = null,
            IReadOnlyList<ResourceAmount> shortfall = null, int level = 0, int maxLevel = 0, int requiredRank = 0)
        {
            Status = status;
            Cost = cost ?? Array.Empty<ResourceAmount>();
            Shortfall = shortfall ?? Array.Empty<ResourceAmount>();
            Level = level;
            MaxLevel = maxLevel;
            RequiredRank = requiredRank;
        }

        public SmithStatus Status { get; }
        public bool CanDo => Status == SmithStatus.Ok;
        public IReadOnlyList<ResourceAmount> Cost { get; }

        /// <summary>How much more of each resource is needed. Empty if affordable or no wallet was given.</summary>
        public IReadOnlyList<ResourceAmount> Shortfall { get; }

        /// <summary>Gear: the item's level and cap (after the action, for a Try method that succeeded). Smith rank: the rank.</summary>
        public int Level { get; }
        public int MaxLevel { get; }

        /// <summary>Gear breakthrough: the Smith rank it needs.</summary>
        public int RequiredRank { get; }
    }

    /// <summary>
    /// The Smith (SCRUM-9; Game Design Summary v3.4 §5, Scope §4.5). Upgrades each Minion's own gear
    /// (SCRUM-10): ordinary levels with Gold + ores, breakthroughs at the cap with a rare Expedition
    /// material. Unique boss materials teach the Smith ranks, once each; rank N allows gear
    /// breakthrough N (team decision 2026-11-19). Numbers: <see cref="EquipmentRules"/> on the GameDatabase.
    ///
    /// Tool changes go through <see cref="MiningService.ChangeRate"/>, so the new bonus only counts from now on.
    /// </summary>
    public class SmithService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly MiningService mining;

        public SmithService(GameState state, GameDatabase database, MiningService mining)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.mining = mining ?? throw new ArgumentNullException(nameof(mining));
            if (state.smith == null) state.smith = new SmithData();
        }

        /// <summary>Raised after any Smith action succeeded.</summary>
        public event Action Changed;

        EquipmentRules Rules => database.Equipment;

        public int Rank => Math.Max(0, state.smith.rank);
        public int MaxRank => Rules.MaxSmithRank;

        /// <summary>The next rank the Smith can learn, or null if every rank is known.</summary>
        public SmithRank NextRank => Rules.GetSmithRank(Rank + 1);

        // ---------- Smith ranks ----------

        public SmithCheck CheckRankUp(Wallet wallet = null)
        {
            SmithRank next = NextRank;
            if (next == null) return new SmithCheck(SmithStatus.NoMore, level: Rank, maxLevel: MaxRank);
            return WithCost(next.cost ?? new List<ResourceAmount>(), wallet, Rank, MaxRank, Rank + 1);
        }

        /// <summary>Pays the boss material and teaches the Smith the next rank.</summary>
        public SmithCheck TryRankUp(Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            SmithCheck check = CheckRankUp(wallet);
            if (!check.CanDo || !wallet.TrySpend(check.Cost)) return check;
            state.smith.rank = Rank + 1;
            Changed?.Invoke();
            return new SmithCheck(SmithStatus.Ok, check.Cost, null, Rank, MaxRank, Rank);
        }

        // ---------- Gear ----------

        /// <summary>Can this Minion's item in <paramref name="slot"/> go up one level?</summary>
        public SmithCheck CheckUpgrade(int instanceId, EquipmentSlot slot, Wallet wallet = null)
        {
            if (!TryFind(instanceId, slot, out OwnedMinion m, out SmithCheck problem)) return problem;
            int level = EquipmentRules.GetLevel(m, slot);
            int max = Rules.GetMaxLevel(m, slot);
            if (level >= max) return new SmithCheck(SmithStatus.AtLevelCap, level: level, maxLevel: max);
            return WithCost(Rules.GetLevelUpCost(level), wallet, level, max, 0);
        }

        /// <summary>Can this Minion's item in <paramref name="slot"/> break through now?</summary>
        public SmithCheck CheckBreakthrough(int instanceId, EquipmentSlot slot, Wallet wallet = null)
        {
            if (!TryFind(instanceId, slot, out OwnedMinion m, out SmithCheck problem)) return problem;
            int level = EquipmentRules.GetLevel(m, slot);
            int max = Rules.GetMaxLevel(m, slot);
            int done = EquipmentRules.GetBreakthroughs(m, slot);
            List<ResourceAmount> cost = Rules.GetBreakthroughCost(done);
            if (cost == null) return new SmithCheck(SmithStatus.NoMore, level: level, maxLevel: max);

            int required = done + 1;
            List<ResourceAmount> shortfall = wallet != null ? wallet.GetShortfall(cost) : null;
            if (level < max) return new SmithCheck(SmithStatus.BelowLevelCap, cost, shortfall, level, max, required);
            if (Rank < required) return new SmithCheck(SmithStatus.SmithRankTooLow, cost, shortfall, level, max, required);
            return WithCost(cost, wallet, level, max, required);
        }

        /// <summary>Pays and raises the item one level, or does nothing and says why.</summary>
        public SmithCheck TryUpgrade(int instanceId, EquipmentSlot slot, Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            SmithCheck check = CheckUpgrade(instanceId, slot, wallet);
            if (!check.CanDo || !wallet.TrySpend(check.Cost)) return check;

            OwnedMinion m = state.FindMinion(instanceId);
            mining.ChangeRate(() => EquipmentRules.GetOrAdd(m, slot).level++);
            Changed?.Invoke();
            return Done(check, m, slot);
        }

        /// <summary>Pays and does the item's next breakthrough (its level cap goes up), or does nothing and says why.</summary>
        public SmithCheck TryBreakthrough(int instanceId, EquipmentSlot slot, Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            SmithCheck check = CheckBreakthrough(instanceId, slot, wallet);
            if (!check.CanDo || !wallet.TrySpend(check.Cost)) return check;

            OwnedMinion m = state.FindMinion(instanceId);
            EquipmentRules.GetOrAdd(m, slot).breakthroughs++;
            Changed?.Invoke();
            return Done(check, m, slot);
        }

        SmithCheck Done(SmithCheck paid, OwnedMinion m, EquipmentSlot slot) =>
            new SmithCheck(SmithStatus.Ok, paid.Cost, null, EquipmentRules.GetLevel(m, slot),
                Rules.GetMaxLevel(m, slot), paid.RequiredRank);

        bool TryFind(int instanceId, EquipmentSlot slot, out OwnedMinion minion, out SmithCheck problem)
        {
            minion = state.FindMinion(instanceId);
            MinionDefinition d = minion != null ? database.FindMinion(minion.definitionId) : null;
            problem = null;
            if (d == null) problem = new SmithCheck(SmithStatus.NotFound);
            else if (!((IList<EquipmentSlot>)EquipmentRules.SlotsFor(d.Kind)).Contains(slot)) problem = new SmithCheck(SmithStatus.WrongSlot);
            return problem == null;
        }

        static SmithCheck WithCost(List<ResourceAmount> cost, Wallet wallet, int level, int max, int requiredRank)
        {
            List<ResourceAmount> shortfall = wallet != null ? wallet.GetShortfall(cost) : new List<ResourceAmount>();
            SmithStatus status = shortfall.Count > 0 ? SmithStatus.CantAfford : SmithStatus.Ok;
            return new SmithCheck(status, cost, shortfall, level, max, requiredRank);
        }
    }
}
