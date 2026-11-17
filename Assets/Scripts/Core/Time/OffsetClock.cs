using System;

namespace Deepshaft.Time
{
    /// <summary>
    /// Another clock plus a fixed offset. For debug tools that skip time forward in a real
    /// build ("+1 h") without changing the device clock.
    /// </summary>
    public sealed class OffsetClock : IClock
    {
        readonly IClock inner;

        public OffsetClock(IClock inner, TimeSpan offset = default)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Offset = offset < TimeSpan.Zero ? TimeSpan.Zero : offset;
        }

        public TimeSpan Offset { get; private set; }

        public DateTime UtcNow => inner.UtcNow + Offset;

        /// <summary>Skips time forward. Time never goes backwards.</summary>
        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount), "Time can't go backwards.");
            Offset += amount;
        }
    }
}
