using System;

namespace Deepshaft.Minions
{
    /// <summary>
    /// One Minion the player owns. Plain save data: the definition is looked up by
    /// <see cref="definitionId"/> through <see cref="Content.GameDatabase"/>.
    /// </summary>
    [Serializable]
    public class OwnedMinion
    {
        /// <summary>Unique per save. Assigned by <see cref="State.GameState.AddMinion"/>.</summary>
        public int instanceId;
        public string definitionId = "";
        public int level = 1;

        /// <summary>
        /// Rare-material breakthroughs done (v3.4 §5). Each one raises the level cap.
        /// Replaces the old star rank; duplicates now become Minion Essence instead.
        /// </summary>
        public int breakthroughs;
    }
}
