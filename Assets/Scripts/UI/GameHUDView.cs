using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game HUD. Sends summon requests to GameController and shows the results.
/// </summary>
public class GameHUDView : UIView
{
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private GameController gameController;

    [Header("Controls")]
    [SerializeField] private Button summonButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Readouts")]
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private TMP_Text logText;
    [SerializeField, Min(1)] private int maxLogLines = 4;

    readonly Queue<string> logLines = new Queue<string>();

    protected override void OnInitialize()
    {
        if (gameController == null)
        {
            gameController = FindFirstObjectByType<GameController>();
        }

        if (summonButton != null) summonButton.onClick.AddListener(OnSummonClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => Manager.SetState(UIState.MainMenu));

        if (gameController != null)
        {
            gameController.MinionSummoned += HandleMinionSummoned;
            gameController.SummonFailed += HandleSummonFailed;
        }
        else
        {
            Debug.LogWarning("GameHUDView: no GameController found, the Summon button will do nothing.", this);
        }

        UpdateCounter(gameController != null ? gameController.SummonedCount : 0);
        RefreshLog();
    }

    void OnDestroy()
    {
        if (gameController != null)
        {
            gameController.MinionSummoned -= HandleMinionSummoned;
            gameController.SummonFailed -= HandleSummonFailed;
        }
    }

    void OnSummonClicked()
    {
        if (gameController != null) gameController.SummonMinion();
    }

    void HandleMinionSummoned(GameObject minion, int total)
    {
        UpdateCounter(total);
        AddLog("Summoned " + minion.name);
    }

    void HandleSummonFailed(string reason)
    {
        AddLog("<color=#FF8A80>" + reason + "</color>");
    }

    void UpdateCounter(int total)
    {
        if (counterText != null) counterText.text = "Minions summoned: " + total;
    }

    void AddLog(string line)
    {
        logLines.Enqueue(line);
        while (logLines.Count > maxLogLines) logLines.Dequeue();
        RefreshLog();
    }

    void RefreshLog()
    {
        if (logText != null) logText.text = string.Join("\n", logLines);
    }
}
