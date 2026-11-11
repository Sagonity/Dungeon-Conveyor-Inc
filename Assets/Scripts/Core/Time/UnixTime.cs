using System;

namespace Deepshaft.Time
{
    /// <summary>
    /// Converts between DateTime and the Unix milliseconds stored in save files.
    /// Save data stores times as <c>long</c> because JsonUtility can't serialize DateTime.
    /// </summary>
    public static class UnixTime
    {
        public static long ToUnixMs(DateTime utc) =>
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        public static DateTime FromUnixMs(long unixMs) =>
            DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
    }
}
