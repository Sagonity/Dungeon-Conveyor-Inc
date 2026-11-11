using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : UIView
{
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Leave empty to use the Product Name from Player Settings.")]
    [SerializeField] private string title = "";
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;

    protected override void OnInitialize()
    {
        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(title) ? Application.productName : title;
        }
        if (playButton != null) playButton.onClick.AddListener(() => Manager.SetState(UIState.Game));
        if (settingsButton != null) settingsButton.onClick.AddListener(() => Manager.SetState(UIState.Settings));
    }
}
