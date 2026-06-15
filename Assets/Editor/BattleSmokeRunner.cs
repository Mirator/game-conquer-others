using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BattleSmokeRunner
{
    private const string RequestPath = "smoke.request";
    private static double playStartedAt;
    private static bool capturedOpening;
    private static bool capturedBattle;
    private static bool commandLineRun;
    private static double requestedAt;
    private static bool completing;
    private static bool diagnosticsComplete;
    private static BattleManager diagnosticManager;
    private static Territory target;
    private static BattleSetup setup;

    static BattleSmokeRunner()
    {
        commandLineRun = System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokeeditor");
        if (File.Exists(RequestPath) || commandLineRun)
        {
            Application.logMessageReceived += OnLogMessage;
            EditorApplication.update += Run;
        }
    }

    [MenuItem("Conquer Others/Run Battle Smoke Test")]
    public static void Request()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play mode before starting the editor smoke test.");
            return;
        }
        File.WriteAllText(RequestPath, "requested");
        playStartedAt = 0d;
        capturedOpening = false;
        capturedBattle = false;
        requestedAt = EditorApplication.timeSinceStartup;
        completing = false;
        diagnosticsComplete = false;
        diagnosticManager = null;
        target = null;
        setup = null;
        Application.logMessageReceived -= OnLogMessage;
        Application.logMessageReceived += OnLogMessage;
        EditorApplication.update -= Run;
        EditorApplication.update += Run;
    }

    [MenuItem("Conquer Others/Run Battle Smoke Test", true)]
    private static bool CanRequest() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static void Run()
    {
        if (requestedAt <= 0d)
            requestedAt = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - requestedAt > 40d)
        {
            Complete(false, "Editor smoke timed out after 40 seconds.");
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
            return;
        }

        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        if (manager == null)
        {
            // The game now opens on the campaign map; launch a battle so the
            // smoke test has something to drive.
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            if (director != null && director.CurrentMode == GameDirector.Mode.Map)
            {
                target = director.FirstAttackableTarget();
                if (target != null)
                {
                    setup = director.Campaign.BuildSetupFor(target);
                    BattleSetup diagnosticSetup = BattleSetup.Default();
                    diagnosticSetup.AllyCount = 0;
                    diagnosticSetup.EnemyCount = 1;
                    diagnosticSetup.Arena = setup.Arena;
                    diagnosticSetup.TargetName = "COMBAT DIAGNOSTICS";
                    director.LaunchBattle(diagnosticSetup);
                }
            }
            return;
        }

        if (!diagnosticsComplete)
        {
            GameDirector director = Object.FindFirstObjectByType<GameDirector>();
            if (setup == null || director == null)
            {
                Complete(false, "Editor smoke did not initialize from the campaign map.");
                return;
            }
            diagnosticManager = manager;
            manager.BeginBattle();
            bool auditsPassed = BattleDiagnostics.AuditDirectionalBlock(manager)
                && BattleDiagnostics.AuditResponsiveCombat(manager)
                && BattleDiagnostics.AuditCombatExcellence(manager);
            if (!auditsPassed)
            {
                Complete(false, "Editor smoke combat audits failed.");
                return;
            }
            diagnosticsComplete = true;
            director.LaunchBattle(setup, target);
            return;
        }

        if (manager == diagnosticManager)
            return;

        if (playStartedAt <= 0d)
        {
            playStartedAt = EditorApplication.timeSinceStartup;
            manager.BeginBattle();
        }

        double elapsed = EditorApplication.timeSinceStartup - playStartedAt;
        if (!capturedOpening && elapsed > 1.5d)
        {
            Capture("smoke-opening.png");
            Debug.Log($"Smoke opening: {manager.DebugSummary}");
            capturedOpening = true;
        }
        if (!capturedBattle && elapsed > 22d)
        {
            if (!manager.DebugAuditAICoordination())
            {
                Complete(false, "Editor smoke AI coordination audit failed.");
                return;
            }
            Capture("smoke-battle.png");
            Debug.Log($"Smoke battle: {manager.DebugSummary}");
            capturedBattle = true;
            Complete(true, "Editor smoke passed.");
        }
    }

    private static void Complete(bool passed, string message)
    {
        if (completing)
            return;
        completing = true;
        if (passed)
            Debug.Log(message);
        else
            Debug.LogError(message);
        File.Delete(RequestPath);
        Application.logMessageReceived -= OnLogMessage;
        EditorApplication.update -= Run;
        EditorApplication.isPlaying = false;
        if (commandLineRun)
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (!completing && (type == LogType.Exception || type == LogType.Assert || type == LogType.Error))
            Complete(false, $"Editor smoke encountered managed {type}: {condition}");
    }

    private static void Capture(string filename)
    {
        bool requestedInBatch = System.Array.Exists(System.Environment.GetCommandLineArgs(),
            argument => argument == "-smokescreenshots");
        if (!Application.isBatchMode || requestedInBatch)
            ScreenCapture.CaptureScreenshot(filename);
    }
}
