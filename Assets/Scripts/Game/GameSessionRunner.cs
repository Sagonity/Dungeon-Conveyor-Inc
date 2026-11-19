using System;
using System.Globalization;
using Deepshaft.Content;
using Deepshaft.Save;
using Deepshaft.Session;
using Deepshaft.Time;
using UnityEngine;

/// <summary>
/// Runs the <see cref="GameSession"/> in the app (SCRUM-30). Created automatically before the
/// first scene loads, together with the grey-box screen, and kept across scenes.
///
/// - Loads the GameDatabase from Resources/GameDatabase.
/// - Saves every <see cref="autosaveSeconds"/>, when the app goes to the background and on quit.
/// - Debug: skips time forward with an <see cref="OffsetClock"/> (kept between launches) and resets the save.
///
/// Time comes from the device clock for now. The team decided the game will use a server
/// clock (Scope decision 20); that becomes another <see cref="IClock"/> here.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameSessionRunner : MonoBehaviour
{
    const string DatabaseResource = "GameDatabase";
    const string ClockOffsetKey = "debug.clockOffsetMs";

    [Tooltip("Loaded from Resources/GameDatabase if empty.")]
    [SerializeField] GameDatabase database;
    [SerializeField, Min(5f)] float autosaveSeconds = 30f;

    float nextAutosave;

    public static GameSessionRunner Instance { get; private set; }

    /// <summary>The running game. Null if the GameDatabase couldn't be found.</summary>
    public GameSession Session { get; private set; }

    public OffsetClock Clock { get; private set; }

    /// <summary>Raised when a session starts (first launch and after a reset).</summary>
    public event Action<GameSession> SessionStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("Game Session");
        DontDestroyOnLoad(go);
        go.AddComponent<GameSessionRunner>();
        go.AddComponent<GreyboxGameScreen>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (database == null) database = Resources.Load<GameDatabase>(DatabaseResource);
        if (database == null)
        {
            Debug.LogError("GameSessionRunner: no GameDatabase at Resources/" + DatabaseResource + ". The game can't start.", this);
            enabled = false;
            return;
        }

        Clock = new OffsetClock(SystemClock.Instance, LoadClockOffset());
        StartSession();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Session == null || Time.unscaledTime < nextAutosave) return;
        nextAutosave = Time.unscaledTime + autosaveSeconds;
        Session.Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Session?.Save();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) Session?.Save();
    }

    void OnApplicationQuit() => Session?.Save();

    void StartSession()
    {
        try
        {
            Session = new GameSession(database, FileSaveStorage.CreateDefault(), Clock);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            Session = null;
            return;
        }

        if (Session.LoadStatus == LoadStatus.Corrupt)
        {
            Debug.LogError("The save was damaged; a new game was started.", this);
        }
        nextAutosave = Time.unscaledTime + autosaveSeconds;
        SessionStarted?.Invoke(Session);
    }

    // ---------- Debug ----------

    /// <summary>Debug: moves the game clock forward, as if the player was away.</summary>
    public void DebugSkipTime(TimeSpan amount)
    {
        if (Session == null || amount <= TimeSpan.Zero) return;
        Session.Playtest.Add("debug_skip", "hours=" + amount.TotalHours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        Session.Save();
        Clock.Advance(amount);
        SaveClockOffset(Clock.Offset);
    }

    /// <summary>Debug: deletes the save and starts a new game with the real clock.</summary>
    public void DebugResetGame()
    {
        if (database == null) return;
        Session?.DeleteSave();
        SaveClockOffset(TimeSpan.Zero);
        Clock = new OffsetClock(SystemClock.Instance);
        StartSession();
    }

    static TimeSpan LoadClockOffset()
    {
        string text = PlayerPrefs.GetString(ClockOffsetKey, "0");
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms) && ms > 0
            ? TimeSpan.FromMilliseconds(ms)
            : TimeSpan.Zero;
    }

    static void SaveClockOffset(TimeSpan offset)
    {
        PlayerPrefs.SetString(ClockOffsetKey, ((long)offset.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}
