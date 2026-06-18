using UnityEngine;
using UnityEngine.UI;

public sealed class FrontendUi : MonoBehaviour
{
    private GameDirector director;
    private Canvas canvas;
    private RectTransform title;
    private RectTransform settings;
    private RectTransform pause;

    public void Configure(GameDirector owner)
    {
        director = owner;
        Build();
    }

    public void ShowTitle(bool visible)
    {
        if (visible && settings != null)
            settings.gameObject.SetActive(false);
        if (title != null)
            title.gameObject.SetActive(visible);
        if (canvas != null)
            canvas.gameObject.SetActive(visible || director.IsPaused);
    }

    public void ShowPause(bool visible)
    {
        if (visible && settings != null)
            settings.gameObject.SetActive(false);
        if (pause != null)
            pause.gameObject.SetActive(visible);
        if (canvas != null)
            canvas.gameObject.SetActive(visible || director.CurrentMode == GameDirector.Mode.Title);
    }

    private void Build()
    {
        canvas = MedievalUi.CreateCanvas(transform, "Frontend Canvas", 100);
        title = FullPanel("Title Screen");
        MedievalUi.Label(title, "Title", "CONQUER OTHERS", 78, TextAnchor.MiddleCenter,
            new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.88f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Label(title, "Subtitle", "A HEROIC MEDIEVAL CAMPAIGN", 26, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.61f), new Vector2(0.75f, 0.69f), Vector2.zero, Vector2.zero);
        AddMenuButton(title, "NEW CAMPAIGN", 0.48f, () => director.StartNewCampaign());
        AddMenuButton(title, "SETTINGS", 0.37f, () => ToggleSettings(true));
        AddMenuButton(title, "QUIT", 0.26f, director.Quit);

        pause = FullPanel("Pause Screen");
        MedievalUi.Label(pause, "Title", "BATTLE PAUSED", 62, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.67f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        AddMenuButton(pause, "RESUME", 0.51f, director.Resume);
        AddMenuButton(pause, "SETTINGS", 0.40f, () => ToggleSettings(true));
        AddMenuButton(pause, "RETURN TO TITLE", 0.29f, director.ReturnToTitle);
        AddMenuButton(pause, "QUIT", 0.18f, director.Quit);

        settings = FullPanel("Settings Screen");
        MedievalUi.Label(settings, "Title", "SETTINGS", 58, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.8f), new Vector2(0.75f, 0.94f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        BuildSettings();
        settings.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
    }

    private RectTransform FullPanel(string name)
        => MedievalUi.Panel(canvas.transform, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.018f, 0.014f, 0.012f, 0.96f));

    private static void AddMenuButton(Transform parent, string label, float centerY, UnityEngine.Events.UnityAction action)
    {
        MedievalUi.Button(parent, label, label, new Vector2(0.37f, centerY - 0.04f), new Vector2(0.63f, centerY + 0.04f),
            Vector2.zero, Vector2.zero, action);
    }

    private void BuildSettings()
    {
        GameSettings value = SettingsService.Current;
        float y = 0.7f;
        AddSettingSlider("MASTER VOLUME", value.masterVolume, y, v => { value.masterVolume = v; SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("MUSIC VOLUME", value.musicVolume, y, v => { value.musicVolume = v; SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("EFFECTS VOLUME", value.effectsVolume, y, v => { value.effectsVolume = v; SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("MOUSE SENSITIVITY", value.mouseSensitivity, y, v => { value.mouseSensitivity = Mathf.Lerp(0.35f, 2f, v); SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("CAMERA SHAKE", value.cameraShake, y, v => { value.cameraShake = v; SettingsService.SaveAndApply(); });
        string quality = QualitySettings.names.Length > 0
            ? QualitySettings.names[Mathf.Clamp(value.qualityPreset, 0, QualitySettings.names.Length - 1)] : "PC";
        MedievalUi.Button(settings, "Quality", $"QUALITY: {quality.ToUpperInvariant()}",
            new Vector2(0.28f, 0.215f), new Vector2(0.48f, 0.28f), Vector2.zero, Vector2.zero, CycleQuality);
        MedievalUi.Button(settings, "Resolution", $"{value.resolutionWidth} x {value.resolutionHeight}",
            new Vector2(0.52f, 0.215f), new Vector2(0.72f, 0.28f), Vector2.zero, Vector2.zero, CycleResolution);
        MedievalUi.Button(settings, "Fullscreen", value.fullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF",
            new Vector2(0.28f, 0.13f), new Vector2(0.48f, 0.2f), Vector2.zero, Vector2.zero, () =>
            {
                value.fullscreen = !value.fullscreen;
                SettingsService.SaveAndApply();
                RebuildSettings();
            });
        MedievalUi.Button(settings, "VSync", value.vSync ? "VSYNC: ON" : "VSYNC: OFF",
            new Vector2(0.52f, 0.13f), new Vector2(0.72f, 0.2f), Vector2.zero, Vector2.zero, () =>
            {
                value.vSync = !value.vSync;
                SettingsService.SaveAndApply();
                RebuildSettings();
            });
        MedievalUi.Button(settings, "Back", "BACK", new Vector2(0.4f, 0.035f), new Vector2(0.6f, 0.105f),
            Vector2.zero, Vector2.zero, () => ToggleSettings(false));
    }

    private void AddSettingSlider(string label, float value, float y, UnityEngine.Events.UnityAction<float> action)
        => MedievalUi.Slider(settings, label, label, value, new Vector2(0.28f, y), new Vector2(0.72f, y + 0.075f), Vector2.zero, Vector2.zero, action);

    private void RebuildSettings()
    {
        for (int i = settings.childCount - 1; i >= 0; i--)
        {
            // Destroy is deferred to end of frame; deactivate now so the stale widgets
            // neither render nor catch input during the frame they coexist with the rebuilt ones.
            GameObject child = settings.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        MedievalUi.Label(settings, "Title", "SETTINGS", 58, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.8f), new Vector2(0.75f, 0.94f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        BuildSettings();
    }

    private void CycleQuality()
    {
        int count = Mathf.Max(1, QualitySettings.names.Length);
        SettingsService.Current.qualityPreset = (SettingsService.Current.qualityPreset + 1) % count;
        SettingsService.SaveAndApply();
        RebuildSettings();
    }

    private void CycleResolution()
    {
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions.Length == 0)
            return;
        int current = 0;
        for (int i = 0; i < resolutions.Length; i++)
            if (resolutions[i].width == SettingsService.Current.resolutionWidth
                && resolutions[i].height == SettingsService.Current.resolutionHeight)
                current = i;
        Resolution next = resolutions[(current + 1) % resolutions.Length];
        SettingsService.Current.resolutionWidth = next.width;
        SettingsService.Current.resolutionHeight = next.height;
        SettingsService.SaveAndApply();
        RebuildSettings();
    }

    private void ToggleSettings(bool visible)
    {
        settings.gameObject.SetActive(visible);
        title.gameObject.SetActive(!visible && director.CurrentMode == GameDirector.Mode.Title);
        pause.gameObject.SetActive(!visible && director.IsPaused);
    }
}
