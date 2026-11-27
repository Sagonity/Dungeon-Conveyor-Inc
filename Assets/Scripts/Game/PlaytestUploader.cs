using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Deepshaft.Session;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Web playtest (SCRUM-35): sends the playtest log to the website, tagged with the tester's code,
/// so remote testers don't have to copy and paste it.
///
/// - Only active when the game was opened with a tester code: <c>…/playtest/game/index.html?code=DS-7K4Q</c>
///   (the /playtest page adds it after checking the code). Otherwise it does nothing, so phone builds
///   keep using Copy log.
/// - Every <see cref="uploadEverySeconds"/> seconds, and when the tab is hidden, it posts the log lines
///   that haven't been sent yet to <c>/api/playtest/log</c> on the same site.
/// - It remembers the last line it sent (in PlayerPrefs), so lines are sent once, also after a reload.
/// - When the tab is hidden it sends with the browser's sendBeacon (SCRUM-39), which finishes even if the
///   tab closes, and marks the lines as sent straight away so they aren't sent twice.
/// - Testers don't see Settings → Debug (SCRUM-39) unless the URL also has <c>debug=1</c>.
/// </summary>
public class PlaytestUploader : MonoBehaviour
{
    const string Endpoint = "/api/playtest/log";
    const int MaxEventsPerUpload = 500;

    [SerializeField, Min(10f)] float uploadEverySeconds = 60f;

    public static PlaytestUploader Instance { get; private set; }

    /// <summary>The tester code from the page URL, or "" when not running as a web playtest.</summary>
    public string Code { get; private set; } = "";
    public bool Active => !string.IsNullOrEmpty(Code);

    /// <summary>
    /// False for testers (a tester code without <c>debug=1</c>): the grey-box screen then hides Settings → Debug.
    /// True in the editor, phone builds and the web build without a code.
    /// </summary>
    public bool DebugAllowed { get; private set; } = true;

    /// <summary>For the Settings panel: when the last upload succeeded (UTC), and the last error.</summary>
    public DateTime? LastUploadUtc { get; private set; }
    public string LastError { get; private set; } = "";
    public int PendingCount => Active && Log != null ? CollectUnsent(Log).Count : 0;

    string origin = "";
    string device = "";
    bool uploading;
    float nextUpload;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void DeepshaftListenForPageHidden(string objectName);
    [DllImport("__Internal")] static extern int DeepshaftSendBeacon(string url, string body);
#else
    static void DeepshaftListenForPageHidden(string objectName) { }
    static int DeepshaftSendBeacon(string url, string body) => 0;
#endif

    static PlaytestLog Log => GameSessionRunner.Instance != null ? GameSessionRunner.Instance.Session?.Playtest : null;

