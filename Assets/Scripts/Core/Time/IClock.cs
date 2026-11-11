using System;

namespace Deepshaft.Time
{
    /// <summary>
    /// The current real-world time. Everything that runs "in the background"
    /// (Mining, Expeditions, Guardian Ticket regeneration) reads time from here,
    /// never from UnityEngine.Time, so it keeps going while menus are open and
    /// while the app is closed. Tests swap in a <see cref="ManualClock"/>.
    /// </summary>
    public interface IClock
    {
        /// <summary>Current time in UTC.</summary>
        DateTime UtcNow { get; }
    }
}
