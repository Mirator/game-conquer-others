using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BattleManager : MonoBehaviour
{
    public enum BattleState { Ready, Fighting, Victory, Defeat }
    public enum AllyCommand { Follow, Hold, Charge }

    public bool IsBattleRunning => State == BattleState.Fighting;
    public BattleState State { get; private set; } = BattleState.Ready;
    public AllyCommand CurrentAllyCommand { get; private set; } = AllyCommand.Follow;
    public PlayerFighter Player { get; private set; }
    public int AlliesAlive => CountAlive(Team.Allies);
    public int EnemiesAlive => CountAlive(Team.Enemies);
    public string DebugAISummary => tactics.DebugSummary;
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
    public string DebugSummary => $"State={State}, Blue={CountAlive(Team.Allies)}, Red={CountAlive(Team.Enemies)}, Time={battleTime:0.0}";

    private readonly List<BattleFighter> fighters = new();
    private readonly Dictionary<AIFighter, Vector3> holdPositions = new();
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

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Follow);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Hold);
                else if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    SetAllyCommand(AllyCommand.Charge);
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
            GameDirector.Instance?.TogglePause();
        }
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

    public bool TryGetCommandPosition(AIFighter ally, BattleFighter target, out Vector3 position)
    {
        position = ally.transform.position;
        if (ally.Team != Team.Allies || CurrentAllyCommand == AllyCommand.Charge || Player == null || !Player.IsAlive)
            return false;

        float threatDistance = target != null && target.IsAlive ? ally.DistanceTo(target) : float.MaxValue;
        float defenseRadius = CurrentAllyCommand == AllyCommand.Hold ? 4.5f : 5.5f;
        if (threatDistance <= defenseRadius)
            return false;

        if (CurrentAllyCommand == AllyCommand.Hold && holdPositions.TryGetValue(ally, out Vector3 held))
            position = held;
        else
            position = GetFollowFormationPosition(ally);
        return true;
    }

    public void SetAllyCommand(AllyCommand command)
    {
        if (IsTraining)
            return;

        CurrentAllyCommand = command;
        holdPositions.Clear();
        if (command == AllyCommand.Hold)
        {
            foreach (BattleFighter fighter in fighters)
                if (fighter is AIFighter ally && ally.IsAlive && ally.Team == Team.Allies)
                    holdPositions[ally] = ally.transform.position;
        }

        message = command switch
        {
            AllyCommand.Follow => "ALLIES: FORM ON ME",
            AllyCommand.Hold => "ALLIES: HOLD THIS GROUND",
            _ => "ALLIES: CHARGE"
        };
        messageTimer = 1.2f;
    }

    private Vector3 GetFollowFormationPosition(AIFighter ally)
    {
        int index = 0;
        foreach (BattleFighter fighter in fighters)
        {
            if (fighter is not AIFighter candidate || !candidate.IsAlive || candidate.Team != Team.Allies)
                continue;
            if (candidate == ally)
                break;
            index++;
        }

        int row = index / 4;
        int column = index % 4;
        float side = (column - 1.5f) * 1.65f;
        float depth = 1.5f - row * 1.7f;
        return Player.transform.position + Player.transform.right * side + Player.transform.forward * depth;
    }

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
        effects?.PlayKill(victim.transform.position);
        if (attacker != null && attacker.IsPlayer)
            attacker.ApplyHitStop(0.13f);
        if (victim.IsPlayer || (attacker != null && attacker.IsPlayer))
            cameraRig?.AddShake(victim.IsPlayer ? 0.22f : 0.16f);
    }

    public BattleFighter FindProjectileTarget(BattleFighter attacker, Vector3 start, Vector3 end, float radius)
        => FindSweptStrikeTarget(attacker, start, end, radius);

    public void ReportProjectileHit() => projectileHits++;

    public void ReportArrowImpact(Vector3 position, bool fighterHit)
    {
        arrowImpacts++;
        effects?.PlayArrowImpact(position, fighterHit);
    }

    public void ReportImpact(BattleFighter target, BattleFighter attacker, bool blocked, bool perfectBlock, bool counterStrike, float appliedDamage = 0f)
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
        tactics.OnFighterRemoved(dead);
        if (dead is AIFighter deadAi)
            holdPositions.Remove(deadAi);
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
        tactics.OnFighterRemoved(fighter);
        holdPositions.Remove(fighter);
        Team team = fighter.Team;
        fighter.WithdrawFromBattle();
        retreats++;
        message = team == Team.Allies ? "AN ALLY HAS RETREATED" : "THE RED LINE IS BREAKING";
        messageTimer = 1.2f;
        if (team == Team.Enemies && CountAlive(Team.Enemies) == 0)
        {
            State = BattleState.Victory;
            effects?.PlayVictory();
            UnlockCursor();
        }
    }

    public void NotifyMoraleBreak(Team team)
    {
        message = team == Team.Allies ? "ALLIED MORALE IS BREAKING" : "THE RED LINE IS BREAKING";
        messageTimer = 1.5f;
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
    internal int AlliesNearCommandPositions(float tolerance)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
        {
            if (fighter is not AIFighter ally || !ally.IsAlive || ally.Team != Team.Allies)
                continue;
            Vector3 destination = CurrentAllyCommand == AllyCommand.Hold && holdPositions.TryGetValue(ally, out Vector3 held)
                ? held : GetFollowFormationPosition(ally);
            if (Vector3.Distance(ally.transform.position, destination) <= tolerance)
                count++;
        }
        return count;
    }
}
