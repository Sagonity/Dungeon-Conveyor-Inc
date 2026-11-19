using System;

namespace Deepshaft.Smith
{
    /// <summary>The Smith's saved progress. Changed through <see cref="SmithService"/>.</summary>
    [Serializable]
    public class SmithData
    {
        /// <summary>Smith ranks learned with boss materials (0 at start). Rank N allows gear breakthrough N.</summary>
        public int rank;
    }
}
