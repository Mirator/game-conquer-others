using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BattleManager : MonoBehaviour
{
    public enum BattleState { Ready, Fighting, Victory, Defeat }

    public bool IsBattleRunning => State == BattleState.Fighting;
    public BattleState State { get; private set; } = BattleState.Ready;
    public PlayerFighter Player { get; private set; }
    public int AlliesAlive => CountAlive(Team.Allies);
    public int EnemiesAlive => CountAlive(Team.Enemies);
    public string DebugAISummary => tactics.DebugSummary;

    // Living allied soldiers excluding the player — this is what carries to the
    // campaign roster (the player/captain is tracked separately).
    public int AlliedSoldiersAlive
    {
        get
        {
            int count = 0;
            foreach (BattleFighter fighter in fighters)
                if (fighter.IsAlive && fighter.Team == Team.Allies && !fighter.IsPlayer)
                    count++;
            return count;
        }
    }

    public int CountAlliedSurvivors(UnitType type)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive && fighter.Team == Team.Allies && !fighter.IsPlayer && fighter.UnitType == type)
                count++;
        return count;
    }

    // Raised when the player dismisses a result screen. The GameDirector listens
    // and applies the outcome to the campaign.
    public System.Action<BattleResult> OnBattleConcluded;
    public string EncounterTitle = "THE OLD COURTYARD";
    public string DebugSummary => $"State={State}, Blue={CountAlive(Team.Allies)}, Red={CountAlive(Team.Enemies)}, Time={battleTime:0.0}";

    private readonly List<BattleFighter> fighters = new();
    private BattleTactics tactics;
    private BattleEffects effects;
    private ThirdPersonCamera cameraRig;
    private float battleTime;
    private float telemetryTimer;
    private float impactFlash;
    private Color impactFlashColor = new Color(0.7f, 0.02f, 0.01f);
    private float messageTimer;
    private string message;
    private bool concluded;
    private float playerDamageDealt;
    private float alliesDamageDealt;
    private float enemiesDamageDealt;
    private float playerDamageTaken;
    private int playerKills;
    private int playerPerfectBlocks;
    private int playerCounterHits;
    private int initialAllies;
    private int initialEnemies;

    public void Configure(BattleEffects battleEffects, ThirdPersonCamera rig)
    {
        effects = battleEffects;
        cameraRig = rig;
        tactics = new BattleTactics(fighters, () => Player);
        gameObject.AddComponent<BattleHud>().Configure(this);
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
        }

        if (Keyboard.current == null)
            return;
        // Mouse input starts the battle. Result screens are dismissed only via
        // their on-screen button so campaign outcomes cannot be bypassed.
        if (State == BattleState.Ready && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            BeginBattle();
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (State == BattleState.Fighting && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
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
        => tactics.SelectTarget(seeker, current);

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
        => tactics.TryClaimAttackPermission(attacker, target);

    public void ReleaseAttackPermission(AIFighter attacker)
        => tactics.ReleaseAttackPermission(attacker);

    public Vector3 GetEngagementPosition(AIFighter seeker, BattleFighter target, bool activeAttacker, float preferredRange)
        => tactics.GetEngagementPosition(seeker, target, activeAttacker, preferredRange);

    public BattleFighter FindSweptStrikeTarget(BattleFighter attacker, Vector3 start, Vector3 end, float radius)
    {
        BattleFighter best = null;
        float bestDistance = radius * radius;
        foreach (BattleFighter fighter in fighters)
        {
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
        => tactics.GetSeparation(seeker);

    public void PlayAttackSound(Vector3 position, bool player) => effects?.PlaySwing(position, player);

    public void PlayFootstep(Vector3 position, bool player) => effects?.PlayFootstep(position, player);

    public void ReportWhiff(BattleFighter attacker) => effects?.PlayWhiff(attacker.transform.position, attacker.IsPlayer);

    public void ReportImpact(BattleFighter target, BattleFighter attacker, bool blocked, bool perfectBlock, bool counterStrike)
    {
        effects?.PlayImpact(target.transform.position, blocked, perfectBlock, counterStrike);
        bool playerInvolved = target.IsPlayer || attacker != null && attacker.IsPlayer;
        if (playerInvolved)
            cameraRig?.AddShake(perfectBlock ? 0.13f : counterStrike ? 0.095f : target.IsPlayer ? 0.11f : blocked ? 0.065f : 0.045f);
        if (perfectBlock)
        {
            target.ApplyHitStop(0.075f);
            attacker?.ApplyHitStop(0.075f);
        }
        else if (!blocked && attacker != null && attacker.IsPlayer)
        {
            float duration = counterStrike ? 0.085f : 0.055f;
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
        if (dead.IsPlayer)
        {
            State = BattleState.Defeat;
            UnlockCursor();
            return;
        }
        if (CountAlive(Team.Enemies) == 0)
        {
            State = BattleState.Victory;
            effects?.PlayVictory();
            UnlockCursor();
        }
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
            AlliesSurvived = AlliedSoldiersAlive,
            MilitiaSurvived = CountAlliedSurvivors(UnitType.Militia),
            VeteransSurvived = CountAlliedSurvivors(UnitType.Veteran),
            GuardsSurvived = CountAlliedSurvivors(UnitType.Guard)
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
        message = "BREAK THE RED LINE";
        messageTimer = 2.2f;
        LockCursor();
    }

    // Attributes applied (unguarded) damage to its source and victim for the
    // end-of-battle scoreboard.
    public void RecordDamage(BattleFighter attacker, BattleFighter victim, float amount, bool killed)
    {
        if (attacker == null || amount <= 0f)
            return;
        if (attacker.IsPlayer)
            playerDamageDealt += amount;
        else if (attacker.Team == Team.Allies)
            alliesDamageDealt += amount;
        else
            enemiesDamageDealt += amount;
        if (victim.IsPlayer)
            playerDamageTaken += amount;
        if (killed && attacker.IsPlayer && victim.Team != attacker.Team)
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
}
