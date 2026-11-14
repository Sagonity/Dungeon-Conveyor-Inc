using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Minions;
using Deepshaft.State;

namespace Deepshaft.Roster
{
    /// <summary>
    /// Assignment rules shared by <see cref="RosterService"/> and <see cref="Mining.MiningService"/>.
    /// Pure functions over the save and the content; no clock, no banking.
    /// </summary>
    internal static class RosterLogic
    {
        /// <summary>
        /// The valid Minions in <paramref name="slots"/>, in slot order: owned, definition still
        /// exists, the right kind, no duplicates, and at most <paramref name="slotCount"/>.
        /// </summary>
        public static List<OwnedMinion> GetValid(GameState state, GameDatabase database, List<int> slots,
            MinionKind kind, int slotCount)
        {
            var result = new List<OwnedMinion>();
            if (slots == null) return result;
            foreach (int instanceId in slots)
            {
                if (result.Count >= slotCount) break;
                OwnedMinion minion = state.FindMinion(instanceId);
                if (minion == null || result.Contains(minion) || !IsKind(database, minion, kind)) continue;
                result.Add(minion);
            }
            return result;
        }

        public static bool IsKind(GameDatabase database, OwnedMinion minion, MinionKind kind)
        {
            MinionDefinition definition = database.FindMinion(minion.definitionId);
            return definition != null && definition.Kind == kind;
        }

        /// <summary>Rewrites <paramref name="slots"/> to hold only its valid entries.</summary>
        public static void Prune(GameState state, GameDatabase database, List<int> slots, MinionKind kind, int slotCount)
        {
            List<OwnedMinion> valid = GetValid(state, database, slots, kind, slotCount);
            slots.Clear();
            foreach (OwnedMinion m in valid) slots.Add(m.instanceId);
        }

        /// <summary>Adds <paramref name="minion"/> to <paramref name="slots"/> if it fits. Doesn't bank.</summary>
        public static bool TryFill(GameState state, GameDatabase database, List<int> slots, MinionKind kind,
            int slotCount, OwnedMinion minion)
        {
            if (minion == null || !IsKind(database, minion, kind) || slots.Contains(minion.instanceId)) return false;
            Prune(state, database, slots, kind, slotCount);
            if (slots.Count >= slotCount) return false;
            slots.Add(minion.instanceId);
            return true;
        }

        /// <summary>
        /// First use of the roster (new game, or a save from before SCRUM-6): makes sure the
        /// data exists, then fills empty Miner and team slots from owned Minions in roster order,
        /// so an old save keeps mining with the Miners it had. Runs once per save.
        /// Call it before any banking so the old rate isn't paid twice.
        /// </summary>
        public static void EnsureInitialized(GameState state, GameDatabase database)
        {
            if (state.roster == null) state.roster = new RosterData();
            RosterData roster = state.roster;
            if (roster.miners == null) roster.miners = new List<int>();
            if (roster.adventureTeam == null) roster.adventureTeam = new List<int>();
            if (roster.initialized) return;

            RosterRules rules = database.Roster;
            foreach (OwnedMinion m in state.minions)
            {
                TryFill(state, database, roster.miners, MinionKind.Miner, rules.MinerSlots, m);
                TryFill(state, database, roster.adventureTeam, MinionKind.Adventure, rules.AdventureSlots, m);
            }
            roster.initialized = true;
        }
    }
}
