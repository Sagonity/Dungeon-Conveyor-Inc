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
#else
    static void DeepshaftListenForPageHidden(string objectName) { }
#endif

    static PlaytestLog Log => GameSessionRunner.Instance != null ? GameSessionRunner.Instance.Session?.Playtest : null;

    void Awake()
    {
        Instance = this;
        string url = Application.absoluteURL ?? "";
        Code = ReadCode(url);
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
        if (Active && !uploading) UploadNow();
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
        var body = new UploadBody { code = Code, device = device, version = Application.version };
        foreach (PlaytestEvent e in events) body.events.Add(new UploadEvent { t = e.atUnixMs, type = e.type, detail = e.detail });

        using (var request = new UnityWebRequest(origin + Endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                PlayerPrefs.SetString(SentKey, Signature(events[events.Count - 1]));
                PlayerPrefs.Save();
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
