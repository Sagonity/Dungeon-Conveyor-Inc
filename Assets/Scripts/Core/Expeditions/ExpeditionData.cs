using System;
using System.Collections.Generic;
using Deepshaft.Mining;

namespace Deepshaft.Expeditions
{
    /// <summary>
    /// Saved Expedition progress. Like the Mine, a running Expedition never stores a total that
    /// counts up every frame: it stores when it was last banked, and <see cref="ExpeditionService"/>
    /// works out the rest from the clock (Technical Architecture §4).
    /// </summary>
    [Serializable]
    public class ExpeditionData
    {
        /// <summary>Ids of Expeditions whose first clear is done. Their automation is unlocked for good.</summary>
        public List<string> cleared = new List<string>();

        /// <summary>The automated Expedition that's running, or empty. Only one at a time.</summary>
        public string activeId = "";

        /// <summary>When the running Expedition was started.</summary>
        public long startedAtUnixMs;

        /// <summary>Output of the running Expedition up to this time is in <see cref="pending"/>.</summary>
        public long lastBankedAtUnixMs;

        /// <summary>
        /// Earned and not collected yet. Stopping collects the whole units; fractions stay here
        /// for the next run so nothing is lost.
        /// </summary>
        public List<MinedAmount> pending = new List<MinedAmount>();
    }
}
