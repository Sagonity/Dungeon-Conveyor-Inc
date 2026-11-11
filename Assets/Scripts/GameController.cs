using System;
using UnityEngine;

/// <summary>
/// Game logic entry point for the UI. Knows nothing about the UI: the UI calls
/// the public methods and listens to the C# events.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Summoning")]
    [Tooltip("Minion to spawn when summoning. Should have MinionStats, MinionHealth and DraggableMinion.")]
    [SerializeField] private GameObject minionPrefab;
    [Tooltip("Found automatically in Awake if left empty.")]
    [SerializeField] private GridManager gridManager;
    [Tooltip("Optional parent for spawned minions, to keep the hierarchy tidy.")]
    [SerializeField] private Transform minionParent;

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

    void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
    }

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

    /// <summary>Spawns a minion on the first free grid cell. Returns true on success.</summary>
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
        if (gridManager == null)
        {
            return Fail("No GridManager found in the scene.");
        }
        if (!gridManager.TryGetFreeCell(out Vector2Int cell))
        {
            return Fail("The board is full.");
        }

        Vector3 position = gridManager.GetWorldPosition(cell.x, cell.y);
        GameObject minion = Instantiate(minionPrefab, position, Quaternion.identity, minionParent);

        SummonedCount++;
        MinionStats stats = minion.GetComponent<MinionStats>();
        string baseName = stats != null ? stats.minionName : "Minion";
        minion.name = baseName + " #" + SummonedCount;

        gridManager.PlaceMinion(minion, cell.x, cell.y);
        MinionSummoned?.Invoke(minion, SummonedCount);
        return true;
    }

    bool Fail(string reason)
    {
        Debug.LogWarning("Summon failed: " + reason, this);
        SummonFailed?.Invoke(reason);
        return false;
    }
}
