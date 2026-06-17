using System.Collections;
using System.IO;
using UnityEngine;

// Rendered frontend smoke used by the presentation overhaul acceptance pass.
public sealed class PresentationRuntimeSmoke : MonoBehaviour
{
    private GameDirector director;

    public void Configure(GameDirector owner)
    {
        director = owner;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return WaitFor(GameDirector.Mode.Title);
        yield return new WaitForSecondsRealtime(0.2f);
        Capture("presentation-title.png");
        yield return new WaitForSecondsRealtime(0.4f);

        director.StartNewCampaign();
        yield return WaitFor(GameDirector.Mode.Map);
        yield return new WaitForSecondsRealtime(0.2f);
        Capture("presentation-map.png");
        yield return new WaitForSecondsRealtime(0.4f);

        director.LaunchBattle(BattleSetup.Default());
        yield return WaitFor(GameDirector.Mode.Battle);
        yield return new WaitForSecondsRealtime(0.2f);
        Capture("presentation-battle-ready.png");
        BattleManager battle = FindFirstObjectByType<BattleManager>();
        battle.BeginBattle();
        yield return new WaitForSecondsRealtime(1f);
        Capture("presentation-battle.png");
        yield return new WaitForSecondsRealtime(0.4f);
        director.Pause();
        yield return new WaitForSecondsRealtime(0.25f);
        Capture("presentation-pause.png");
        yield return new WaitForSecondsRealtime(0.4f);
        Application.Quit(0);
    }

    private IEnumerator WaitFor(GameDirector.Mode mode)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!director.IsModeReady(mode) && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!director.IsModeReady(mode))
            Application.Quit(1);
        yield return new WaitForEndOfFrame();
    }

    private static void Capture(string filename)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", filename));
        ScreenCapture.CaptureScreenshot(path);
    }
}
