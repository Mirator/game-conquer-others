using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Public gameplay facade: owns the fighter registry, the battle state machine
// (Ready/Fighting/Victory/Defeat), telemetry, ally commands, and feedback routing.
public sealed class BattleManager : MonoBehaviour
{
    public enum BattleState { Ready, Fighting, Victory, Defeat }
    public enum AllyCommand { Follow, Hold, Charge, Advance }

    // The field stays live (fighters move, act, simulate) both during the fight
    // and while a win awaits its prompt, so the player can roam the won battlefield.
    public bool IsBattleRunning => State == BattleState.Fighting || AwaitingVictoryAck;
    public BattleState State { get; private set; } = BattleState.Ready;
    // A won battle first shows a corner "press to continue" prompt over the live
    // field; the full result screen waits until the player acknowledges it.
    public bool AwaitingVictoryAck => State == BattleState.Victory && !victoryAcknowledged;
    // The captain's command/formation layer lives in BattleCommands; these stay the
    // public surface as facades. They read defaults until a battle is configured.
    public AllyCommand CurrentAllyCommand => commands?.CurrentAllyCommand ?? AllyCommand.Follow;
    public FormationShape CurrentFormation => commands?.CurrentFormation ?? FormationShape.Line;
    public bool AllyHoldFire => commands != null && commands.AllyHoldFire;
    public float FormationSpeedScale => commands?.FormationSpeedScale ?? FormationBalance.SpeedScale(FormationShape.Line);
    public PlayerFighter Player { get; private set; }
    public int AlliesAlive => CountAlive(Team.Allies);
    public int EnemiesAlive => CountAlive(Team.Enemies);
    public string DebugAISummary => tactics?.DebugSummary ?? "";
    public bool IsTraining { get; private set; }

    // Surviving allied soldiers excluding the player, including withdrawals.
    // This is what carries to the campaign roster.
    public int AlliedSoldiersAlive
    {
        get
        {
            int count = 0;
            foreach (BattleFighter fighter in fighters)
                if (fighter.SurvivedBattle && fighter.Team == Team.Allies && !fighter.IsPlayer)
                    count++;
            return count;
        }
    }

