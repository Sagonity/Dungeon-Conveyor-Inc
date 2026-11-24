using System;
using System.Collections.Generic;

namespace Deepshaft.Economy
{
    /// <summary>
    /// The result of buying several levels in one go (SCRUM-32 "Max" buttons): how many levels,
    /// what they cost in total, and the level after buying. A check returns what a buy would do.
    /// </summary>
    public sealed class BulkPurchase
    {
        public BulkPurchase(int levels, IReadOnlyList<ResourceAmount> cost, int fromLevel, int toLevel)
        {
            Levels = Math.Max(0, levels);
            Cost = cost ?? Array.Empty<ResourceAmount>();
            FromLevel = fromLevel;
            ToLevel = toLevel;
        }

        public static readonly BulkPurchase None = new BulkPurchase(0, null, 0, 0);

        /// <summary>How many levels are (or would be) bought. 0 = nothing affordable.</summary>
        public int Levels { get; }
        public IReadOnlyList<ResourceAmount> Cost { get; }
        public int FromLevel { get; }
        public int ToLevel { get; }

        /// <summary>
        /// Adds up level costs, one level at a time, while the running total is still affordable and
        /// <paramref name="costOfLevel"/> returns a cost (null = at the cap). Doesn't spend anything.
        /// </summary>
        public static BulkPurchase Plan(int currentLevel, Func<int, List<ResourceAmount>> costOfLevel, Wallet wallet, int maxLevels = 1000)
        {
            if (costOfLevel == null) throw new ArgumentNullException(nameof(costOfLevel));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            var total = new Dictionary<ResourceId, long>();
            int levels = 0;
            int level = currentLevel;
            while (levels < maxLevels)
            {
                List<ResourceAmount> next = costOfLevel(level);
                if (next == null) break;
                var trial = new Dictionary<ResourceId, long>(total);
                foreach (ResourceAmount c in next)
                {
                    trial.TryGetValue(c.id, out long sum);
                    trial[c.id] = sum + c.amount;
                }
                if (!wallet.CanAfford(ToList(trial))) break;
                total = trial;
                levels++;
                level++;
            }
            return new BulkPurchase(levels, ToList(total), currentLevel, level);
        }

        static List<ResourceAmount> ToList(Dictionary<ResourceId, long> sums)
        {
            var list = new List<ResourceAmount>();
            foreach (KeyValuePair<ResourceId, long> s in sums) if (s.Value > 0) list.Add(new ResourceAmount(s.Key, s.Value));
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }
    }
}
