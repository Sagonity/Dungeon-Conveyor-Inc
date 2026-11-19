using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Deepshaft.Time;

namespace Deepshaft.Session
{
    /// <summary>One thing the player did or saw, for the playtest (SCRUM-8).</summary>
    [Serializable]
    public class PlaytestEvent
    {
        public long atUnixMs;
        /// <summary>Short machine-friendly name, e.g. "boss_fight", "level_up", "tab".</summary>
        public string type = "";
        /// <summary>Human-readable detail, e.g. "spore_foreman lost power=72 rec=80".</summary>
        public string detail = "";
    }

    /// <summary>Saved playtest log. Kept in the save so it survives app restarts.</summary>
    [Serializable]
    public class PlaytestLogData
    {
        public List<PlaytestEvent> events = new List<PlaytestEvent>();
    }

    /// <summary>
    /// A local event log for the core loop playtest (SCRUM-8). Every <see cref="GameSession"/> action
    /// (and the tab the player opens) is recorded with the time, so the facilitator can check the
    /// success criteria afterwards: what the tester did after losing a fight, whether each session
    /// ended with an upgrade, what happened after a long break.
    ///
    /// Stays on the device: nothing is sent anywhere. Settings → Debug copies it as CSV.
    /// Keeps the newest <see cref="MaxEvents"/> events. Replaced by real analytics in SCRUM-17.
    /// </summary>
    public class PlaytestLog
    {
        public const int MaxEvents = 2000;

        readonly PlaytestLogData data;
        readonly IClock clock;

        public PlaytestLog(PlaytestLogData data, IClock clock)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (this.data.events == null) this.data.events = new List<PlaytestEvent>();
        }

        public IReadOnlyList<PlaytestEvent> Events => data.events;

        public void Add(string type, string detail = "")
        {
            data.events.Add(new PlaytestEvent
            {
                atUnixMs = UnixTime.ToUnixMs(clock.UtcNow),
                type = type ?? "",
                detail = detail ?? "",
            });
            int extra = data.events.Count - MaxEvents;
            if (extra > 0) data.events.RemoveRange(0, extra);
        }

        public void Clear() => data.events.Clear();

        /// <summary>
        /// The log as CSV: UTC time, minutes since the first event, type, detail.
        /// The clock includes debug time skips, so a skipped break shows up as a gap.
        /// </summary>
        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.Append("utc,minutes,type,detail\n");
            long first = data.events.Count > 0 ? data.events[0].atUnixMs : 0;
            foreach (PlaytestEvent e in data.events)
            {
                sb.Append(UnixTime.FromUnixMs(e.atUnixMs).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(((e.atUnixMs - first) / 60000.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(Csv(e.type)).Append(',');
                sb.Append(Csv(e.detail)).Append('\n');
            }
            return sb.ToString();
        }

        static string Csv(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
