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
        int startingGold = director.Campaign.Gold;
        int startingRoster = director.Campaign.Roster;
        bool recruited = director.Campaign.Recruit(UnitType.Veteran);
        bool variedArenas = HasAllArenaTypes(director.Campaign);
        bool campaignAudit = recruited
            && director.Campaign.Gold == startingGold - UnitCatalog.Cost(UnitType.Veteran)
            && director.Campaign.Roster == startingRoster + 1
            && director.Campaign.IncomePerVictory() > 0
            && variedArenas;
        Debug.Log($"Runtime smoke campaign progression: passed={campaignAudit}, recruited={recruited}, gold={director.Campaign.Gold}, roster={director.Campaign.Roster}, variedArenas={variedArenas}");
        BattleSetup setup = director.Campaign.BuildSetupFor(target);
        bool largeRun = System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokelarge");
        if (largeRun)
        {
            setup.AllyCount = 5;
            setup.AllyMilitia = 5;
            setup.AllyVeterans = 0;
            setup.AllyGuards = 0;
            setup.EnemyCount = 6;
        }
        setup.Arena = ArenaOverride(setup.Arena);
        Debug.Log($"Runtime smoke encounter: allies={setup.AllyCount + 1}, enemies={setup.EnemyCount}, arena={setup.Arena}, large={largeRun}");
        director.LaunchBattle(setup, target);

        while (director.CurrentMode != GameDirector.Mode.Battle)
            yield return null; // wait for the battle to finish building
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        manager.BeginBattle();
        bool directionalAudit = manager.DebugAuditDirectionalBlock();
        Debug.Log($"Runtime smoke directional combat: passed={directionalAudit}");
        bool responsiveAudit = manager.DebugAuditResponsiveCombat();
        Debug.Log($"Runtime smoke responsive combat: passed={responsiveAudit}");
        yield return new WaitForSeconds(1.5f);
        Capture("smoke-opening.png");
        Debug.Log($"Runtime smoke opening: {manager.DebugSummary}, {manager.DebugAISummary}");

        bool victoryRun = System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokevictory");
        bool campaignRun = System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smokecampaign");
        if (victoryRun)
        {
            manager.DebugEliminateTeam(Team.Enemies);
            yield return new WaitForSeconds(1f);
            Capture("smoke-victory.png");
            Debug.Log($"Runtime smoke victory: {manager.DebugSummary}");
            manager.ConfirmResult(); // dismiss result -> return to map
            while (director.CurrentMode != GameDirector.Mode.Map)
                yield return null;
            bool economyAdvanced = director.Campaign.Gold > startingGold - UnitCatalog.Cost(UnitType.Veteran)
                && target.Owner == TerritoryOwner.Player;
            Debug.Log($"Runtime smoke return: mode={director.CurrentMode}, target owner={target.Owner}, roster={director.Campaign.Roster}, gold={director.Campaign.Gold}, economyAdvanced={economyAdvanced}");
            if (campaignRun)
                yield return RunCampaignConquests(4);
            Capture("smoke-map.png");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
            yield break;
        }

        yield return new WaitForSeconds(5.5f);
        Capture("smoke-combat.png");
        bool coordinationAudit = manager.DebugAuditAICoordination();
        Debug.Log($"Runtime smoke AI coordination: passed={coordinationAudit}");
        Debug.Log($"Runtime smoke combat: {manager.DebugSummary}, {manager.DebugAISummary}");
        yield return new WaitForSeconds(16.5f);
        Capture("smoke-battle.png");
        Debug.Log($"Runtime smoke battle: {manager.DebugSummary}, {manager.DebugAISummary}");
        yield return new WaitForSeconds(1f);
        Application.Quit();
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
            BattleSetup setup = director.Campaign.BuildSetupFor(next);
            director.LaunchBattle(setup, next);
            while (director.CurrentMode != GameDirector.Mode.Battle)
                yield return null;
            BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
            manager.BeginBattle();
            yield return new WaitForSeconds(0.4f);
            manager.DebugEliminateTeam(Team.Enemies);
            yield return new WaitForSeconds(0.4f);
            manager.ConfirmResult();
            while (director.CurrentMode != GameDirector.Mode.Map)
                yield return null;
            Debug.Log($"Runtime smoke campaign step: lands={director.Campaign.PlayerTerritoryCount()}, gold={director.Campaign.Gold}, roster={director.Campaign.Roster}, captured={next.Name}, arena={setup.Arena}");
        }

        bool passed = director.Campaign.PlayerTerritoryCount() >= desiredTerritories
            && director.Campaign.Gold >= 0 && director.Campaign.Roster > 0;
        Debug.Log($"Runtime smoke multi-conquest: passed={passed}, lands={director.Campaign.PlayerTerritoryCount()}, gold={director.Campaign.Gold}, roster={director.Campaign.Roster}");
    }

    private static void Capture(string filename)
    {
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
