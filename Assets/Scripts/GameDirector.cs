using System.Collections;
using UnityEngine;

// Top-level coordinator. The single runtime entry point for the game; owns the
// switch between the campaign map and a battle, and owns the persistent campaign
// state. Battle and map each live under their own root GameObject so a clean
// teardown/rebuild swaps modes without leaking cameras, lights, or listeners.
public sealed class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    public enum Mode { Map, Battle }
    public Mode CurrentMode { get; private set; }
    public CampaignState Campaign => campaign;
    public bool IsModeReady(Mode mode) => !transitioning
        && (mode == Mode.Map ? mapRoot != null : battleRoot != null);

    private BattleBootstrap battleBuilder;
    private CampaignState campaign;
    private GameObject battleRoot;
    private GameObject mapRoot;
    private BattleSetup currentSetup = BattleSetup.Default();
    private Territory pendingTarget;
    private int campaignSeed = 1;
    private bool transitioning;
    private bool smokeStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (FindFirstObjectByType<GameDirector>() != null)
            return;
        new GameObject("Game Director").AddComponent<GameDirector>();
    }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;

        // One-time: silence the SampleScene's default camera/light. Each mode
        // builds its own afterwards, so this must not run on every mode switch.
        DisableSceneDefaults();
        battleBuilder = gameObject.AddComponent<BattleBootstrap>();

        campaign = CampaignState.CreateDefault(campaignSeed);
        MaybeStartSmoke();
        EnterMap();
    }

    public void LaunchBattle(BattleSetup setup, Territory target = null)
    {
        currentSetup = setup;
        pendingTarget = target;
        StartCoroutine(TransitionTo(Mode.Battle));
    }

    public void EnterMap() => StartCoroutine(TransitionTo(Mode.Map));

    public void RestartCampaign()
    {
        campaignSeed++;
        campaign = CampaignState.CreateDefault(campaignSeed);
        pendingTarget = null;
        EnterMap();
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

        // Cursor is visible on the map and on the battle's Ready screen; the
        // battle re-locks it when the fight actually begins.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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
        yield return null; // let the deferred Destroy complete before rebuilding

        if (target == Mode.Battle)
            BuildBattleRoot();
        else
            BuildMapRoot();

        CurrentMode = target;
        transitioning = false;
    }

    private void BuildBattleRoot()
    {
        battleRoot = new GameObject("Battle");
        BattleManager manager = battleBuilder.Build(battleRoot, currentSetup);
        manager.EncounterTitle = currentSetup.TargetName;
        manager.OnBattleConcluded = HandleBattleConcluded;
    }

    private void BuildMapRoot()
    {
        mapRoot = new GameObject("Campaign Map");
        mapRoot.AddComponent<CampaignMapController>().Configure(this, campaign);
    }

    private void HandleBattleConcluded(BattleResult result)
    {
        if (result.PlayerWon && pendingTarget != null)
            campaign.ApplyVictory(pendingTarget, result);
        else if (!result.PlayerWon)
            campaign.ApplyDefeat();
        pendingTarget = null;
        EnterMap();
    }

    private void MaybeStartSmoke()
    {
        if (smokeStarted)
            return;
        if (System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smoketest"))
        {
            smokeStarted = true;
            gameObject.AddComponent<BattleRuntimeSmoke>().Configure(this);
        }
    }

    private static void DisableSceneDefaults()
    {
        foreach (Camera existing in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            existing.gameObject.SetActive(false);
        foreach (Light existing in FindObjectsByType<Light>(FindObjectsSortMode.None))
            existing.gameObject.SetActive(false);
    }
}
