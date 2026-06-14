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

    static BattleSmokeRunner()
    {
        commandLineRun = System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokeeditor");
        if (File.Exists(RequestPath) || commandLineRun)
            EditorApplication.update += Run;
    }

    [MenuItem("Conquer Others/Run Battle Smoke Test")]
    public static void Request()
    {
        File.WriteAllText(RequestPath, "requested");
        playStartedAt = 0d;
        capturedOpening = false;
        capturedBattle = false;
        EditorApplication.update -= Run;
        EditorApplication.update += Run;
    }

    private static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
            return;
        }

        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        if (manager == null)
            return;

        if (playStartedAt <= 0d)
        {
            playStartedAt = EditorApplication.timeSinceStartup;
            manager.BeginBattle();
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
            ScreenCapture.CaptureScreenshot("smoke-battle.png");
            Debug.Log($"Smoke battle: {manager.DebugSummary}");
            capturedBattle = true;
            File.Delete(RequestPath);
            EditorApplication.update -= Run;
            EditorApplication.isPlaying = false;
            if (commandLineRun)
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
