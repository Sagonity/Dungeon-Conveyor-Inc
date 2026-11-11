using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings screen. Reads and writes GameAudioSettings; saves when the screen closes.
/// </summary>
public class SettingsView : UIView
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private Toggle muteToggle;
    [SerializeField] private Button backButton;

    protected override void OnInitialize()
    {
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            volumeSlider.onValueChanged.AddListener(v => GameAudioSettings.MasterVolume = v);
        }
        if (muteToggle != null) muteToggle.onValueChanged.AddListener(on => GameAudioSettings.Muted = on);
        if (backButton != null) backButton.onClick.AddListener(() => Manager.SetState(UIState.MainMenu));

        GameAudioSettings.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        GameAudioSettings.Changed -= Refresh;
    }

    protected override void OnShow() => Refresh();

    protected override void OnHide() => GameAudioSettings.Save();

    void Refresh()
    {
        float volume = GameAudioSettings.MasterVolume;
        bool muted = GameAudioSettings.Muted;

        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(volume);
        if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(muted);
        if (volumeValueText != null)
        {
            volumeValueText.text = muted ? "Muted" : Mathf.RoundToInt(volume * 100f) + "%";
        }
    }
}
