using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game state's view for UIManager. Since SCRUM-30 the game is played on the grey-box
/// screen (GreyboxGameScreen), so this view only hides the old Menu button. The placeholder
/// Summon button, counter and log were removed in SCRUM-10. It goes away with the real HUD (SCRUM-12).
/// </summary>
public class GameHUDView : UIView
{
    [Header("Controls")]
    [SerializeField] private Button mainMenuButton;

    [Tooltip("Hide the old Menu button (the grey-box screen has its own settings menu).")]
    [SerializeField] private bool hidePlaceholderControls = true;

    protected override void OnInitialize()
    {
        if (mainMenuButton == null) return;
        if (hidePlaceholderControls)
        {
            mainMenuButton.gameObject.SetActive(false);
            return;
        }
        mainMenuButton.onClick.AddListener(() => Manager.SetState(UIState.MainMenu));
    }
}