    void Awake()
    {
        Instance = this;
        string url = Application.absoluteURL ?? "";
        Code = ReadCode(url);
        DebugAllowed = !Active || ReadFlag(url, "debug");
        if (!Active) return;

        origin = ReadOrigin(url);
        device = PlayerPrefs.GetString("playtest.device", "");
        if (string.IsNullOrEmpty(device))
        {
            device = Guid.NewGuid().ToString("N").Substring(0, 8);
            PlayerPrefs.SetString("playtest.device", device);
            PlayerPrefs.Save();
        }
        DeepshaftListenForPageHidden(gameObject.name);
        nextUpload = Time.unscaledTime + 5f; // first upload soon after start (the new_game / session_start line)
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!Active || uploading || Time.unscaledTime < nextUpload) return;
        nextUpload = Time.unscaledTime + uploadEverySeconds;
        UploadNow();
    }

    /// <summary>Called by the browser plugin when the tab is hidden: save, then send what's new.</summary>
    public void OnWebPageHidden()
    {
        GameSessionRunner.Instance?.Session?.Save();
        if (!Active) return;
        if (!SendWithBeacon() && !uploading) UploadNow();
    }

    /// <summary>
    /// Sends the unsent lines with sendBeacon, which the browser completes even while the tab closes, and marks
    /// them as sent at once (a beacon gets no answer). False if there was nothing to send or no beacon support.
    /// </summary>
    bool SendWithBeacon()
    {
        PlaytestLog log = Log;
        if (log == null) return false;
        List<PlaytestEvent> unsent = CollectUnsent(log);
        if (unsent.Count == 0) return true;
        if (unsent.Count > MaxEventsPerUpload) unsent = unsent.GetRange(0, MaxEventsPerUpload);
        if (DeepshaftSendBeacon(origin + Endpoint, BuildBody(unsent)) != 1) return false;
        MarkSent(unsent);
        return true;
    }

    /// <summary>Sends the unsent log lines now (also from the Settings panel).</summary>
    public void UploadNow()
    {
        PlaytestLog log = Log;
        if (!Active || log == null || uploading) return;
        List<PlaytestEvent> unsent = CollectUnsent(log);
        if (unsent.Count == 0) return;
        if (unsent.Count > MaxEventsPerUpload) unsent = unsent.GetRange(0, MaxEventsPerUpload);
        StartCoroutine(Send(unsent));
    }

    IEnumerator Send(List<PlaytestEvent> events)
    {
        uploading = true;
        using (var request = new UnityWebRequest(origin + Endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(BuildBody(events)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // A beacon may have sent later lines meanwhile: never move the marker backwards.
                if (IsAfterMarker(events[events.Count - 1])) MarkSent(events);
                LastUploadUtc = DateTime.UtcNow;
                LastError = "";
            }
            else
            {
                LastError = request.responseCode > 0 ? "HTTP " + request.responseCode : request.error;
            }
        }
        uploading = false;
    }

    string SentKey => "playtest.lastSent." + Code;

    string BuildBody(List<PlaytestEvent> events)
    {
        var body = new UploadBody { code = Code, device = device, version = Application.version };
        foreach (PlaytestEvent e in events) body.events.Add(new UploadEvent { t = e.atUnixMs, type = e.type, detail = e.detail });
        return JsonUtility.ToJson(body);
    }

    void MarkSent(List<PlaytestEvent> events)
    {
        PlayerPrefs.SetString(SentKey, Signature(events[events.Count - 1]));
        PlayerPrefs.Save();
        LastUploadUtc = DateTime.UtcNow;
    }

    /// <summary>True if this line comes after the last line marked as sent (or nothing is marked).</summary>
    bool IsAfterMarker(PlaytestEvent e)
    {
        PlaytestLog log = Log;
        if (log == null) return true;
        foreach (PlaytestEvent unsent in CollectUnsent(log)) if (unsent.atUnixMs == e.atUnixMs && Signature(unsent) == Signature(e)) return true;
        return false;
    }

    /// <summary>
    /// The log lines after the last one that was sent. If that line isn't in the log any more (a reset
    /// started a new log, or old lines were dropped), everything in the log is sent.
    /// </summary>
    List<PlaytestEvent> CollectUnsent(PlaytestLog log)
    {
        IReadOnlyList<PlaytestEvent> all = log.Events;
        string last = PlayerPrefs.GetString(SentKey, "");
        int start = 0;
        if (!string.IsNullOrEmpty(last))
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (Signature(all[i]) == last) { start = i + 1; break; }
            }
        }
        var unsent = new List<PlaytestEvent>();
        for (int i = start; i < all.Count; i++) unsent.Add(all[i]);
        return unsent;
    }

    static string Signature(PlaytestEvent e) => e.atUnixMs + "|" + e.type + "|" + e.detail;

    /// <summary>The "code" query parameter, upper-cased, letters, digits and dashes only (max 32).</summary>
    public static string ReadCode(string url)
    {
        int q = url.IndexOf('?');
        if (q < 0) return "";
        foreach (string pair in url.Substring(q + 1).Split('&', '#'))
        {
            if (!pair.StartsWith("code=", StringComparison.OrdinalIgnoreCase)) continue;
            string value = Uri.UnescapeDataString(pair.Substring(5)).Trim().ToUpperInvariant();
            var clean = new StringBuilder();
            foreach (char c in value) if (char.IsLetterOrDigit(c) || c == '-') clean.Append(c);
            return clean.Length <= 32 ? clean.ToString() : clean.ToString(0, 32);
        }
        return "";
    }

    /// <summary>True if the URL's query has <paramref name="name"/>=1 (e.g. debug=1).</summary>
    public static bool ReadFlag(string url, string name)
    {
        int q = url.IndexOf('?');
        if (q < 0) return false;
        foreach (string pair in url.Substring(q + 1).Split('&', '#'))
        {
            if (string.Equals(pair, name + "=1", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>"https://site.example" from "https://site.example/playtest/game/index.html?…".</summary>
    public static string ReadOrigin(string url)
    {
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) return "";
        int path = url.IndexOf('/', scheme + 3);
        return path < 0 ? url : url.Substring(0, path);
    }

    [Serializable]
    class UploadBody
    {
        public string code;
        public string device;
        public string version;
        public List<UploadEvent> events = new List<UploadEvent>();
    }

    [Serializable]
    class UploadEvent
    {
        public long t;
        public string type;
        public string detail;
    }
}
