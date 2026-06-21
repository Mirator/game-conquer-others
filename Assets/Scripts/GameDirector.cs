using System.Collections;
using UnityEngine;

// Top-level coordinator. The single runtime entry point for the game; owns the
// switch between the campaign map and a battle, and owns the persistent campaign
// state. Battle and map each live under their own root GameObject so a clean
// teardown/rebuild swaps modes without leaking cameras, lights, or listeners.
public sealed class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum Mode { Title, Map, Battle }
    public Mode CurrentMode { get; private set; }
    public CampaignState Campaign => campaign;
    public bool HasSavedCampaign => CampaignSaveService.HasSave;
    public bool IsPaused { get; private set; }
    public bool IsModeReady(Mode mode) => !transitioning && CurrentMode == mode
        && (mode == Mode.Title ? frontend != null : mode == Mode.Map ? mapRoot != null : battleRoot != null);

    private BattleBootstrap battleBuilder;
    private CampaignState campaign;
    private GameObject battleRoot;
    private GameObject mapRoot;
    private GameObject titleRoot;
    private FrontendUi frontend;
    private BattleSetup currentSetup = BattleSetup.Default();
    private Territory pendingTarget;
    private EnemyParty pendingParty;
    private bool customBattle;
    private int campaignSeed = 1;
    private bool transitioning;
    private bool smokeStarted;
    private static readonly string[] PresentationRootNames = { "Frontend", "Campaign Map", "Battle" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (FindFirstObjectByType<GameDirector>() != null)
            return;
        new GameObject("Game Director").AddComponent<GameDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        DestroyStrayPresentationRoots();

        // One-time: silence the SampleScene's default camera/light. Each mode
        // builds its own afterwards, so this must not run on every mode switch.
        DisableSceneDefaults();
        battleBuilder = gameObject.AddComponent<BattleBootstrap>();
        SettingsService.Load();

        campaign = CampaignState.CreateDefault(campaignSeed);
        MaybeStartSmoke();
        BuildFrontend();
        if (smokeStarted)
            EnterMap();
        else
            ReturnToTitle();
    }

    public void LaunchBattle(BattleSetup setup, Territory target = null)
    {
        currentSetup = setup;
        pendingTarget = target;
        pendingParty = null;
        SaveCampaign(); // capture map-side changes (recruits, weapon choice) before the fight
        StartCoroutine(TransitionTo(Mode.Battle));
    }

    public void LaunchFieldBattle(BattleSetup setup, EnemyParty party)
    {
        currentSetup = setup;
        pendingTarget = null;
        pendingParty = party;
        SaveCampaign();
        StartCoroutine(TransitionTo(Mode.Battle));
    }

    // A one-off battle configured from the title screen for testing/sandbox play.
    // It touches no campaign state and returns to the title when concluded.
    public void LaunchCustomBattle(BattleSetup setup)
    {
        currentSetup = setup;
        pendingTarget = null;
        pendingParty = null;
        customBattle = true;
        StartCoroutine(TransitionTo(Mode.Battle));
    }

    public void EnterMap() => StartCoroutine(TransitionTo(Mode.Map));

    public void StartNewCampaign()
    {
        campaignSeed++;
        campaign = CampaignState.CreateDefault(campaignSeed);
        pendingTarget = null;
        SaveCampaign();
        EnterMap();
    }

    public void ContinueCampaign()
    {
        CampaignState loaded = CampaignSaveService.Load();
        if (loaded == null)
        {
            StartNewCampaign();
            return;
        }
        campaign = loaded;
        campaignSeed = loaded.Seed;
        pendingTarget = null;
        EnterMap();
    }

    public void RestartCampaign()
    {
        StartNewCampaign();
    }

    public void TogglePause()
    {
        if (CurrentMode != Mode.Battle && !IsPaused)
            return;
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (IsPaused || CurrentMode != Mode.Battle)
            return;
        IsPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        frontend?.ShowPause(true);
    }

    public void Resume()
    {
        if (!IsPaused)
            return;
        IsPaused = false;
        Time.timeScale = 1f;
        frontend?.ShowPause(false);
        BattleManager manager = FindFirstObjectByType<BattleManager>();
        if (manager != null && manager.State == BattleManager.BattleState.Fighting)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ReturnToTitle()
    {
        Resume();
        SaveCampaign(); // preserve any map-side progress (e.g. recruiting then leaving)
        StartCoroutine(TransitionTo(Mode.Title));
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public Territory FirstAttackableTarget()
    {
        foreach (Territory t in campaign.AttackableTargets())
            return t;
        return null;
    }

    private IEnumerator TransitionTo(Mode target)
    {
        if (transitioning)
            yield break;
        transitioning = true;
        if (IsPaused)
            Resume();

        // Cursor is visible on the map and on the battle's Ready screen; the
        // battle re-locks it when the fight actually begins.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // The title owns a dedicated camera and dawn lighting. Hide it before a
        // map or battle creates its own camera, rather than allowing even one
        // frame of competing cameras/listeners during the hand-off.
        if (target != Mode.Title)
            frontend?.ShowTitle(false);

        if (battleRoot != null)
        {
            Destroy(battleRoot);
            battleRoot = null;
        }
        if (mapRoot != null)
        {
            Destroy(mapRoot);
            mapRoot = null;
        }
        DestroyStrayPresentationRoots();
        yield return null; // let the deferred Destroy complete before rebuilding

        if (target == Mode.Battle)
            BuildBattleRoot();
        else if (target == Mode.Map)
            BuildMapRoot();

        CurrentMode = target;
        frontend?.ShowTitle(target == Mode.Title);
        transitioning = false;
    }

    private void BuildFrontend()
    {
        titleRoot = new GameObject("Frontend");
        titleRoot.transform.SetParent(transform);
        frontend = titleRoot.AddComponent<FrontendUi>();
        frontend.Configure(this);
    }

    private void BuildBattleRoot()
    {
        battleRoot = new GameObject("Battle");
        BattleManager manager = battleBuilder.Build(battleRoot, currentSetup);
        manager.EncounterTitle = currentSetup.TargetName;
        manager.EncounterKind = currentSetup.Kind;
        manager.OnBattleConcluded = HandleBattleConcluded;
    }

    private void BuildMapRoot()
    {
        mapRoot = new GameObject("Campaign Map");
        mapRoot.AddComponent<CampaignMapController>().Configure(this, campaign);
    }

    private void HandleBattleConcluded(BattleResult result)
    {
        // A custom battle is a sandbox: discard its outcome and return to the title
        // without applying victory/defeat to the campaign.
        if (customBattle)
        {
            customBattle = false;
            StartCoroutine(TransitionTo(Mode.Title));
            return;
        }
        if (currentSetup.IsTraining)
            campaign.LastReport = $"Training completed with {WeaponCatalog.Label(currentSetup.PlayerWeapon)}.";
        else if (result.PlayerWon && pendingParty != null)
            campaign.ResolveFieldBattle(pendingParty, result);
        else if (result.PlayerWon && pendingTarget != null)
            campaign.ApplyVictory(pendingTarget, result);
        else if (!result.PlayerWon)
            campaign.ApplyDefeat();
        pendingTarget = null;
        pendingParty = null;
        SaveCampaign();
        EnterMap();
    }

    // Persist an in-progress campaign; a lost one clears the save so the title
    // screen does not offer to continue it. Free-roam has no victory end.
    private void SaveCampaign()
    {
        if (campaign == null)
            return;
        if (campaign.CampaignOver)
            CampaignSaveService.Delete();
        else
            CampaignSaveService.Save(campaign);
    }

    private void OnApplicationQuit() => SaveCampaign();

    private void MaybeStartSmoke()
    {
        if (smokeStarted)
            return;
        if (System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokepresentation"))
        {
            gameObject.AddComponent<PresentationRuntimeSmoke>().Configure(this);
            return;
        }
        if (System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smoketest"))
        {
            smokeStarted = true;
            gameObject.AddComponent<BattleRuntimeSmoke>().Configure(this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Time.timeScale = 1f;
    }

    private void DestroyStrayPresentationRoots()
    {
        foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!IsPresentationRootName(transform.name))
                continue;
            // Unity "fake-null" (destroyed/unassigned) is not C# null, so a `?.`
            // here would still dereference a dead root; use Unity-aware checks.
            if ((titleRoot != null && transform == titleRoot.transform)
                || (mapRoot != null && transform == mapRoot.transform)
                || (battleRoot != null && transform == battleRoot.transform))
                continue;
            if (transform.GetComponentInParent<GameDirector>() == this)
                continue;
            Destroy(transform.gameObject);
        }
    }

    private static bool IsPresentationRootName(string value)
    {
        foreach (string name in PresentationRootNames)
            if (value == name)
                return true;
        return false;
    }

    private static void DisableSceneDefaults()
    {
        foreach (Camera existing in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            existing.gameObject.SetActive(false);
        foreach (Light existing in FindObjectsByType<Light>(FindObjectsSortMode.None))
            existing.gameObject.SetActive(false);
    }
}
