using System;
using Deepshaft.Time;
using NUnit.Framework;

namespace Deepshaft.Tests
{
    public class ClockTests
    {
        [Test]
        public void ManualClock_OnlyMovesWhenAdvanced()
        {
            var start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            var clock = new ManualClock(start);

            Assert.AreEqual(start, clock.UtcNow);
            clock.Advance(TimeSpan.FromMinutes(10));
            Assert.AreEqual(start.AddMinutes(10), clock.UtcNow);
        }

        [Test]
        public void ManualClock_CannotGoBackwards()
        {
            var clock = new ManualClock(DateTime.UtcNow);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
        }

        [Test]
        public void UnixTime_RoundTrips()
        {
            var time = new DateTime(2026, 11, 11, 23, 59, 30, 123, DateTimeKind.Utc);
            long ms = UnixTime.ToUnixMs(time);

            Assert.AreEqual(time, UnixTime.FromUnixMs(ms));
            Assert.AreEqual(DateTimeKind.Utc, UnixTime.FromUnixMs(ms).Kind);
        }
    }
}
