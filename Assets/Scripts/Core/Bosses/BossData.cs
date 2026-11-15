using System;
using System.Collections.Generic;

namespace Deepshaft.Bosses
{
    /// <summary>Saved boss progress. Changed through <see cref="BossService"/>.</summary>
    [Serializable]
    public class BossData
    {
        /// <summary>Ids of bosses beaten at least once. Permanent.</summary>
        public List<string> defeated = new List<string>();
    }
}
