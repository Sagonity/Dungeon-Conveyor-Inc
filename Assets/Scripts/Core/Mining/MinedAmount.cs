using System;
using Deepshaft.Economy;

namespace Deepshaft.Mining
{
    /// <summary>
    /// An amount of one resource that can be fractional: a production rate per hour, or
    /// mined output that hasn't been collected yet. Whole units go to the
    /// <see cref="Wallet"/> on collect; the fraction stays so nothing is lost.
    /// </summary>
    [Serializable]
    public struct MinedAmount
    {
        public ResourceId id;
        public double amount;

        public MinedAmount(ResourceId id, double amount)
        {
            this.id = id;
            this.amount = amount;
        }

        public override string ToString() => amount.ToString("0.###") + " " + id;
    }
}
