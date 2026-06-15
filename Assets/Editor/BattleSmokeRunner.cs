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
        File.WriteAllText(RequestPath, "requested");
        playStartedAt = 0d;
        capturedOpening = false;
        capturedBattle = false;
        requestedAt = EditorApplication.timeSinceStartup;
        completing = false;
        Application.logMessageReceived -= OnLogMessage;
        Application.logMessageReceived += OnLogMessage;
        EditorApplication.update -= Run;
        EditorApplication.update += Run;
    }

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
                Territory target = director.FirstAttackableTarget();
                if (target != null)
                    director.LaunchBattle(director.Campaign.BuildSetupFor(target), target);
            }
            return;
        }

        if (playStartedAt <= 0d)
        {
            playStartedAt = EditorApplication.timeSinceStartup;
            manager.BeginBattle();
            bool auditsPassed = BattleDiagnostics.AuditDirectionalBlock(manager)
                && BattleDiagnostics.AuditResponsiveCombat(manager)
                && BattleDiagnostics.AuditCombatExcellence(manager);
            if (!auditsPassed)
            {
                Complete(false, "Editor smoke combat audits failed.");
                return;
            }
        }

        double elapsed = EditorApplication.timeSinceStartup - playStartedAt;
        if (!capturedOpening && elapsed > 1.5d)
        {
            ScreenCapture.CaptureScreenshot("smoke-opening.png");
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
            ScreenCapture.CaptureScreenshot("smoke-battle.png");
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
}
