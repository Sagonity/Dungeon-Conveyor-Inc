using System;
using UnityEngine;

/// <summary>
/// Pauses and resumes gameplay when the UI enters or leaves the Game state. Knows nothing about
/// the UI: UIManager calls <see cref="ResumeGameplay"/> / <see cref="PauseGameplay"/>.
///
/// The old summon prototype (scene objects with MinionStats / MinionHealth) was removed in SCRUM-10;
/// Minions are save data now (<c>OwnedMinion</c>) and summoning comes with SCRUM-11.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Pause")]
    [Tooltip("Set Time.timeScale to 0 while gameplay is inactive (menus open). Game systems run on IClock, not game time.")]
    [SerializeField] private bool pauseTimeWhenInactive = true;

    public bool IsGameplayActive { get; private set; }

    /// <summary>Raised when gameplay is paused or resumed.</summary>
    public event Action<bool> GameplayActiveChanged;

    void OnDestroy()
    {
        // Don't leave the game frozen if this object goes away while paused.
        if (pauseTimeWhenInactive)
        {
            Time.timeScale = 1f;
        }
    }

    public void ResumeGameplay() => SetGameplayActive(true);

    public void PauseGameplay() => SetGameplayActive(false);

    public void SetGameplayActive(bool active)
    {
        IsGameplayActive = active;
        if (pauseTimeWhenInactive)
        {
            Time.timeScale = active ? 1f : 0f;
        }
        GameplayActiveChanged?.Invoke(active);
    }
}
