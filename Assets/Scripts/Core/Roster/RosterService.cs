using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.State;

namespace Deepshaft.Roster
{
    /// <summary>
    /// Miner and Adventure team assignment (SCRUM-6, Game Design Summary v3.4 §3, Scope §4.4).
    ///
    /// There's no movement or placement: the player only chooses who works.
    /// - Miner Minions can only go in the Mine's Miner slots; only assigned Miners mine.
    /// - Adventure Minions can only go in the Adventure team, which fights bosses and
    ///   Expedition first clears (SCRUM-7).
    /// - Slot counts are data (<see cref="RosterRules"/> on the GameDatabase).
    /// - While an automated Expedition runs, the team is busy: it can't be edited until the
    ///   player stops the Expedition and collects (<see cref="ITeamBusySource"/>).
    /// - Changing the Miners changes the mining rate, so it goes through
    ///   <see cref="MiningService.ChangeRate"/> and the old rate is banked first.
    /// - New Minions fill a free slot of their kind automatically; the player can swap them later.
    /// </summary>
    public class RosterService
    {
        readonly GameState state;
        readonly GameDatabase database;
        readonly MiningService mining;
        readonly ITeamBusySource busy;

        public RosterService(GameState state, GameDatabase database, MiningService mining, ITeamBusySource busy = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.mining = mining ?? throw new ArgumentNullException(nameof(mining));
            this.busy = busy ?? NoExpeditions.Instance;
            RosterLogic.EnsureInitialized(state, database);
        }

        RosterData Data => state.roster;
        RosterRules Rules => database.Roster;

        /// <summary>Raised after any assignment change, so lists and the HUD can refresh.</summary>
        public event Action Changed;

        public int MinerSlots => Rules.MinerSlots;
        public int AdventureSlots => Rules.AdventureSlots;

        /// <summary>True while an automated Expedition runs. Show "Busy" and "Stop Expedition &amp; Collect".</summary>
        public bool IsTeamBusy => busy.IsAdventureTeamBusy;

        /// <summary>The Miners working in the Mine, in slot order.</summary>
        public List<OwnedMinion> GetMiners() => mining.GetWorkingMiners();

        /// <summary>The Adventure team, in slot order. Valid even while busy (it's the team on the Expedition).</summary>
        public List<OwnedMinion> GetTeam() =>
            RosterLogic.GetValid(state, database, Data.adventureTeam, MinionKind.Adventure, AdventureSlots);

        /// <summary>
        /// Owned Minions of <paramref name="kind"/> that aren't assigned: the pick list
        /// behind a "+" slot. Minions whose definition was removed are left out.
        /// </summary>
        public List<OwnedMinion> GetUnassigned(MinionKind kind)
        {
            List<OwnedMinion> assigned = kind == MinionKind.Miner ? GetMiners() : GetTeam();
            var result = new List<OwnedMinion>();
            foreach (OwnedMinion m in state.minions)
            {
                if (RosterLogic.IsKind(database, m, kind) && !assigned.Contains(m)) result.Add(m);
            }
            return result;
        }

        public bool IsAssigned(int instanceId)
        {
            foreach (OwnedMinion m in GetMiners()) if (m.instanceId == instanceId) return true;
            foreach (OwnedMinion m in GetTeam()) if (m.instanceId == instanceId) return true;
            return false;
        }

        public AssignResult AssignMiner(int instanceId)
        {
            AssignResult check = CheckAssign(instanceId, MinionKind.Miner, GetMiners(), MinerSlots);
            if (check != AssignResult.Ok) return check;
            mining.ChangeRate(() =>
            {
                RosterLogic.Prune(state, database, Data.miners, MinionKind.Miner, MinerSlots);
                Data.miners.Add(instanceId);
            });
            Changed?.Invoke();
            return AssignResult.Ok;
        }

        public AssignResult UnassignMiner(int instanceId)
        {
            if (!Contains(GetMiners(), instanceId)) return AssignResult.NotAssigned;
            mining.ChangeRate(() =>
            {
                RosterLogic.Prune(state, database, Data.miners, MinionKind.Miner, MinerSlots);
                Data.miners.Remove(instanceId);
            });
            Changed?.Invoke();
            return AssignResult.Ok;
        }

        public AssignResult AssignToTeam(int instanceId)
        {
            if (IsTeamBusy) return AssignResult.TeamBusy;
            AssignResult check = CheckAssign(instanceId, MinionKind.Adventure, GetTeam(), AdventureSlots);
            if (check != AssignResult.Ok) return check;
            RosterLogic.Prune(state, database, Data.adventureTeam, MinionKind.Adventure, AdventureSlots);
            Data.adventureTeam.Add(instanceId);
            Changed?.Invoke();
            return AssignResult.Ok;
        }

        public AssignResult RemoveFromTeam(int instanceId)
        {
            if (IsTeamBusy) return AssignResult.TeamBusy;
            if (!Contains(GetTeam(), instanceId)) return AssignResult.NotAssigned;
            RosterLogic.Prune(state, database, Data.adventureTeam, MinionKind.Adventure, AdventureSlots);
            Data.adventureTeam.Remove(instanceId);
            Changed?.Invoke();
            return AssignResult.Ok;
        }

        /// <summary>
        /// Adds a new Minion (e.g. from a summon). A Miner fills a free Miner slot and starts
        /// mining now; an Adventure Minion fills a free team slot unless the team is busy.
        /// </summary>
        public OwnedMinion AddMinion(string definitionId)
        {
            OwnedMinion added = mining.AddMinion(definitionId);
            if (!IsTeamBusy)
            {
                RosterLogic.TryFill(state, database, Data.adventureTeam, MinionKind.Adventure, AdventureSlots, added);
            }
            Changed?.Invoke();
            return added;
        }

        AssignResult CheckAssign(int instanceId, MinionKind kind, List<OwnedMinion> current, int slotCount)
        {
            OwnedMinion minion = state.FindMinion(instanceId);
            if (minion == null || database.FindMinion(minion.definitionId) == null) return AssignResult.NotOwned;
            if (!RosterLogic.IsKind(database, minion, kind)) return AssignResult.WrongKind;
            if (current.Contains(minion)) return AssignResult.AlreadyAssigned;
            if (current.Count >= slotCount) return AssignResult.NoFreeSlot;
            return AssignResult.Ok;
        }

        static bool Contains(List<OwnedMinion> list, int instanceId)
        {
            foreach (OwnedMinion m in list) if (m.instanceId == instanceId) return true;
            return false;
        }
    }
}
