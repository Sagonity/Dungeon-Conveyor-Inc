using System;
using System.Collections.Generic;

namespace Deepshaft.Mining
{
    /// <summary>
    /// Saved Mine state. The Mine never stores a running total that counts up every frame:
    /// it stores what was banked and when, and <see cref="MiningService"/> works out the rest
    /// from the clock (Technical Architecture §4).
    /// </summary>
    [Serializable]
    public class MiningData
    {
        /// <summary>
        /// Output up to this time is in <see cref="pending"/>. 0 means "not started yet":
        /// the Mine starts counting the first time a <see cref="MiningService"/> sees it.
        /// </summary>
        public long lastBankedAtUnixMs;

        /// <summary>
        /// Mined but not collected yet. No cap (team decision): it keeps adding up
        /// for as long as the player is away.
        /// </summary>
        public List<MinedAmount> pending = new List<MinedAmount>();
    }
}
