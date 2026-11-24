using System;
using UnityEngine;

/// <summary>
/// Tracks whether the player is in the game or in a menu. UIManager calls
/// <see cref="ResumeGameplay"/> / <see cref="PauseGameplay"/> when the UI enters or leaves the Game state.
///
/// It no longer freezes Unity's game time while a menu is open (SCRUM-33): every game system runs on
/// <c>IClock</c> (real UTC time), and autosave and UI fades use unscaled time, so nothing depended on it.
/// The old summon prototype (MinionStats / MinionHealth) was removed in SCRUM-10.
/// </summary>
public class GameController : MonoBehaviour
{
    public bool IsGameplayActive { get; private set; }

    /// <summary>Raised when gameplay is paused or resumed.</summary>
    public event Action<bool> GameplayActiveChanged;

    public void ResumeGameplay() => SetGameplayActive(true);

    public void PauseGameplay() => SetGameplayActive(false);

    public void SetGameplayActive(bool active)
    {
        IsGameplayActive = active;
        GameplayActiveChanged?.Invoke(active);
    }
}
