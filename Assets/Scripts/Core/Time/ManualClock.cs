using System;

namespace Deepshaft.Time
{
    /// <summary>
    /// A clock that only moves when told to. For tests, and for debug tools
    /// that fast-forward timers.
    /// </summary>
    public sealed class ManualClock : IClock
    {
        public ManualClock(DateTime startUtc)
        {
            UtcNow = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get; private set; }

        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount), "Time can't go backwards.");
            UtcNow += amount;
        }

        public void Set(DateTime utc)
        {
            UtcNow = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }
    }
}
