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

        if (HasArgument("-smokebowreview"))
        {
            yield return RunBowReview();
            Finish();
            yield break;
        }
        if (HasArgument("-smokefeelreview"))
        {
            yield return RunCombatFeelReview();
            Finish();
            yield break;
        }
        if (HasArgument("-smokecommands"))
        {
            yield return RunCommandReview();
            Finish();
            yield break;
        }

        int startingGold = director.Campaign.Gold;
        int startingRoster = director.Campaign.Roster;
        Territory recruitSite = FindRecruitSite(director.Campaign, UnitType.Veteran);
        bool recruited = recruitSite != null
            && director.Campaign.Recruit(UnitType.Veteran, Archetype.Soldier, recruitSite);
        bool variedArenas = HasAllArenaTypes(director.Campaign);
        Require(recruited
            && director.Campaign.Gold == startingGold - UnitCatalog.Cost(UnitType.Veteran)
            && director.Campaign.Roster == startingRoster + 1
            && director.Campaign.DailyIncome() > 0
            && variedArenas, "campaign progression");

        if (HasArgument("-smokeweapons"))
            yield return RunWeaponTrainingAudit();

        BattleSetup setup = director.Campaign.BuildSetupFor(target);
        bool largeRun = HasArgument("-smokelarge");
        bool duelRun = HasArgument("-smokeduel");
        if (largeRun || duelRun)
        {
            // BuildSetupFor pre-populates explicit rosters; clear them so the scalar
            // count overrides below actually drive the spawn (BattleBootstrap prefers a
            // non-empty composition over the counts).
            setup.AllyComposition = null;
            setup.EnemyComposition = null;
        }
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

    private IEnumerator RunWeaponTrainingAudit()
    {
        int gold = director.Campaign.Gold;
        int roster = director.Campaign.Roster;
        int lands = director.Campaign.PlayerTerritoryCount();
        director.Campaign.PlayerWeapon = WeaponType.Bow;
        director.Campaign.TrainingEnemyWeapon = WeaponType.TwoHandedSword;
        BattleSetup setup = director.Campaign.BuildTrainingSetup();
        Require(setup.IsTraining && setup.PlayerWeapon == WeaponType.Bow
            && setup.TrainingEnemyWeapon == WeaponType.TwoHandedSword
            && WeaponCatalog.DefaultFor(UnitType.Militia) == WeaponType.SwordAndShield
            && WeaponCatalog.DefaultFor(UnitType.Veteran) == WeaponType.TwoHandedSword
            && WeaponCatalog.DefaultFor(UnitType.Guard) == WeaponType.Bow, "weapon loadout model");

        director.LaunchBattle(setup);
        yield return WaitForMode(GameDirector.Mode.Battle, "training battle launch");
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        BattleFighter opponent = manager != null ? manager.FindNearestOpponent(manager.Player) : null;
        Require(manager != null && manager.IsTraining && manager.Player.Weapon == WeaponType.Bow
            && opponent != null && opponent.Weapon == WeaponType.TwoHandedSword, "training equipment selection");
        if (manager == null || opponent == null)
            yield break;

        manager.BeginBattle();
        float health = opponent.CurrentHealth;
        manager.Player.DebugAimAt(opponent);
        bool prepared = manager.Player.DebugPrepareAttack(CombatDirection.Thrust);
        float looseSpread = manager.Player.BowCurrentSpreadDegrees;
        Require(prepared && !manager.Player.BowPrecisionReady && looseSpread >= 7f, "bow starts loose before threshold");
        manager.Player.DebugReleasePreparedAttack();
        Require(manager.Player.BowPrecisionReady && manager.Player.BowCurrentSpreadDegrees < looseSpread,
            "held bow becomes more precise");
        yield return new WaitForSeconds(1.35f);
        Require(prepared && opponent.CurrentHealth < health, "bow projectile damages at range");
        manager.DebugEliminateTeam(Team.Enemies);
        yield return new WaitForSeconds(0.3f);
        manager.ConfirmResult();
        yield return WaitForMode(GameDirector.Mode.Map, "training return to map");
        Require(!director.Campaign.CampaignOver && director.Campaign.Gold == gold
            && director.Campaign.Roster == roster && director.Campaign.PlayerTerritoryCount() == lands,
            "training has no campaign consequence");

        director.Campaign.PlayerWeapon = WeaponType.TwoHandedSword;
        director.Campaign.TrainingEnemyWeapon = WeaponType.Bow;
        director.LaunchBattle(director.Campaign.BuildTrainingSetup());
        yield return WaitForMode(GameDirector.Mode.Battle, "ranged NPC training launch");
        manager = Object.FindFirstObjectByType<BattleManager>();
        opponent = manager != null ? manager.FindNearestOpponent(manager.Player) : null;
        Require(manager != null && manager.Player.Weapon == WeaponType.TwoHandedSword
            && opponent != null && opponent.Weapon == WeaponType.Bow, "two-handed player and bow NPC loadouts");
        if (manager == null || opponent == null)
            yield break;
        manager.BeginBattle();
        health = manager.Player.CurrentHealth;
        yield return new WaitForSeconds(6f);
        Debug.Log($"Ranged NPC audit: shots={manager.ProjectileShots}, hits={manager.ProjectileHits}, health={manager.Player.CurrentHealth:0.0}/{health:0.0}, distance={opponent.DistanceTo(manager.Player):0.0}, phase={opponent.Phase}, target={(opponent as AIFighter)?.CurrentTarget?.name}");
        Require(manager.Player.CurrentHealth < health, "bow NPC engages and damages at range");
        manager.DebugEliminateTeam(Team.Enemies);
        yield return new WaitForSeconds(0.3f);
        manager.ConfirmResult();
        yield return WaitForMode(GameDirector.Mode.Map, "ranged NPC training return");
        Require(!director.Campaign.CampaignOver && director.Campaign.Gold == gold
            && director.Campaign.Roster == roster && director.Campaign.PlayerTerritoryCount() == lands,
            "repeated training remains consequence-free");
    }

    private IEnumerator RunBowReview()
    {
        director.Campaign.PlayerWeapon = WeaponType.Bow;
        director.Campaign.TrainingEnemyWeapon = WeaponType.TwoHandedSword;
        director.LaunchBattle(director.Campaign.BuildTrainingSetup());
        yield return WaitForMode(GameDirector.Mode.Battle, "bow review battle launch");
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        BattleFighter opponent = manager != null ? manager.FindNearestOpponent(manager.Player) : null;
        Require(manager != null && opponent != null && manager.Player.Weapon == WeaponType.Bow, "bow review loadout");
        if (manager == null || opponent == null)
            yield break;

        manager.BeginBattle();
        manager.Player.DebugAimAt(opponent);
        bool prepared = manager.Player.DebugPrepareAttack(CombatDirection.Thrust);
        yield return new WaitForSeconds(0.25f);
        float looseSpread = manager.Player.BowCurrentSpreadDegrees;
        Require(prepared && !manager.Player.BowPrecisionReady && manager.Player.BowPrecisionNormalized <= 0.01f,
            "bow precision waits for draw threshold");
        Capture("smoke-bow-early-draw.png");
        yield return new WaitForSeconds(1.15f);
        Require(manager.Player.BowPrecisionReady && manager.Player.BowPrecisionNormalized >= 0.99f
            && manager.Player.BowCurrentSpreadDegrees < looseSpread, "bow precision improves with hold time");
        Capture("smoke-bow-full-draw.png");
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator RunCombatFeelReview()
    {
        BattleSetup heavySetup = BattleSetup.Default();
        heavySetup.AllyCount = 0;
        heavySetup.EnemyCount = 2;
        heavySetup.PlayerWeapon = WeaponType.TwoHandedSword;
        heavySetup.TargetName = "COMBAT FEEL REVIEW";
        director.LaunchBattle(heavySetup);
        yield return WaitForMode(GameDirector.Mode.Battle, "heavy combat-feel battle launch");
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        BattleFighter opponent = manager != null ? manager.FindNearestOpponent(manager.Player) : null;
        Require(manager != null && opponent != null && manager.Player.Weapon == WeaponType.TwoHandedSword,
            "heavy combat-feel loadout");
        if (manager == null || opponent == null)
            yield break;

        manager.BeginBattle();
        bool prepared = manager.Player.DebugPrepareAttack(CombatDirection.Right);
        manager.Player.DebugReleasePreparedAttack();
        yield return new WaitForSeconds(0.16f);
        Require(prepared && manager.HeavyReleases > 0 && manager.Player.DebugTrailEmitting,
            "heavy release audio and sword trail");
        Capture("smoke-heavy-release.png");
        yield return new WaitForEndOfFrame();
        opponent.ReceiveHit(1f, manager.Player, CombatDirection.Left);
        yield return new WaitForSeconds(0.04f);
        Require(opponent.Phase == CombatPhase.HitReaction, "directional hit reaction");
        Capture("smoke-hit-reaction.png");
        yield return new WaitForEndOfFrame();
        opponent.ReceiveHit(10000f, manager.Player, CombatDirection.Left);
        yield return new WaitForSeconds(0.15f);
        Require(!opponent.IsAlive && Mathf.Abs(Mathf.DeltaAngle(opponent.transform.eulerAngles.x, 0f)) > 50f,
            "posed death fall");
        Capture("smoke-death-pose.png");
        yield return new WaitForEndOfFrame();

        BattleSetup bowSetup = BattleSetup.Default();
        bowSetup.AllyCount = 0;
        bowSetup.EnemyCount = 1;
        bowSetup.PlayerWeapon = WeaponType.Bow;
        bowSetup.TargetName = "ARROW IMPACT REVIEW";
        director.LaunchBattle(bowSetup);
        yield return WaitForMode(GameDirector.Mode.Battle, "arrow-impact battle launch");
        manager = Object.FindFirstObjectByType<BattleManager>();
        opponent = manager != null ? manager.FindNearestOpponent(manager.Player) : null;
        Require(manager != null && opponent != null && manager.Player.Weapon == WeaponType.Bow,
            "arrow-impact review loadout");
        if (manager == null || opponent == null)
            yield break;
        manager.BeginBattle();
        manager.Player.DebugAimAt(opponent);
        manager.Player.DebugPrepareAttack(CombatDirection.Thrust);
        manager.Player.DebugReleasePreparedAttack();
        yield return new WaitForSeconds(1.1f);
        GameObject embeddedArrow = GameObject.Find("Allies Arrow");
        Require(manager.BowReleases > 0 && manager.ArrowImpacts > 0 && embeddedArrow != null,
            "bow release audio, arrow impact, and embedded arrow");
        Capture("smoke-arrow-impact.png");
    }

    private IEnumerator RunCommandReview()
    {
        BattleSetup setup = BattleSetup.Default();
        setup.AllyCount = 4;
        setup.AllyMilitia = 4;
        setup.AllyVeterans = 0;
        setup.AllyGuards = 0;
        setup.EnemyCount = 1;
        setup.TargetName = "COMMAND REVIEW";
        director.LaunchBattle(setup);
        yield return WaitForMode(GameDirector.Mode.Battle, "command review battle launch");
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Require(manager != null && manager.CurrentAllyCommand == BattleManager.AllyCommand.Follow
            && manager.CurrentFormation == FormationShape.Line,
            "command battle starts in follow / line");
        if (manager == null)
            yield break;

        manager.BeginBattle();
        yield return new WaitForSeconds(2.2f);
        Require(manager.AlliesNearCommandPositions(2f) >= 3, "follow formation assembles around captain");
        Capture("smoke-command-follow.png");
        yield return new WaitForEndOfFrame();

        manager.SetAllyCommand(BattleManager.AllyCommand.Hold);
        Vector3 previousPlayerPosition = manager.Player.transform.position;
        manager.Player.DebugTeleport(previousPlayerPosition + Vector3.right * 7f);
        yield return new WaitForSeconds(1.4f);
        Require(manager.CurrentAllyCommand == BattleManager.AllyCommand.Hold
            && manager.AlliesNearCommandPositions(1.25f) >= 3, "hold formation remains anchored");
        Capture("smoke-command-hold.png");
        yield return new WaitForEndOfFrame();

        manager.SetAllyCommand(BattleManager.AllyCommand.Charge);
        Require(manager.CurrentAllyCommand == BattleManager.AllyCommand.Charge, "charge releases formation");
        yield return new WaitForSeconds(1f);
        Capture("smoke-command-charge.png");
        yield return new WaitForEndOfFrame();

        // Advance and formation cycling are visual reviews here; the marching-anchor
        // and slot-shape maths are asserted deterministically in PlayMode.
        manager.SetAllyCommand(BattleManager.AllyCommand.Advance);
        Require(manager.CurrentAllyCommand == BattleManager.AllyCommand.Advance, "advance order accepted");
        FormationShape shapeBefore = manager.CurrentFormation;
        manager.CycleFormation();
        Require(manager.CurrentFormation != shapeBefore, "formation cycles to a new shape");
        yield return new WaitForSeconds(1f);
        Capture("smoke-command-advance.png");
        yield return new WaitForEndOfFrame();

        yield return RunHoldFireReview();

        BattleSetup moraleSetup = BattleSetup.Default();
        moraleSetup.AllyCount = 0;
        moraleSetup.EnemyCount = 4;
        moraleSetup.TargetName = "MORALE REVIEW";
        director.LaunchBattle(moraleSetup);
        yield return WaitForMode(GameDirector.Mode.Battle, "morale review battle launch");
        manager = Object.FindFirstObjectByType<BattleManager>();
        Require(manager != null, "morale review manager exists");
        if (manager == null)
            yield break;
        manager.BeginBattle();
        int eliminated = 0;
        foreach (BattleFighter fighter in manager.Fighters)
            if (fighter.Team == Team.Enemies && eliminated++ < 3)
                fighter.ReceiveHit(10000f, manager.Player);
        yield return new WaitForSeconds(0.25f);
        bool enemyBroke = false;
        foreach (BattleFighter fighter in manager.Fighters)
            if (fighter is AIFighter enemy && enemy.Team == Team.Enemies && enemy.IsAlive)
                enemyBroke |= enemy.IsRetreating;
        Require(enemyBroke, "shattered enemy force breaks morale");
        Capture("smoke-morale-break.png");
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(2.5f);
        Require(manager.Retreats == 1 && manager.State == BattleManager.BattleState.Victory,
            "retreating enemy withdraws and ends battle");

        BattleSetup alliedMoraleSetup = BattleSetup.Default();
        alliedMoraleSetup.AllyCount = 4;
        alliedMoraleSetup.AllyMilitia = 4;
        alliedMoraleSetup.EnemyCount = 4;
        alliedMoraleSetup.TargetName = "ALLIED MORALE REVIEW";
        director.LaunchBattle(alliedMoraleSetup);
        yield return WaitForMode(GameDirector.Mode.Battle, "allied morale review battle launch");
        manager = Object.FindFirstObjectByType<BattleManager>();
        Require(manager != null, "allied morale review manager exists");
        if (manager == null)
            yield break;
        manager.BeginBattle();
        eliminated = 0;
        foreach (BattleFighter fighter in manager.Fighters)
            if (fighter.Team == Team.Allies && !fighter.IsPlayer && eliminated++ < 3)
                fighter.ReceiveHit(10000f, manager.Player);
        yield return new WaitForSeconds(2.5f);
        Require(manager.Retreats == 1 && manager.AlliedSoldiersAlive == 1,
            "withdrawn ally remains a campaign survivor");
    }

    // An allied archer holds fire (keeps positioning, never looses) until released.
    // Charge is issued so the archer engages with ranged AI rather than holding a
    // formation slot, which is what makes the loose/hold distinction observable.
    private IEnumerator RunHoldFireReview()
    {
        BattleSetup setup = BattleSetup.Default();
        setup.AllyCount = 0;
        setup.AllyComposition = new List<UnitSpec> { new UnitSpec(UnitType.Militia, Archetype.Soldier, WeaponType.Bow) };
        setup.EnemyCount = 1;
        setup.TargetName = "HOLD FIRE REVIEW";
        director.LaunchBattle(setup);
        yield return WaitForMode(GameDirector.Mode.Battle, "hold-fire battle launch");
        BattleManager manager = Object.FindFirstObjectByType<BattleManager>();
        Require(manager != null, "hold-fire manager exists");
        if (manager == null)
            yield break;
        manager.BeginBattle();
        manager.SetAllyCommand(BattleManager.AllyCommand.Charge);
        manager.ToggleHoldFire();
        Require(manager.AllyHoldFire, "hold-fire engages for allied archers");
        int heldShots = manager.ProjectileShots;
        yield return new WaitForSeconds(2.5f);
        Require(manager.ProjectileShots == heldShots, "held allied archers do not loose");
        manager.ToggleHoldFire();
        Require(!manager.AllyHoldFire, "hold-fire releases");
        yield return new WaitForSeconds(2.5f);
        Require(manager.ProjectileShots > heldShots, "released allied archers loose again");
        Capture("smoke-command-holdfire.png");
        yield return new WaitForEndOfFrame();
    }

    private IEnumerator RunCampaignConquests(int desiredTerritories)
    {
        int rounds = 0;
        while (director.Campaign.PlayerTerritoryCount() < desiredTerritories && rounds++ < 5)
        {
            director.Campaign.Recruit(UnitType.Militia, Archetype.Soldier, FindRecruitSite(director.Campaign, UnitType.Militia));
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

    // First settlement on the map that offers volunteers of at least the given tier
    // and still has any in its pool — used so the smoke can recruit deterministically.
    private static Territory FindRecruitSite(CampaignState campaign, UnitType tier)
    {
        foreach (Territory territory in campaign.Territories)
            if (territory.Recruits > 0 && SettlementCatalog.Allows(territory.Settlement, tier))
                return territory;
        return null;
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
