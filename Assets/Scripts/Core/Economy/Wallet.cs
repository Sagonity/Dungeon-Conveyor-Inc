using System;
using System.Collections.Generic;

namespace Deepshaft.Economy
{
    /// <summary>
    /// Reads and changes resource balances. All changes go through here so the UI
    /// can listen to <see cref="Changed"/> and so balances can never go negative.
    /// </summary>
    public class Wallet
    {
        readonly WalletData data;

        /// <summary>Raised after a balance changes. Args: resource, new balance.</summary>
        public event Action<ResourceId, long> Changed;

        public Wallet(WalletData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (this.data.balances == null) this.data.balances = new List<ResourceAmount>();
        }

        public long Get(ResourceId id)
        {
            int index = IndexOf(id);
            return index >= 0 ? data.balances[index].amount : 0;
        }

        public void Add(ResourceId id, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove resources.");
            if (amount == 0) return;
            Set(id, checked(Get(id) + amount));
        }

        public void Add(IEnumerable<ResourceAmount> amounts)
        {
            foreach (ResourceAmount a in amounts) Add(a.id, a.amount);
        }

        public bool CanAfford(IEnumerable<ResourceAmount> cost)
        {
            foreach (KeyValuePair<ResourceId, long> c in Combine(cost))
            {
                if (Get(c.Key) < c.Value) return false;
            }
            return true;
        }

        /// <summary>Spends the whole cost, or nothing at all if any part is missing.</summary>
        public bool TrySpend(IEnumerable<ResourceAmount> cost)
        {
            Dictionary<ResourceId, long> combined = Combine(cost);
            foreach (KeyValuePair<ResourceId, long> c in combined)
            {
                if (Get(c.Key) < c.Value) return false;
            }
            foreach (KeyValuePair<ResourceId, long> c in combined)
            {
                if (c.Value > 0) Set(c.Key, Get(c.Key) - c.Value);
            }
            return true;
        }

        public bool TrySpend(ResourceId id, long amount) =>
            TrySpend(new[] { new ResourceAmount(id, amount) });

        /// <summary>How much more of each resource is needed to afford the cost (empty if affordable).</summary>
        public List<ResourceAmount> GetShortfall(IEnumerable<ResourceAmount> cost)
        {
            var missing = new List<ResourceAmount>();
            foreach (KeyValuePair<ResourceId, long> c in Combine(cost))
            {
                long have = Get(c.Key);
                if (have < c.Value) missing.Add(new ResourceAmount(c.Key, c.Value - have));
            }
            return missing;
        }

        void Set(ResourceId id, long amount)
        {
            int index = IndexOf(id);
            if (index >= 0) data.balances[index] = new ResourceAmount(id, amount);
            else data.balances.Add(new ResourceAmount(id, amount));
            Changed?.Invoke(id, amount);
        }

        int IndexOf(ResourceId id)
        {
            for (int i = 0; i < data.balances.Count; i++)
            {
                if (data.balances[i].id == id) return i;
            }
            return -1;
        }

        // A cost may list the same resource twice; treat it as the sum.
        static Dictionary<ResourceId, long> Combine(IEnumerable<ResourceAmount> cost)
        {
            if (cost == null) throw new ArgumentNullException(nameof(cost));
            var combined = new Dictionary<ResourceId, long>();
            foreach (ResourceAmount c in cost)
            {
                if (c.amount < 0) throw new ArgumentOutOfRangeException(nameof(cost), "Costs can't be negative: " + c);
                combined.TryGetValue(c.id, out long sum);
                combined[c.id] = checked(sum + c.amount);
            }
            return combined;
        }
    }
}
