using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FrontendUi : MonoBehaviour
{
    private GameDirector director;
    private Canvas canvas;
    private RectTransform title;
    private RectTransform settings;
    private RectTransform pause;
    private RectTransform custom;
    private RectTransform titleMenu;
    private RectTransform settingsCard;
    private RectTransform customCard;
    private Button continueButton;
    private Button newCampaignButton;
    private TitleBackdrop backdrop;
    private bool compactTitleLayout;

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
        backdrop?.SetVisible(visible);
        if (canvas != null)
            canvas.gameObject.SetActive(visible || director.IsPaused);
        if (visible)
            SelectTitleDefault();
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
        backdrop = gameObject.AddComponent<TitleBackdrop>();
        backdrop.Configure();
        canvas = MedievalUi.CreateCanvas(transform, "Frontend Canvas", 100);
        BuildTitle();
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

        settings = ModalOverlay("Settings Screen", new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.97f), out settingsCard);
        MedievalUi.Label(settingsCard, "Title", "SETTINGS", 58, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.8f), new Vector2(0.75f, 0.94f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        BuildSettings();
        settings.gameObject.SetActive(false);

        custom = ModalOverlay("Custom Battle Screen", new Vector2(0.26f, 0.035f), new Vector2(0.74f, 0.965f), out customCard);
        BuildCustom();
        custom.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
    }

    private RectTransform FullPanel(string name)
        => MedievalUi.Panel(canvas.transform, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.018f, 0.014f, 0.012f, 0.96f));

    private RectTransform ModalOverlay(string name, Vector2 cardMin, Vector2 cardMax, out RectTransform card)
    {
        RectTransform overlay = MedievalUi.Panel(canvas.transform, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.012f, 0.01f, 0.012f, 0.64f));
        card = MedievalUi.Frame(overlay, name + " Card", cardMin, cardMax, Vector2.zero, Vector2.zero,
            new Color(0.035f, 0.028f, 0.022f, 0.94f), MedievalUi.Gold);
        return overlay;
    }

    private void BuildTitle()
    {
        title = MedievalUi.Panel(canvas.transform, "Title Screen", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
        title.GetComponent<Image>().raycastTarget = false;
        RectTransform wash = MedievalUi.Panel(title, "Dawn Vignette", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.012f, 0.012f, 0.018f, 0.20f));
        wash.GetComponent<Image>().raycastTarget = false;
        RectTransform leftShade = MedievalUi.Panel(title, "Menu Shadow", Vector2.zero, new Vector2(0.56f, 1f), Vector2.zero, Vector2.zero,
            new Color(0.012f, 0.01f, 0.012f, 0.48f));
        leftShade.GetComponent<Image>().raycastTarget = false;
        titleMenu = MedievalUi.Frame(title, "Title Menu", new Vector2(0.055f, 0.13f), new Vector2(0.39f, 0.87f),
            Vector2.zero, Vector2.zero, new Color(0.035f, 0.028f, 0.022f, 0.82f), MedievalUi.Gold);
        MedievalUi.Label(titleMenu, "Title", "CONQUER OTHERS", 64, TextAnchor.MiddleCenter,
            new Vector2(0.06f, 0.73f), new Vector2(0.94f, 0.93f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Label(titleMenu, "Subtitle", "A HEROIC MEDIEVAL CAMPAIGN", 20, TextAnchor.MiddleCenter,
            new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.74f), Vector2.zero, Vector2.zero, MedievalUi.Bone);
        MedievalUi.Divider(titleMenu, "Title Divider", new Vector2(0.16f, 0.625f), new Vector2(0.84f, 0.65f), Vector2.zero, Vector2.zero);
        continueButton = AddTitleButton("CONTINUE", 0.52f, () => director.ContinueCampaign());
        newCampaignButton = AddTitleButton("NEW CAMPAIGN", 0.42f, () => director.StartNewCampaign());
        AddTitleButton("CUSTOM BATTLE", 0.32f, () => ToggleCustom(true));
        AddTitleButton("SETTINGS", 0.22f, () => ToggleSettings(true));
        AddTitleButton("QUIT", 0.12f, director.Quit);
        UpdateTitleLayout();
    }

    private Button AddTitleButton(string label, float centerY, UnityEngine.Events.UnityAction action)
    {
        Button button = MedievalUi.Button(titleMenu, label, label, new Vector2(0.11f, centerY - 0.038f), new Vector2(0.89f, centerY + 0.038f),
            Vector2.zero, Vector2.zero, action);
        ColorBlock colors = button.colors;
        colors.normalColor = MedievalUi.Parchment;
        colors.highlightedColor = new Color(1f, 0.84f, 0.45f);
        colors.selectedColor = new Color(1f, 0.84f, 0.45f);
        colors.pressedColor = MedievalUi.Gold;
        button.colors = colors;
        return button;
    }

    private static Button AddMenuButton(Transform parent, string label, float centerY, UnityEngine.Events.UnityAction action)
        => MedievalUi.Button(parent, label, label, new Vector2(0.37f, centerY - 0.04f), new Vector2(0.63f, centerY + 0.04f),
            Vector2.zero, Vector2.zero, action);

    private void BuildSettings()
    {
        GameSettings value = SettingsService.Current;
        float y = 0.72f;
        AddSettingSlider("MASTER VOLUME", value.masterVolume, y, v => { value.masterVolume = v; SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("MUSIC VOLUME", value.musicVolume, y, v => { value.musicVolume = v; SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("EFFECTS VOLUME", value.effectsVolume, y, v => { value.effectsVolume = v; SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("MOUSE SENSITIVITY", Mathf.InverseLerp(0.35f, 2f, value.mouseSensitivity), y, v => { value.mouseSensitivity = Mathf.Lerp(0.35f, 2f, v); SettingsService.SaveAndApply(); }); y -= 0.1f;
        AddSettingSlider("CAMERA SHAKE", value.cameraShake, y, v => { value.cameraShake = v; SettingsService.SaveAndApply(); });
        MedievalUi.Button(settingsCard, "Reduced Motion", value.reduceMotion ? "REDUCED MOTION: ON" : "REDUCED MOTION: OFF",
            new Vector2(0.28f, 0.265f), new Vector2(0.72f, 0.33f), Vector2.zero, Vector2.zero, () =>
            {
                value.reduceMotion = !value.reduceMotion;
                SettingsService.SaveAndApply();
                RebuildSettings();
            });
        string quality = QualitySettings.names.Length > 0
            ? QualitySettings.names[Mathf.Clamp(value.qualityPreset, 0, QualitySettings.names.Length - 1)] : "PC";
        MedievalUi.Button(settingsCard, "Quality", $"QUALITY: {quality.ToUpperInvariant()}",
            new Vector2(0.28f, 0.18f), new Vector2(0.48f, 0.245f), Vector2.zero, Vector2.zero, CycleQuality);
        MedievalUi.Button(settingsCard, "Resolution", $"{value.resolutionWidth} x {value.resolutionHeight}",
            new Vector2(0.52f, 0.18f), new Vector2(0.72f, 0.245f), Vector2.zero, Vector2.zero, CycleResolution);
        MedievalUi.Button(settingsCard, "Fullscreen", value.fullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF",
            new Vector2(0.28f, 0.10f), new Vector2(0.48f, 0.165f), Vector2.zero, Vector2.zero, () =>
            {
                value.fullscreen = !value.fullscreen;
                SettingsService.SaveAndApply();
                RebuildSettings();
            });
        MedievalUi.Button(settingsCard, "VSync", value.vSync ? "VSYNC: ON" : "VSYNC: OFF",
            new Vector2(0.52f, 0.10f), new Vector2(0.72f, 0.165f), Vector2.zero, Vector2.zero, () =>
            {
                value.vSync = !value.vSync;
                SettingsService.SaveAndApply();
                RebuildSettings();
            });
        MedievalUi.Button(settingsCard, "Back", "BACK", new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.08f),
            Vector2.zero, Vector2.zero, () => ToggleSettings(false));
    }

    private void AddSettingSlider(string label, float value, float y, UnityEngine.Events.UnityAction<float> action)
        => MedievalUi.Slider(settingsCard, label, label, value, new Vector2(0.28f, y), new Vector2(0.72f, y + 0.075f), Vector2.zero, Vector2.zero, action);

    private void RebuildSettings()
    {
        for (int i = settingsCard.childCount - 1; i >= 0; i--)
        {
            // Destroy is deferred to end of frame; deactivate now so the stale widgets
            // neither render nor catch input during the frame they coexist with the rebuilt ones.
            GameObject child = settingsCard.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        MedievalUi.Label(settingsCard, "Title", "SETTINGS", 58, TextAnchor.MiddleCenter,
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
        if (!visible && director.CurrentMode == GameDirector.Mode.Title)
            SelectTitleDefault();
    }

    private void ToggleCustom(bool visible)
    {
        custom.gameObject.SetActive(visible);
        title.gameObject.SetActive(!visible && director.CurrentMode == GameDirector.Mode.Title);
        if (!visible && director.CurrentMode == GameDirector.Mode.Title)
            SelectTitleDefault();
    }

    private void BuildCustom()
    {
        MedievalUi.Label(customCard, "Title", "CUSTOM BATTLE", 58, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.86f), new Vector2(0.75f, 0.96f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(customCard, "Custom Divider", new Vector2(0.34f, 0.835f), new Vector2(0.66f, 0.86f),
            Vector2.zero, Vector2.zero);

        AddStepper("ALLIES (MELEE)", 0.72f, () => customAllies, v => customAllies = v, 0, BattleSetup.MaxDeployed);
        AddStepper("ALLIES (ARCHERS)", 0.63f, () => customArchers, v => customArchers = v, 0, BattleSetup.MaxDeployed);
        AddStepper("ENEMIES", 0.54f, () => customEnemies, v => customEnemies = v, 1, BattleSetup.MaxDeployed);
        AddCycle("ARENA", 0.45f, () => customArena.ToString().ToUpperInvariant(),
            () => customArena = (ArenaType)(((int)customArena + 1) % System.Enum.GetValues(typeof(ArenaType)).Length));
        AddCycle("YOUR WEAPON", 0.36f, () => WeaponCatalog.Label(customWeapon).ToUpperInvariant(),
            () => customWeapon = (WeaponType)(((int)customWeapon + 1) % System.Enum.GetValues(typeof(WeaponType)).Length));

        AddMenuButton(customCard, "START BATTLE", 0.17f, StartCustomBattle);
        AddMenuButton(customCard, "BACK", 0.07f, () => ToggleCustom(false));
    }

    // A label with a value readout and -/+ buttons that mutate an int in [min, max].
    private void AddStepper(string label, float centerY, System.Func<int> get, System.Action<int> set, int min, int max)
    {
        RectTransform row = MedievalUi.Panel(customCard, label + " Row", new Vector2(0.28f, centerY - 0.035f),
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
        button = MedievalUi.Button(customCard, prefix + " Cycle", $"{prefix}: {value()}",
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

    private void Update()
    {
        if (titleMenu == null || Screen.height <= 0)
            return;
        bool compact = Screen.width / (float)Screen.height < 1.5f;
        if (compact == compactTitleLayout)
            return;
        compactTitleLayout = compact;
        UpdateTitleLayout();
    }

    private void UpdateTitleLayout()
    {
        if (titleMenu == null || Screen.height <= 0)
            return;
        compactTitleLayout = Screen.width / (float)Screen.height < 1.5f;
        titleMenu.anchorMin = compactTitleLayout ? new Vector2(0.16f, 0.12f) : new Vector2(0.055f, 0.13f);
        titleMenu.anchorMax = compactTitleLayout ? new Vector2(0.84f, 0.88f) : new Vector2(0.39f, 0.87f);
    }

    private void SelectTitleDefault()
    {
        Button selected = continueButton != null && continueButton.gameObject.activeInHierarchy ? continueButton : newCampaignButton;
        if (selected != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }
}
