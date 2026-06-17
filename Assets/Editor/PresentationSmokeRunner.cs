using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PresentationSmokeRunner
{
    private const string RequestPath = "presentation-smoke.request";
    private const string ResultPath = "presentation-smoke.result";
    private const double TimeoutSeconds = 35d;

    private static bool running;
    private static bool commandLineRun;
    private static int step;
    private static double startedAt;
    private static double stepAt;
    private static GameDirector director;
    private static bool requestedTitleReset;

    static PresentationSmokeRunner()
    {
        commandLineRun = System.Array.Exists(System.Environment.GetCommandLineArgs(),
            argument => argument == "-smokepresentationeditor");
        EditorApplication.update -= Run;
        EditorApplication.update += Run;
    }

    [MenuItem("Conquer Others/Run Presentation Smoke Capture")]
    public static void Request()
    {
        File.WriteAllText(RequestPath, "requested");
        File.Delete(ResultPath);
        ResetState();
    }

    private static void Run()
    {
        if (!running)
        {
            if (!commandLineRun && !File.Exists(RequestPath))
                return;
            ResetState();
            running = true;
        }

        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            Complete(false, $"Presentation smoke timed out after {TimeoutSeconds:0}s at step {step}.");
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
            return;
        }

        director ??= Object.FindFirstObjectByType<GameDirector>();
        if (director == null)
            return;

        if (!requestedTitleReset)
        {
            requestedTitleReset = true;
            director.ReturnToTitle();
            stepAt = EditorApplication.timeSinceStartup;
            return;
        }

        if (step == 0 && director.IsModeReady(GameDirector.Mode.Title))
        {
            if (!AfterDelay(0.75d))
                return;
            Capture("presentation-title.png");
            Advance();
            return;
        }

        if (step == 1)
        {
            if (!AfterDelay(0.5d))
                return;
            director.StartNewCampaign();
            Advance();
            return;
        }

        if (step == 2 && director.IsModeReady(GameDirector.Mode.Map))
        {
            if (!AfterDelay(0.9d))
                return;
            Capture("presentation-map.png");
            Advance();
            return;
        }

        if (step == 3)
        {
            if (!AfterDelay(0.5d))
                return;
            director.LaunchBattle(BattleSetup.Default());
            Advance();
            return;
        }

        if (step == 4 && director.IsModeReady(GameDirector.Mode.Battle))
        {
            BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
            if (manager == null || !AfterDelay(0.8d))
                return;
            Capture("presentation-battle-ready.png");
            Advance();
            return;
        }

        if (step == 5)
        {
            BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
            if (manager == null || !AfterDelay(0.5d))
                return;
            manager.BeginBattle();
            Advance();
            return;
        }

        if (step == 6)
        {
            if (!AfterDelay(2.2d))
                return;
            Capture("presentation-battle-early.png");
            Advance();
            return;
        }

        if (step == 7)
        {
            if (!AfterDelay(3.5d))
                return;
            Capture("presentation-battle-late.png");
            BattleManager closeupManager = Object.FindFirstObjectByType<BattleManager>();
            if (closeupManager != null && closeupManager.Player != null)
                CaptureCloseup(closeupManager.Player.transform, "presentation-fighter-closeup.png");
            Transform archer = FindArcher();
            if (archer != null)
                CaptureCloseup(archer, "presentation-archer-closeup.png");
            Advance();
            return;
        }

        if (step == 8)
        {
            if (!AfterDelay(0.5d))
                return;
            director.Pause();
            Advance();
            return;
        }

        if (step == 9 && director.IsPaused)
        {
            if (!AfterDelay(0.5d))
                return;
            Capture("presentation-pause.png");
            Complete(true, "Presentation smoke capture passed.");
        }
    }

    private static bool AfterDelay(double seconds) => EditorApplication.timeSinceStartup - stepAt >= seconds;

    private static void Advance()
    {
        step++;
        stepAt = EditorApplication.timeSinceStartup;
    }

    private static void Capture(string filename)
    {
        string directory = Path.GetFullPath("PresentationCaptures");
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, filename));
    }

    private static Transform FindArcher()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != "Bow Pivot" || !t.gameObject.activeInHierarchy)
                continue;
            BattleFighter owner = t.GetComponentInParent<BattleFighter>();
            if (owner != null)
                return owner.transform;
        }
        return null;
    }

    private static void CaptureCloseup(Transform target, string filename)
    {
        string directory = Path.GetFullPath("PresentationCaptures");
        Directory.CreateDirectory(directory);
        GameObject camGo = new GameObject("SmokeCloseupCam");
        Camera cam = camGo.AddComponent<Camera>();
        Vector3 focus = target.position + Vector3.up * 1.0f;
        cam.transform.position = focus + target.forward * 1.7f + target.right * 1.4f + Vector3.up * 1.6f;
        cam.transform.LookAt(focus);
        RenderTexture rt = new RenderTexture(600, 800, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(600, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 600, 800), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(directory, filename), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.Destroy(tex);
        Object.Destroy(rt);
        Object.Destroy(camGo);
    }

    private static void Complete(bool passed, string message)
    {
        File.Delete(RequestPath);
        File.WriteAllText(ResultPath, message);
        running = false;
        commandLineRun = false;
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
        if (passed)
            Debug.Log(message);
        else
            Debug.LogError(message);
    }

    private static void ResetState()
    {
        step = 0;
        director = null;
        requestedTitleReset = false;
        startedAt = EditorApplication.timeSinceStartup;
        stepAt = startedAt;
    }
}
