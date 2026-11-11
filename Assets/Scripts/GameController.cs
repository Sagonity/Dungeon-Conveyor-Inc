using System;
using UnityEngine;

/// <summary>
/// Game logic entry point for the UI. Knows nothing about the UI: the UI calls
/// the public methods and listens to the C# events.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Summoning")]
    [Tooltip("Minion to spawn when summoning. Should have MinionStats and MinionHealth.")]
    [SerializeField] private GameObject minionPrefab;
    [Tooltip("Optional parent for spawned minions. Its position is where the first minion appears.")]
    [SerializeField] private Transform minionParent;
    [Tooltip("Placeholder layout until the roster screen exists: minions are laid out in rows.")]
    [SerializeField, Min(1)] private int minionsPerRow = 4;
    [SerializeField, Min(0.1f)] private float minionSpacing = 1.5f;

    [Header("Pause")]
    [Tooltip("Set Time.timeScale to 0 while gameplay is inactive (menus open).")]
    [SerializeField] private bool pauseTimeWhenInactive = true;

    public bool IsGameplayActive { get; private set; }
    public int SummonedCount { get; private set; }

    /// <summary>Raised after a minion is spawned. Args: the minion, total summoned so far.</summary>
    public event Action<GameObject, int> MinionSummoned;
    /// <summary>Raised when a summon request can't be fulfilled. Arg: reason.</summary>
    public event Action<string> SummonFailed;
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

    /// <summary>
    /// Spawns a placeholder minion. Returns true on success.
    /// Temporary: summoning becomes a roster/gacha operation paid with Summon Shards (SCRUM-11).
    /// </summary>
    public bool SummonMinion()
    {
        if (!IsGameplayActive)
        {
            return Fail("The game is paused.");
        }
        if (minionPrefab == null)
        {
            return Fail("No minion prefab is assigned on GameController.");
        }

        GameObject minion = Instantiate(minionPrefab, GetSpawnPosition(SummonedCount), Quaternion.identity, minionParent);

        SummonedCount++;
        MinionStats stats = minion.GetComponent<MinionStats>();
        string baseName = stats != null ? stats.minionName : "Minion";
        minion.name = baseName + " #" + SummonedCount;

        MinionSummoned?.Invoke(minion, SummonedCount);
        return true;
    }

    Vector3 GetSpawnPosition(int index)
    {
        Vector3 origin = minionParent != null ? minionParent.position : Vector3.zero;
        int column = index % minionsPerRow;
        int row = index / minionsPerRow;
        return origin + new Vector3(column * minionSpacing, -row * minionSpacing, 0f);
    }

    bool Fail(string reason)
    {
        Debug.LogWarning("Summon failed: " + reason, this);
        SummonFailed?.Invoke(reason);
        return false;
    }
}
