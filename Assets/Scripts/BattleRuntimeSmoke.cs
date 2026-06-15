using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Standalone automated check (-smoketest). Every asserted condition contributes
// to the process exit code, and every mode transition has a timeout.
public sealed class BattleRuntimeSmoke : MonoBehaviour
{
    private const float TransitionTimeout = 10f;

    private readonly List<string> failures = new();
    private GameDirector director;
    private bool finishing;
    private bool captureScreenshots;

    public void Configure(GameDirector gameDirector)
    {
        director = gameDirector;
        captureScreenshots = HasArgument("-smokescreenshots") || !Application.isBatchMode;
        Application.logMessageReceived += OnLogMessage;
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    private IEnumerator Run()
    {
        yield return WaitForMode(GameDirector.Mode.Map, "opening map");
        if (failures.Count > 0)
            yield break;

        Territory target = director.FirstAttackableTarget();
        Require(target != null, "opening map has an attackable target");
        if (target == null)
        {
            Finish();
            yield break;
        }

        int startingGold = director.Campaign.Gold;
        int startingRoster = director.Campaign.Roster;
        bool recruited = director.Campaign.Recruit(UnitType.Veteran);
        bool variedArenas = HasAllArenaTypes(director.Campaign);
        Require(recruited
            && director.Campaign.Gold == startingGold - UnitCatalog.Cost(UnitType.Veteran)
            && director.Campaign.Roster == startingRoster + 1
            && director.Campaign.IncomePerVictory() > 0
            && variedArenas, "campaign progression");

        BattleSetup setup = director.Campaign.BuildSetupFor(target);
        bool largeRun = HasArgument("-smokelarge");
        bool duelRun = HasArgument("-smokeduel");
        if (largeRun)
        {
            setup.AllyCount = 5;
            setup.AllyMilitia = 5;
            setup.AllyVeterans = 0;
            setup.AllyGuards = 0;
            setup.EnemyCount = 6;
        }
        else if (duelRun)
        {
            setup.AllyCount = 0;
            setup.AllyMilitia = 0;
            setup.AllyVeterans = 0;
            setup.AllyGuards = 0;
            setup.EnemyCount = 1;
            setup.EnemyVeterans = 0;
            setup.EnemyGuards = 0;
        }
        setup.Arena = ArenaOverride(setup.Arena);
        BattleSetup diagnosticSetup = BattleSetup.Default();
        diagnosticSetup.AllyCount = 0;
        diagnosticSetup.EnemyCount = 1;
        diagnosticSetup.Arena = setup.Arena;
        diagnosticSetup.TargetName = "COMBAT DIAGNOSTICS";
        director.LaunchBattle(diagnosticSetup);

        yield return WaitForMode(GameDirector.Mode.Battle, "diagnostic battle launch");
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Require(manager != null, "diagnostic battle manager exists");
        if (manager == null)
        {
            Finish();
            yield break;
        }

        manager.BeginBattle();
        Require(BattleDiagnostics.AuditDirectionalBlock(manager), "directional combat");
        Require(BattleDiagnostics.AuditResponsiveCombat(manager), "responsive combat");
        Require(BattleDiagnostics.AuditCombatExcellence(manager), "combat excellence");

        director.LaunchBattle(setup, target);
        yield return WaitForMode(GameDirector.Mode.Battle, "natural battle launch");
        manager = Object.FindFirstObjectByType<BattleManager>();
        Require(manager != null, "natural battle manager exists");
        if (manager == null)
        {
            Finish();
            yield break;
        }
        manager.BeginBattle();

        if (duelRun)
        {
            yield return new WaitForSeconds(0.15f);
            manager.Player.DebugResetCombatFeedback();
            manager.DebugClearCombatMessage();
            BattleFighter duelThreat = manager.FindNearestOpponent(manager.Player);
            bool forcedTelegraph = duelThreat != null && duelThreat.DebugForceAttackTelegraph(CombatDirection.Right);
            yield return new WaitForSeconds(0.12f);
            Require(forcedTelegraph && manager.FindIncomingThreat(manager.Player) == duelThreat, "duel threat telegraph");
            Capture("smoke-telegraph.png");
        }

        yield return new WaitForSeconds(1.5f);
        Capture("smoke-opening.png");
        Debug.Log($"Runtime smoke opening: {manager.DebugSummary}, {manager.DebugAISummary}");

        if (HasArgument("-smokevictory"))
        {
            manager.DebugEliminateTeam(Team.Enemies);
            yield return new WaitForSeconds(1f);
            Require(manager.State == BattleManager.BattleState.Victory, "forced victory");
            Capture("smoke-victory.png");
            manager.ConfirmResult();
            yield return WaitForMode(GameDirector.Mode.Map, "victory return to map");
            Require(target.Owner == TerritoryOwner.Player
                && director.Campaign.Gold > startingGold - UnitCatalog.Cost(UnitType.Veteran), "victory economy");
            if (HasArgument("-smokecampaign"))
                yield return RunCampaignConquests(4);
            Capture("smoke-map.png");
            yield return new WaitForSeconds(0.5f);
            Finish();
            yield break;
        }

        yield return new WaitForSeconds(5.5f);
        Capture("smoke-combat.png");
        Require(manager.DebugAuditAICoordination(), "AI coordination");
        Debug.Log($"Runtime smoke combat: {manager.DebugSummary}, {manager.DebugAISummary}");
        yield return new WaitForSeconds(16.5f);
        Capture("smoke-battle.png");
        Debug.Log($"Runtime smoke battle: {manager.DebugSummary}, {manager.DebugAISummary}");
        Finish();
    }

    private IEnumerator RunCampaignConquests(int desiredTerritories)
    {
        int rounds = 0;
        while (director.Campaign.PlayerTerritoryCount() < desiredTerritories && rounds++ < 5)
        {
            director.Campaign.Recruit(UnitType.Militia);
            Territory next = director.FirstAttackableTarget();
            if (next == null)
                break;
            director.LaunchBattle(director.Campaign.BuildSetupFor(next), next);
            yield return WaitForMode(GameDirector.Mode.Battle, $"campaign battle {rounds}");
            if (failures.Count > 0)
                yield break;
            BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
            Require(manager != null, $"campaign battle {rounds} manager exists");
            if (manager == null)
                yield break;
            manager.BeginBattle();
            yield return new WaitForSeconds(0.4f);
            manager.DebugEliminateTeam(Team.Enemies);
            yield return new WaitForSeconds(0.4f);
            Require(manager.State == BattleManager.BattleState.Victory, $"campaign battle {rounds} victory");
            manager.ConfirmResult();
            yield return WaitForMode(GameDirector.Mode.Map, $"campaign return {rounds}");
        }

        Require(director.Campaign.PlayerTerritoryCount() >= desiredTerritories
            && director.Campaign.Gold >= 0 && director.Campaign.Roster > 0, "multi-conquest campaign");
    }

    private IEnumerator WaitForMode(GameDirector.Mode mode, string label)
    {
        float deadline = Time.realtimeSinceStartup + TransitionTimeout;
        while (director != null && !director.IsModeReady(mode) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(director != null && director.IsModeReady(mode), $"{label} completed within {TransitionTimeout:0}s");
        if (failures.Count > 0)
            Finish();
    }

    private void Require(bool passed, string label)
    {
        if (passed)
        {
            Debug.Log($"Runtime smoke PASS: {label}");
            return;
        }
        failures.Add(label);
        Debug.LogError($"Runtime smoke FAIL: {label}");
    }

    private void Finish()
    {
        if (finishing)
            return;
        finishing = true;
        Application.logMessageReceived -= OnLogMessage;
        StopAllCoroutines();
        if (failures.Count == 0)
        {
            Debug.Log("Runtime smoke PASSED");
            Application.Quit(0);
        }
        else
        {
            Debug.LogError($"Runtime smoke FAILED ({failures.Count}): {string.Join(", ", failures)}");
            Application.Quit(1);
        }
    }

    private void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (finishing || type != LogType.Exception && type != LogType.Assert && type != LogType.Error)
            return;
        failures.Add($"{type}: {condition}");
        if (type == LogType.Exception || type == LogType.Assert)
            Application.Quit(1);
    }

    private static bool HasArgument(string argument)
    {
        return System.Array.Exists(System.Environment.GetCommandLineArgs(), value => value == argument);
    }

    private void Capture(string filename)
    {
        if (!captureScreenshots)
            return;
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", filename));
        ScreenCapture.CaptureScreenshot(path);
    }

    private static bool HasAllArenaTypes(CampaignState campaign)
    {
        bool courtyard = false, forest = false, marsh = false, highlands = false;
        foreach (Territory territory in campaign.Territories)
        {
            courtyard |= territory.Arena == ArenaType.Courtyard;
            forest |= territory.Arena == ArenaType.Forest;
            marsh |= territory.Arena == ArenaType.Marsh;
            highlands |= territory.Arena == ArenaType.Highlands;
        }
        return courtyard && forest && marsh && highlands;
    }

    private static ArenaType ArenaOverride(ArenaType fallback)
    {
        foreach (string argument in System.Environment.GetCommandLineArgs())
        {
            const string prefix = "-smokearena=";
            if (argument.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                && System.Enum.TryParse(argument.Substring(prefix.Length), true, out ArenaType arena))
                return arena;
        }
        return fallback;
    }
}
