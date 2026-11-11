using System;
using UnityEngine;

/// <summary>
/// Master volume and mute, saved in PlayerPrefs and applied to AudioListener.
/// Loaded automatically before the first scene loads.
/// </summary>
public static class GameAudioSettings
{
    const string VolumeKey = "settings.masterVolume";
    const string MutedKey = "settings.muted";

    static bool loaded;
    static float masterVolume = 1f;
    static bool muted;

    public static event Action Changed;

    public static float MasterVolume
    {
        get { EnsureLoaded(); return masterVolume; }
        set
        {
            EnsureLoaded();
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, masterVolume)) return;
            masterVolume = clamped;
            ApplyAndNotify();
        }
    }

    public static bool Muted
    {
        get { EnsureLoaded(); return muted; }
        set
        {
            EnsureLoaded();
            if (value == muted) return;
            muted = value;
            ApplyAndNotify();
        }
    }

    /// <summary>Writes the current values to disk. Called when the settings screen closes.</summary>
    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.SetFloat(VolumeKey, masterVolume);
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Reset statics so this works with "Enter Play Mode Options" (domain reload off).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadAndApply()
    {
        EnsureLoaded();
        Apply();
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
    }

    static void ApplyAndNotify()
    {
        Apply();
        Changed?.Invoke();
    }

    static void Apply()
    {
        AudioListener.volume = muted ? 0f : masterVolume;
    }
}
