using System.Collections;
using System.IO;
using UnityEngine;

// Standalone automated check (-smoketest). Drives one campaign step: from the
// map, assault the first attackable territory, audit directional blocks, and
// (with -smokevictory) force a win and confirm the loop returns to the map with
// the territory captured.
public sealed class BattleRuntimeSmoke : MonoBehaviour
{
    private GameDirector director;

    public void Configure(GameDirector gameDirector)
    {
        director = gameDirector;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        while (director.CurrentMode != GameDirector.Mode.Map)
            yield return null; // wait for the opening map transition to settle
        Territory target = director.FirstAttackableTarget();
        Debug.Log($"Runtime smoke map ready: target={(target != null ? target.Name : "none")}, mode={director.CurrentMode}");
        director.LaunchBattle(director.Campaign.BuildSetupFor(target), target);

        while (director.CurrentMode != GameDirector.Mode.Battle)
            yield return null; // wait for the battle to finish building
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        manager.BeginBattle();
        bool directionalAudit = manager.DebugAuditDirectionalBlock();
        Debug.Log($"Runtime smoke directional combat: passed={directionalAudit}");
        yield return new WaitForSeconds(1.5f);
        Capture("smoke-opening.png");
        Debug.Log($"Runtime smoke opening: {manager.DebugSummary}");

        bool victoryRun = System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokevictory");
        if (victoryRun)
        {
            manager.DebugEliminateTeam(Team.Enemies);
            yield return new WaitForSeconds(1f);
            Capture("smoke-victory.png");
            Debug.Log($"Runtime smoke victory: {manager.DebugSummary}");
            manager.ConfirmResult(); // dismiss result -> return to map
            while (director.CurrentMode != GameDirector.Mode.Map)
                yield return null;
            Debug.Log($"Runtime smoke return: mode={director.CurrentMode}, target owner={target.Owner}, roster={director.Campaign.Roster}");
            Capture("smoke-map.png");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
            yield break;
        }

        yield return new WaitForSeconds(5.5f);
        Capture("smoke-combat.png");
        Debug.Log($"Runtime smoke combat: {manager.DebugSummary}");
        yield return new WaitForSeconds(16.5f);
        Capture("smoke-battle.png");
        Debug.Log($"Runtime smoke battle: {manager.DebugSummary}");
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }

    private static void Capture(string filename)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", filename));
        ScreenCapture.CaptureScreenshot(path);
    }
}
