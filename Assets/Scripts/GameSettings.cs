using System;
using UnityEngine;

[Serializable]
public sealed class GameSettings
{
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float effectsVolume = 1f;
    public float mouseSensitivity = 1f;
    public float cameraShake = 1f;
    public bool fullscreen = true;
    public int resolutionWidth;
    public int resolutionHeight;
    public int qualityPreset;
    public bool vSync = true;
    public bool reduceMotion;
    public bool showDamageNumbers = true;

    public static GameSettings Defaults()
    {
        Resolution resolution = Screen.currentResolution;
        return new GameSettings
        {
            resolutionWidth = resolution.width,
            resolutionHeight = resolution.height,
            qualityPreset = Mathf.Max(0, QualitySettings.names.Length - 1)
        };
    }
}

public static class SettingsService
{
    private const string Key = "ConquerOthers.GameSettings";
    public static GameSettings Current { get; private set; }

    public static void Load()
    {
        Current = GameSettings.Defaults();
        string json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json))
            JsonUtility.FromJsonOverwrite(json, Current);
        Apply();
    }

    public static void SaveAndApply()
    {
        if (Current == null)
            Load();
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
        Apply();
    }

    public static void Apply()
    {
        if (Current == null)
            Current = GameSettings.Defaults();
        AudioListener.volume = Mathf.Clamp01(Current.masterVolume);
        if (Application.isEditor)
            return;
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0;
        int quality = Mathf.Clamp(Current.qualityPreset, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        QualitySettings.SetQualityLevel(quality, true);
        if (!Application.isBatchMode && Current.resolutionWidth > 0 && Current.resolutionHeight > 0)
            Screen.SetResolution(Current.resolutionWidth, Current.resolutionHeight,
                Current.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    public static void ResetForTests()
    {
        PlayerPrefs.DeleteKey(Key);
        Current = GameSettings.Defaults();
    }
}
