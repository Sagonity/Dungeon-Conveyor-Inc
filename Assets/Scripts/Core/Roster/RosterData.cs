using System;
using System.Collections.Generic;

namespace Deepshaft.Roster
{
    /// <summary>
    /// Saved assignment: which owned Minions work in the Mine and which form the Adventure team.
    /// Entries are <see cref="Minions.OwnedMinion.instanceId"/> values, in slot order.
    /// Read and changed through <see cref="RosterService"/> (and <see cref="Mining.MiningService"/>
    /// for the Miners it reads).
    /// </summary>
    [Serializable]
    public class RosterData
    {
        /// <summary>Miners in the Mine. Only these mine (SCRUM-6).</summary>
        public List<int> miners = new List<int>();

        /// <summary>The Adventure team used for boss fights and Expedition first clears.</summary>
        public List<int> adventureTeam = new List<int>();

        /// <summary>
        /// False for a new game and for saves from before SCRUM-6. The first time a roster
        /// is used, empty slots are filled from owned Minions so old saves keep mining.
        /// </summary>
        public bool initialized;
    }
}
