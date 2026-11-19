using System;
using System.Collections.Generic;
using Deepshaft.Content;
using Deepshaft.Minions;
using Deepshaft.Roster;
using Deepshaft.State;

namespace Deepshaft.Combat
{
    /// <summary>
    /// Turns the saved Adventure team (SCRUM-6) into <see cref="Combatant"/>s for a fight.
    /// Used by boss fights and Expedition first clears.
    /// </summary>
    public static class AdventureTeam
    {
        /// <summary>The valid team Minions in slot order, with stats from their definition, level and gear.</summary>
        public static List<Combatant> Build(GameState state, GameDatabase database)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (database == null) throw new ArgumentNullException(nameof(database));
            RosterLogic.EnsureInitialized(state, database);

            var team = new List<Combatant>();
            List<OwnedMinion> members = RosterLogic.GetValid(state, database, state.roster.adventureTeam,
                MinionKind.Adventure, database.Roster.AdventureSlots);
            foreach (OwnedMinion m in members)
            {
                team.Add(Combatant.ForMinion(database.FindMinion(m.definitionId), m, database.Combat, database.Equipment));
            }
            return team;
        }

        /// <summary>The team's power, for the "Team / Rec." line.</summary>
        public static float GetPower(GameState state, GameDatabase database) =>
            CombatSimulator.GetTeamPower(Build(state, database), database.Combat);
    }
}
