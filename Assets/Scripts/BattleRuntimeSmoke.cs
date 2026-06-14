using System.Collections;
using System.IO;
using UnityEngine;

public sealed class BattleRuntimeSmoke : MonoBehaviour
{
    private BattleManager manager;

    public void Configure(BattleManager battleManager)
    {
        manager = battleManager;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return null;
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
            yield return new WaitForSeconds(1f);
            BattleBootstrap.Instance.ResetBattle();
            yield return null;
            BattleManager restarted = Object.FindFirstObjectByType<BattleManager>();
            Debug.Log($"Runtime smoke restart: {restarted.DebugSummary}");
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
