using System;

namespace Deepshaft.Time
{
    /// <summary>The device clock. Use <see cref="Instance"/> in the game.</summary>
    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        SystemClock() { }

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
