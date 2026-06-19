using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class FrontendUi : MonoBehaviour
{
    private GameDirector director;
    private Canvas canvas;
    private RectTransform title;
    private RectTransform settings;
    private RectTransform pause;
    private RectTransform custom;
    private Button continueButton;

    // Custom-battle configuration, edited on the setup screen and turned into a
    // BattleSetup when the fight is launched.
    private int customAllies = 8;
    private int customArchers = 0;
    private int customEnemies = 8;
    private ArenaType customArena = ArenaType.Courtyard;
    private WeaponType customWeapon = WeaponType.SwordAndShield;

    public void Configure(GameDirector owner)
    {
        director = owner;
        Build();
    }

    public void ShowTitle(bool visible)
    {
        if (visible && settings != null)
            settings.gameObject.SetActive(false);
        if (visible && custom != null)
            custom.gameObject.SetActive(false);
        if (title != null)
            title.gameObject.SetActive(visible);
        if (continueButton != null)
            continueButton.gameObject.SetActive(visible && director.HasSavedCampaign);
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
        MedievalUi.Divider(title, "Title Divider", new Vector2(0.32f, 0.575f), new Vector2(0.68f, 0.6f),
            Vector2.zero, Vector2.zero);
        continueButton = AddMenuButton(title, "CONTINUE", 0.52f, () => director.ContinueCampaign());
        AddMenuButton(title, "NEW CAMPAIGN", 0.42f, () => director.StartNewCampaign());
        AddMenuButton(title, "CUSTOM BATTLE", 0.32f, () => ToggleCustom(true));
        AddMenuButton(title, "SETTINGS", 0.22f, () => ToggleSettings(true));
        AddMenuButton(title, "QUIT", 0.12f, director.Quit);
        continueButton.gameObject.SetActive(false);

        pause = FullPanel("Pause Screen");
        MedievalUi.Label(pause, "Title", "BATTLE PAUSED", 62, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.67f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(pause, "Pause Divider", new Vector2(0.32f, 0.62f), new Vector2(0.68f, 0.645f),
            Vector2.zero, Vector2.zero);
        AddMenuButton(pause, "RESUME", 0.51f, director.Resume);
        AddMenuButton(pause, "SETTINGS", 0.40f, () => ToggleSettings(true));
        AddMenuButton(pause, "RETURN TO TITLE", 0.29f, director.ReturnToTitle);
        AddMenuButton(pause, "QUIT", 0.18f, director.Quit);

        settings = FullPanel("Settings Screen");
        MedievalUi.Label(settings, "Title", "SETTINGS", 58, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.8f), new Vector2(0.75f, 0.94f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        BuildSettings();
        settings.gameObject.SetActive(false);

        custom = FullPanel("Custom Battle Screen");
        BuildCustom();
        custom.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
    }

    private RectTransform FullPanel(string name)
        => MedievalUi.Panel(canvas.transform, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.018f, 0.014f, 0.012f, 0.96f));

    private static Button AddMenuButton(Transform parent, string label, float centerY, UnityEngine.Events.UnityAction action)
        => MedievalUi.Button(parent, label, label, new Vector2(0.37f, centerY - 0.04f), new Vector2(0.63f, centerY + 0.04f),
            Vector2.zero, Vector2.zero, action);

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

    private void ToggleCustom(bool visible)
    {
        custom.gameObject.SetActive(visible);
        title.gameObject.SetActive(!visible && director.CurrentMode == GameDirector.Mode.Title);
    }

    private void BuildCustom()
    {
        MedievalUi.Label(custom, "Title", "CUSTOM BATTLE", 58, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.86f), new Vector2(0.75f, 0.96f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(custom, "Custom Divider", new Vector2(0.34f, 0.835f), new Vector2(0.66f, 0.86f),
            Vector2.zero, Vector2.zero);

        AddStepper("ALLIES (MELEE)", 0.72f, () => customAllies, v => customAllies = v, 0, BattleSetup.MaxDeployed);
        AddStepper("ALLIES (ARCHERS)", 0.63f, () => customArchers, v => customArchers = v, 0, BattleSetup.MaxDeployed);
        AddStepper("ENEMIES", 0.54f, () => customEnemies, v => customEnemies = v, 1, BattleSetup.MaxDeployed);
        AddCycle("ARENA", 0.45f, () => customArena.ToString().ToUpperInvariant(),
            () => customArena = (ArenaType)(((int)customArena + 1) % System.Enum.GetValues(typeof(ArenaType)).Length));
        AddCycle("YOUR WEAPON", 0.36f, () => WeaponCatalog.Label(customWeapon).ToUpperInvariant(),
            () => customWeapon = (WeaponType)(((int)customWeapon + 1) % System.Enum.GetValues(typeof(WeaponType)).Length));

        AddMenuButton(custom, "START BATTLE", 0.17f, StartCustomBattle);
        AddMenuButton(custom, "BACK", 0.07f, () => ToggleCustom(false));
    }

    // A label with a value readout and -/+ buttons that mutate an int in [min, max].
    private void AddStepper(string label, float centerY, System.Func<int> get, System.Action<int> set, int min, int max)
    {
        RectTransform row = MedievalUi.Panel(custom, label + " Row", new Vector2(0.28f, centerY - 0.035f),
            new Vector2(0.72f, centerY + 0.035f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.06f, 0.04f, 0.85f));
        MedievalUi.Label(row, "Label", label, 24, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(0.55f, 1f), new Vector2(18f, 0f), Vector2.zero);
        Text value = MedievalUi.Label(row, "Value", get().ToString(), 26, TextAnchor.MiddleCenter,
            new Vector2(0.55f, 0f), new Vector2(0.74f, 1f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Button(row, "Minus", "-", new Vector2(0.76f, 0.12f), new Vector2(0.87f, 0.88f),
            Vector2.zero, Vector2.zero, () => { set(Mathf.Clamp(get() - 1, min, max)); value.text = get().ToString(); });
        MedievalUi.Button(row, "Plus", "+", new Vector2(0.88f, 0.12f), new Vector2(0.99f, 0.88f),
            Vector2.zero, Vector2.zero, () => { set(Mathf.Clamp(get() + 1, min, max)); value.text = get().ToString(); });
    }

    // A single button that shows "PREFIX: value" and cycles the value on click.
    private void AddCycle(string prefix, float centerY, System.Func<string> value, System.Action advance)
    {
        Button button = null;
        button = MedievalUi.Button(custom, prefix + " Cycle", $"{prefix}: {value()}",
            new Vector2(0.28f, centerY - 0.035f), new Vector2(0.72f, centerY + 0.035f), Vector2.zero, Vector2.zero, () =>
            {
                advance();
                button.GetComponentInChildren<Text>().text = $"{prefix}: {value()}";
            });
    }

    private void StartCustomBattle()
    {
        BattleSetup setup = BattleSetup.Default();
        setup.IsTraining = false;
        setup.Kind = BattleKind.BanditField;
        setup.Arena = customArena;
        setup.PlayerWeapon = customWeapon;
        setup.TargetName = "CUSTOM BATTLE";
        setup.EnemyCount = customEnemies;
        setup.AllyCount = customAllies + customArchers;

        List<UnitSpec> allies = new();
        for (int i = 0; i < customAllies; i++)
            allies.Add(new UnitSpec(UnitType.Militia, Archetype.Soldier, WeaponType.SwordAndShield));
        for (int i = 0; i < customArchers; i++)
            allies.Add(new UnitSpec(UnitType.Militia, Archetype.Archer, WeaponType.Bow));
        setup.AllyComposition = allies.Count > 0 ? allies : null;

        List<UnitSpec> enemies = new();
        for (int i = 0; i < customEnemies; i++)
            enemies.Add(new UnitSpec(UnitType.Militia, Archetype.Soldier, WeaponType.SwordAndShield));
        setup.EnemyComposition = enemies;

        ToggleCustom(false);
        director.LaunchCustomBattle(setup);
    }
}
