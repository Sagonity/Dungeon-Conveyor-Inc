using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Screen state controller. Owns which view is visible and crossfades between them.
/// Game logic hooks in through the UnityEvents (wired in the Inspector) or the
/// StateChanged C# event, so this class never references game code directly.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Views (auto-found in children if empty)")]
    [SerializeField] private MainMenuView mainMenuView;
    [SerializeField] private GameHUDView gameView;
    [SerializeField] private SettingsView settingsView;

    [Header("Transitions")]
    [SerializeField] private UIState initialState = UIState.MainMenu;
    [Tooltip("Seconds for a full fade between screens. 0 = instant.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    [Header("Events")]
    [Tooltip("Invoked when entering the Game state (e.g. GameController.ResumeGameplay).")]
    [SerializeField] private UnityEvent onEnterGame = new UnityEvent();
    [Tooltip("Invoked when leaving the Game state, and at startup if not starting in Game (e.g. GameController.PauseGameplay).")]
    [SerializeField] private UnityEvent onExitGame = new UnityEvent();
    [SerializeField] private UnityEvent<UIState> onStateChanged = new UnityEvent<UIState>();

    public UIState CurrentState { get; private set; }

    /// <summary>Raised on every state change. Args: previous state, new state.</summary>
    public event Action<UIState, UIState> StateChanged;

    public UnityEvent OnEnterGame => onEnterGame;
    public UnityEvent OnExitGame => onExitGame;

    void Awake()
    {
        if (mainMenuView == null) mainMenuView = GetComponentInChildren<MainMenuView>(true);
        if (gameView == null) gameView = GetComponentInChildren<GameHUDView>(true);
        if (settingsView == null) settingsView = GetComponentInChildren<SettingsView>(true);

        foreach (UIState state in Enum.GetValues(typeof(UIState)))
        {
            UIView view = GetView(state);
            if (view == null)
            {
                Debug.LogError("UIManager: no view assigned for state " + state, this);
                continue;
            }
            view.Initialize(this);
            if (state == initialState) view.Show(0f);
            else view.Hide(0f);
        }

        CurrentState = initialState;
    }

    void Start()
    {
        // Runs after every Awake, so listeners like GameController are ready.
        if (initialState == UIState.Game) onEnterGame.Invoke();
        else onExitGame.Invoke();
    }

    public void SetState(UIState newState)
    {
        if (newState == CurrentState) return;

        UIState previous = CurrentState;
        CurrentState = newState;

        GetView(previous)?.Hide(fadeDuration);
        GetView(newState)?.Show(fadeDuration);

        if (newState == UIState.Game) onEnterGame.Invoke();
        else if (previous == UIState.Game) onExitGame.Invoke();

        StateChanged?.Invoke(previous, newState);
        onStateChanged.Invoke(newState);
    }

    // Parameterless helpers so states can also be set from Button.onClick in the Inspector.
    public void ShowMainMenu() => SetState(UIState.MainMenu);
    public void ShowGame() => SetState(UIState.Game);
    public void ShowSettings() => SetState(UIState.Settings);

    UIView GetView(UIState state)
    {
        switch (state)
        {
            case UIState.MainMenu: return mainMenuView;
            case UIState.Game: return gameView;
            case UIState.Settings: return settingsView;
            default: return null;
        }
    }
}
