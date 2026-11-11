using System;

namespace Deepshaft.Economy
{
    /// <summary>An amount of one resource. Used for wallet contents, costs and rewards.</summary>
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceId id;
        public long amount;

        public ResourceAmount(ResourceId id, long amount)
        {
            this.id = id;
            this.amount = amount;
        }

        public override string ToString() => amount + " " + id;
    }
}