    public int CountAlliedSurvivors(UnitType type)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter.SurvivedBattle && fighter.Team == Team.Allies && !fighter.IsPlayer && fighter.UnitType == type)
                count++;
        return count;
    }

    // Allied survivors tallied by tier x archetype, so the warband keeps its
    // archetype makeup across battles.
    private List<RosterEntry> BuildAlliedSurvivors()
    {
        List<RosterEntry> survivors = new();
        foreach (BattleFighter fighter in fighters)
        {
            if (fighter == null || fighter.IsPlayer || fighter.Team != Team.Allies || !fighter.SurvivedBattle)
                continue;
            bool merged = false;
            foreach (RosterEntry entry in survivors)
                if (entry.Tier == fighter.UnitType && entry.Archetype == fighter.Archetype)
                {
                    entry.Count++;
                    merged = true;
                    break;
                }
            if (!merged)
                survivors.Add(new RosterEntry { Tier = fighter.UnitType, Archetype = fighter.Archetype, Count = 1 });
        }
        return survivors;
    }

    // Raised when the player dismisses a result screen. The GameDirector listens
    // and applies the outcome to the campaign.
    public System.Action<BattleResult> OnBattleConcluded;
    public string EncounterTitle = "THE OLD COURTYARD";
    public BattleKind EncounterKind = BattleKind.SettlementAssault;
    public string DebugSummary => $"State={State}, Blue={CountAlive(Team.Allies)}, Red={CountAlive(Team.Enemies)}, Time={battleTime:0.0}";

    private readonly List<BattleFighter> fighters = new();
    private readonly List<BattleFighter> strikeScratch = new(); // reused by FindSweptStrikeTarget's grid query
    private BattleTactics tactics;
    private BattleCommands commands;
    private BattleEffects effects;
    private ThirdPersonCamera cameraRig;
    private BattleDecals decals;
    private readonly Queue<GameObject> embeddedArrows = new();
    private const int MaxEmbeddedArrows = 50;
    private float battleTime;
    private float telemetryTimer;
    private float impactFlash;
    private Color impactFlashColor = new Color(0.7f, 0.02f, 0.01f);
    private float messageTimer;
    private string message;
    private bool concluded;
    private bool victoryAcknowledged;
    private float playerDamageDealt;
    private float alliesDamageDealt;
    private float enemiesDamageDealt;
    private float playerDamageTaken;
    private int playerKills;
    private int playerPerfectBlocks;
    private int playerCounterHits;
    private int projectileShots;
    private int projectileHits;
    private int arrowImpacts;
    private int bowReleases;
    private int heavyReleases;
    private int retreats;
    private int initialAllies;
    private int initialEnemies;

    public void Configure(BattleEffects battleEffects, ThirdPersonCamera rig, bool training = false)
    {
        effects = battleEffects;
        cameraRig = rig;
        IsTraining = training;
        tactics = new BattleTactics(fighters, () => Player);
        commands = new BattleCommands(fighters, () => Player, () => IsTraining, ShowOrderMessage);
        gameObject.AddComponent<BattleHud>().Configure(this);
    }

    private void ShowOrderMessage(string text)
    {
        message = text;
        messageTimer = 1.2f;
    }

    public void SetDecals(BattleDecals battleDecals) => decals = battleDecals;

    // Keeps embedded arrows on the field but capped — the oldest are removed once the
    // count exceeds the cap, so accumulation stays bounded over a long fight.
    public void RegisterEmbeddedArrow(GameObject arrow)
    {
        if (arrow == null)
            return;
        embeddedArrows.Enqueue(arrow);
        while (embeddedArrows.Count > MaxEmbeddedArrows)
        {
            GameObject oldest = embeddedArrows.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    public void Register(BattleFighter fighter)
    {
        fighters.Add(fighter);
        if (fighter is PlayerFighter player)
            Player = player;
    }

    private void Update()
    {
        if (State == BattleState.Fighting)
        {
            battleTime += Time.deltaTime;
            impactFlash = Mathf.MoveTowards(impactFlash, 0f, Time.deltaTime * 3.5f);
            messageTimer -= Time.deltaTime;
            telemetryTimer -= Time.deltaTime;
            if (telemetryTimer <= 0f)
            {
                telemetryTimer = 0.25f;
                tactics.UpdateTelemetry();
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Follow);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Hold);
                else if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Charge);
                else if (Keyboard.current.digit4Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Advance);
                if (Keyboard.current.fKey.wasPressedThisFrame)
                    CycleFormation();
                if (Keyboard.current.hKey.wasPressedThisFrame)
                    ToggleHoldFire();
            }

            commands.TickAdvance();
        }

        if (Keyboard.current == null)
            return;
        // Mouse input starts the battle. Result screens are dismissed only via
        // their on-screen button so campaign outcomes cannot be bypassed.
        if (State == BattleState.Ready && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (cameraRig != null && cameraRig.IsSweeping)
                cameraRig.SkipSweep(); // first click skips the intro flyover
            else
                BeginBattle();
        }
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GameDirector.Instance?.TogglePause();
        }
        // A win pauses on the battlefield behind a corner prompt; E reveals the
        // full result screen (defeat skips this and shows its screen at once).
        if (AwaitingVictoryAck && Keyboard.current.eKey.wasPressedThisFrame)
            AcknowledgeVictory();
        if (State == BattleState.Fighting && (GameDirector.Instance == null || !GameDirector.Instance.IsPaused)
            && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();
    }

    public BattleFighter FindNearestOpponent(BattleFighter seeker)
    {
        BattleFighter best = null;
        float bestScore = float.MaxValue;
        foreach (BattleFighter fighter in fighters)
        {
            if (!fighter.IsAlive || fighter.Team == seeker.Team)
                continue;
            float score = (fighter.transform.position - seeker.transform.position).sqrMagnitude;
            if (fighter.IsPlayer)
                score *= 1.12f;
            if (score < bestScore)
            {
                bestScore = score;
                best = fighter;
            }
        }
        return best;
    }

    public BattleFighter SelectTacticalTarget(AIFighter seeker, BattleFighter current)
        => tactics?.SelectTarget(seeker, current);

    public BattleFighter FindIncomingThreat(BattleFighter defender)
    {
        BattleFighter best = null;
        float bestDistance = float.MaxValue;
        foreach (BattleFighter fighter in fighters)
        {
            if (fighter is not AIFighter ai || !ai.IsAlive || ai.Team == defender.Team
                || ai.CurrentTarget != defender || !ai.IsAttackThreatening)
                continue;
            float distance = ai.DistanceTo(defender);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = ai;
            }
        }
        return best;
    }

    public bool TryClaimAttackPermission(AIFighter attacker, BattleFighter target)
        => tactics != null && tactics.TryClaimAttackPermission(attacker, target);

    // tactics is null only outside a configured/running battle (e.g. teardown), where a
    // release is a harmless no-op — guarding here fixes a teardown NullReferenceException.
    public void ReleaseAttackPermission(AIFighter attacker)
        => tactics?.ReleaseAttackPermission(attacker);

    public Vector3 GetEngagementPosition(AIFighter seeker, BattleFighter target, bool activeAttacker, float preferredRange)
        => tactics != null
            ? tactics.GetEngagementPosition(seeker, target, activeAttacker, preferredRange)
            : seeker.transform.position;

    // Only fighters near the strike segment can be hit, so query the spatial grid around
    // the segment midpoint instead of scanning the whole roster (O(k) vs O(n) per swing).
    public BattleFighter FindSweptStrikeTarget(BattleFighter attacker, Vector3 start, Vector3 end, float radius)
    {
        BattleFighter best = null;
        float bestDistance = radius * radius;
        List<BattleFighter> candidates = fighters;
        if (tactics != null)
        {
            tactics.QueryNeighbors((start + end) * 0.5f, strikeScratch);
            candidates = strikeScratch;
        }
        for (int i = 0; i < candidates.Count; i++)
        {
            BattleFighter fighter = candidates[i];
            if (!fighter.IsAlive || fighter.Team == attacker.Team)
                continue;
            Vector3 center = fighter.transform.position + Vector3.up * 1.05f;
            float distance = SqrDistanceToSegment(center, start, end);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = fighter;
            }
        }
        return best;
    }

    public Vector3 GetSeparation(BattleFighter seeker)
        => tactics != null ? tactics.GetSeparation(seeker) : Vector3.zero;

    public bool TryGetCommandPosition(AIFighter ally, BattleFighter target, out Vector3 position)
        => commands.TryGetCommandPosition(ally, target, out position);

    public void SetAllyCommand(AllyCommand command) => commands.SetAllyCommand(command);

    public void CycleFormation() => commands.CycleFormation();

    public void ToggleHoldFire() => commands.ToggleHoldFire();

    public static string FormationName(FormationShape shape) => shape switch
    {
        FormationShape.ShieldWall => "SHIELD WALL",
        FormationShape.Skirmish => "SKIRMISH",
        _ => "LINE"
    };

    // Test/diagnostic hook: the captain-relative slot an ally would move to under
    // the current order and formation.
    public Vector3 DebugFormationPosition(AIFighter ally) => commands.GetFormationPosition(ally);

    internal int AllyFormationIndex(AIFighter ally) => commands.AllyFormationIndex(ally);

    public void PlayAttackSound(Vector3 position, bool player, WeaponType weapon)
    {
        if (weapon == WeaponType.Bow)
            bowReleases++;
        else if (weapon == WeaponType.TwoHandedSword)
            heavyReleases++;
        effects?.PlayAttack(position, player, weapon);
    }

    public void PlayFootstep(Vector3 position, bool player) => effects?.PlayFootstep(position, player);

    public void ReportWhiff(BattleFighter attacker) => effects?.PlayWhiff(attacker.transform.position, attacker.IsPlayer);

    public void SpawnArrow(BattleFighter attacker, Vector3 position, Vector3 direction, float damage)
    {
        projectileShots++;
        GameObject arrow = new GameObject($"{attacker.Team} Arrow");
        arrow.transform.SetParent(transform);
        arrow.transform.position = position;
        arrow.AddComponent<BattleProjectile>().Configure(this, attacker, direction, damage);
    }

    // Extra weight for a lethal blow: a meaty finisher pause on the player's
    // killing strike, a blood burst, and a camera kick when the player is
    // involved. The killing hit's normal impact feedback fires via ReportImpact.
    public void ReportKill(BattleFighter attacker, BattleFighter victim)
    {
        if (victim == null)
            return;
        Vector3 blow = attacker != null ? victim.transform.position - attacker.transform.position : Vector3.zero;
        effects?.PlayKill(victim.transform.position, blow);
        decals?.AddBlood(victim.transform.position, Random.Range(1.6f, 2.6f));
        if (Random.value < 0.4f)
            decals?.AddDebris(victim.transform.position + Random.insideUnitSphere * 0.7f);
        if (attacker != null && attacker.IsPlayer)
            attacker.ApplyHitStop(0.16f);
        if (victim.IsPlayer || (attacker != null && attacker.IsPlayer))
        {
            cameraRig?.AddShake(victim.IsPlayer ? 0.24f : 0.17f);
            // A weighty finisher zoom on the player's killing blow; a harder shove
            // (and slightly smaller zoom) when it is the player who falls.
            cameraRig?.AddImpulse(victim.IsPlayer ? 5f : 6f, victim.IsPlayer ? 0.3f : 0f, blow);
        }
    }

    public BattleFighter FindProjectileTarget(BattleFighter attacker, Vector3 start, Vector3 end, float radius)
        => FindSweptStrikeTarget(attacker, start, end, radius);

    public void ReportProjectileHit() => projectileHits++;

    public void ReportArrowImpact(Vector3 position, bool fighterHit)
    {
        arrowImpacts++;
        effects?.PlayArrowImpact(position, fighterHit);
    }

    public void ReportImpact(BattleFighter target, BattleFighter attacker, bool blocked, bool perfectBlock, bool counterStrike, float appliedDamage = 0f, bool guardBroken = false)
    {
        Vector3 blow = attacker != null ? target.transform.position - attacker.transform.position : Vector3.zero;
        effects?.PlayImpact(target.transform.position, blocked, perfectBlock, counterStrike, blow);
        bool playerInvolved = target.IsPlayer || attacker != null && attacker.IsPlayer;
        if (playerInvolved)
        {
            cameraRig?.AddShake(perfectBlock ? 0.13f : counterStrike ? 0.095f : target.IsPlayer ? 0.11f : blocked ? 0.065f : 0.045f);
            // A crisp zoom-in punctuates the beat — sharp on a perfect block or counter,
            // heaviest when the player takes an unblocked blow (which also shoves the
            // view along the strike). Landing a hit on an AI gives a lighter kick.
            float fovPunch = perfectBlock ? 3.5f : counterStrike ? 3f
                : target.IsPlayer ? (blocked ? 1.6f : 4.5f) : 2.4f;
            float kick = target.IsPlayer && !blocked && !perfectBlock ? 0.22f : 0f;
            cameraRig?.AddImpulse(fovPunch, kick, blow);
        }
        // A shattered guard reads as its own beat: a heavier shatter cue on top of
        // the landed-hit impact, plus an amber jolt if it was the player's guard.
        if (guardBroken)
        {
            effects?.PlayGuardBreak(target.transform.position);
            if (playerInvolved)
            {
                cameraRig?.AddShake(0.08f);
                cameraRig?.AddImpulse(1.8f);
            }
        }
        if (perfectBlock)
        {
            target.ApplyHitStop(0.075f);
            attacker?.ApplyHitStop(0.075f);
        }
        else if (!blocked && attacker != null && attacker.IsPlayer)
        {
            // Heavier landed hits freeze longer, so a two-handed blow reads
            // weightier than a quick thrust.
            float duration = Mathf.Lerp(0.05f, 0.12f, Mathf.Clamp01(appliedDamage / 45f));
            if (counterStrike)
                duration += 0.02f;
            attacker.ApplyHitStop(duration);
            target.ApplyHitStop(duration);
        }
        if (target.IsPlayer)
        {
            impactFlash = perfectBlock ? 0.16f : blocked ? 0.1f : 0.28f;
            impactFlashColor = perfectBlock ? new Color(0.28f, 0.82f, 1f)
                : blocked ? new Color(0.9f, 0.68f, 0.18f) : new Color(0.7f, 0.02f, 0.01f);
        }
        if (blocked && target.IsPlayer)
        {
            message = perfectBlock ? "PERFECT BLOCK - COUNTER READY" : "DIRECTIONAL BLOCK";
            messageTimer = perfectBlock ? 0.9f : 0.5f;
            if (perfectBlock)
                playerPerfectBlocks++;
        }
        if (counterStrike && attacker != null && attacker.IsPlayer && !blocked)
        {
            message = "COUNTER STRIKE";
            messageTimer = 0.7f;
            playerCounterHits++;
        }
    }

    public void NotifyDeath(BattleFighter dead)
    {
        tactics?.OnFighterRemoved(dead);
        if (dead is AIFighter deadAi)
            commands?.OnFighterRemoved(deadAi);
        // The outcome latches on the first death that resolves it, and a dead player
        // always resolves to Defeat. This keeps a same-frame mutual kill (the player and
        // the last enemy dying together) deterministic regardless of processing order.
        if (State == BattleState.Victory || State == BattleState.Defeat)
            return;
        if (dead.IsPlayer || (Player != null && !Player.IsAlive))
        {
            State = BattleState.Defeat;
            UnlockCursor();
            return;
        }
        if (CountAlive(Team.Enemies) == 0)
        {
            State = BattleState.Victory;
            effects?.PlayVictory();
            // Cursor stays locked through the "press E" prompt so the player can
            // keep moving and looking; it unlocks in AcknowledgeVictory.
        }
    }

    public bool ShouldRetreat(AIFighter fighter)
    {
        if (fighter == null || fighter.IsRetreating || IsTraining)
            return false;
        int initial = fighter.Team == Team.Allies ? initialAllies : initialEnemies;
        if (initial < 3)
            return false;
        int allies = CountAlive(fighter.Team);
        int opponents = CountAlive(fighter.Team == Team.Allies ? Team.Enemies : Team.Allies);
        // Braver archetypes hold the line longer; the thresholds shrink as
        // bravery rises (bravery 1 leaves the original 0.25/0.5 fractions).
        float bravery = Mathf.Max(0.01f, fighter.Profile != null ? fighter.Profile.retreatBravery : 1f);
        bool shattered = allies <= Mathf.CeilToInt(initial * 0.25f / bravery);
        bool badlyOutnumbered = allies <= Mathf.CeilToInt(initial * 0.5f / bravery) && allies * 2 <= opponents;
        return shattered || badlyOutnumbered;
    }

    public void NotifyRetreat(AIFighter fighter)
    {
        if (fighter == null || !fighter.IsAlive)
            return;
        tactics?.OnFighterRemoved(fighter);
        commands?.OnFighterRemoved(fighter);
        Team team = fighter.Team;
        fighter.WithdrawFromBattle();
        retreats++;
        message = team == Team.Allies ? "AN ALLY HAS RETREATED" : "THE RED LINE IS BREAKING";
        messageTimer = 1.2f;
        if (team == Team.Enemies && CountAlive(Team.Enemies) == 0)
        {
            State = BattleState.Victory;
            effects?.PlayVictory();
            // Cursor stays locked through the "press E" prompt (see NotifyDeath).
        }
    }

    public void NotifyMoraleBreak(Team team)
    {
        message = team == Team.Allies ? "ALLIED MORALE IS BREAKING" : "THE RED LINE IS BREAKING";
        messageTimer = 1.5f;
    }

    // Reveals the full victory result screen after the player dismisses the
    // bottom-corner "battle won" prompt. No-op outside of victory.
    public void AcknowledgeVictory()
    {
        if (State != BattleState.Victory)
            return;
        victoryAcknowledged = true;
        UnlockCursor(); // hand the cursor to the result screen's button
    }

    // Dismisses the result screen and reports the outcome to the GameDirector.
    // Guarded so it fires once even if invoked across multiple frames.
    public void ConfirmResult()
    {
        if (concluded || (State != BattleState.Victory && State != BattleState.Defeat))
            return;
        concluded = true;
        OnBattleConcluded?.Invoke(new BattleResult
        {
            PlayerWon = State == BattleState.Victory,
            MilitiaSurvived = CountAlliedSurvivors(UnitType.Militia),
            VeteransSurvived = CountAlliedSurvivors(UnitType.Veteran),
            GuardsSurvived = CountAlliedSurvivors(UnitType.Guard),
            SurvivingUnits = BuildAlliedSurvivors()
        });
    }

    public void DebugEliminateTeam(Team team)
    {
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive && fighter.Team == team)
            {
                fighter.DebugSetBlock(false, CombatDirection.Right);
                fighter.ReceiveHit(10000f, Player);
            }
    }

    public void DebugClearCombatMessage()
    {
        message = null;
        messageTimer = 0f;
        impactFlash = 0f;
    }

    public bool DebugAuditAICoordination()
    {
        bool passed = tactics.AuditCoordination();
        Debug.Log($"AI coordination audit: passed={passed}, {DebugAISummary}");
        return passed;
    }

    internal int CountAlive(Team team)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive && fighter.Team == team)
                count++;
        return count;
    }

    private static float SqrDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.0001f)
            return (point - start).sqrMagnitude;
        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
        return (point - (start + segment * t)).sqrMagnitude;
    }

    public void BeginBattle()
    {
        State = BattleState.Fighting;
        battleTime = 0f;
        telemetryTimer = 0f;
        initialAllies = CountAlive(Team.Allies);
        initialEnemies = CountAlive(Team.Enemies);
        message = IsTraining ? "SPAR WITH YOUR OPPONENT" : "BREAK THE RED LINE";
        messageTimer = 2.2f;
        LockCursor();
        decals?.SeedClashZone();
    }

    // Attributes applied (unguarded) damage to its source and victim for the
    // end-of-battle scoreboard.
    public void RecordDamage(BattleFighter attacker, BattleFighter victim, float amount, bool killed)
    {
        if (attacker == null || victim == null || amount <= 0f || victim.Team == attacker.Team)
            return;
        if (attacker.IsPlayer)
            playerDamageDealt += amount;
        else if (attacker.Team == Team.Allies)
            alliesDamageDealt += amount;
        else
            enemiesDamageDealt += amount;
        if (victim.IsPlayer)
            playerDamageTaken += amount;
        if (killed && attacker.IsPlayer)
            playerKills++;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    internal IReadOnlyList<BattleFighter> Fighters => fighters;
    internal float BattleTime => battleTime;
    internal float ImpactFlash => impactFlash;
    internal Color ImpactFlashColor => impactFlashColor;
    internal float MessageTimer => messageTimer;
    internal string Message => message;
    internal float PlayerDamageDealt => playerDamageDealt;
    internal float AlliesDamageDealt => alliesDamageDealt;
    internal float PlayerDamageTaken => playerDamageTaken;
    internal int PlayerKills => playerKills;
    internal int PlayerPerfectBlocks => playerPerfectBlocks;
    internal int PlayerCounterHits => playerCounterHits;
    internal int InitialAllies => initialAllies;
    internal int InitialEnemies => initialEnemies;
    internal int ProjectileShots => projectileShots;
    internal int ProjectileHits => projectileHits;
    internal int ArrowImpacts => arrowImpacts;
    internal int BowReleases => bowReleases;
    internal int HeavyReleases => heavyReleases;
    internal int Retreats => retreats;
    internal int AlliesNearCommandPositions(float tolerance) => commands.AlliesNearCommandPositions(tolerance);
}
