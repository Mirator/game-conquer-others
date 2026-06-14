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
    public string DebugAISummary => $"MaxPlayerAttackers={maxPlayerAttackers}, MaxTargetAttackers={maxTargetAttackers}, MinFighterDistance={minimumFighterDistance:0.00}, MaxClosePairs={maxClosePairs}";

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

    // Raised when the player dismisses a result screen. The GameDirector listens
    // and applies the outcome to the campaign.
    public System.Action<BattleResult> OnBattleConcluded;
    public string EncounterTitle = "THE OLD COURTYARD";
    public string DebugSummary => $"State={State}, Blue={CountAlive(Team.Allies)}, Red={CountAlive(Team.Enemies)}, Time={battleTime:0.0}";

    private readonly List<BattleFighter> fighters = new();
    private readonly Dictionary<BattleFighter, List<AIFighter>> attackPermissions = new();
    private BattleEffects effects;
    private ThirdPersonCamera cameraRig;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle centerStyle;
    private GUIStyle smallStyle;
    private GUIStyle smallCenterStyle;
    private GUIStyle buttonStyle;
    private GUIStyle bodyTopStyle;
    private Texture2D whiteTexture;
    private float battleTime;
    private float impactFlash;
    private float messageTimer;
    private string message;
    private bool concluded;
    private float playerDamageDealt;
    private float alliesDamageDealt;
    private float enemiesDamageDealt;
    private float playerDamageTaken;
    private int playerKills;
    private int initialAllies;
    private int initialEnemies;
    private int maxPlayerAttackers;
    private int maxTargetAttackers;
    private int maxClosePairs;
    private float minimumFighterDistance = float.MaxValue;

    public void Configure(BattleEffects battleEffects, ThirdPersonCamera rig)
    {
        effects = battleEffects;
        cameraRig = rig;
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
            UpdateTacticalTelemetry();
        }

        if (Keyboard.current == null)
            return;
        if (Keyboard.current.rKey.wasPressedThisFrame)
            GameDirector.Instance.RestartBattle();
        // Battle starts on Enter/click; the result screen is dismissed only via
        // its on-screen button (see OnGUI) so the statistics are not skipped.
        if (State == BattleState.Ready && (Keyboard.current.enterKey.wasPressedThisFrame
            || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
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
    {
        if (seeker.Team == Team.Enemies && Player != null && Player.IsAlive && CountAssignedTo(Player, seeker) == 0)
            return Player;
        BattleFighter best = current != null && current.IsAlive ? current : null;
        float bestScore = best != null ? ScoreTarget(seeker, best, true) : float.MaxValue;
        foreach (BattleFighter fighter in fighters)
        {
            if (!fighter.IsAlive || fighter.Team == seeker.Team)
                continue;
            float score = ScoreTarget(seeker, fighter, fighter == current);
            if (score < bestScore)
            {
                bestScore = score;
                best = fighter;
            }
        }
        return best;
    }

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
    {
        CleanupAttackPermissions();
        if (target == null || !target.IsAlive)
            return false;
        if (!attackPermissions.TryGetValue(target, out List<AIFighter> attackers))
        {
            attackers = new List<AIFighter>();
            attackPermissions[target] = attackers;
        }
        if (attackers.Contains(attacker))
            return true;
        int limit = target.IsPlayer ? 1 : 2;
        if (attackers.Count >= limit)
            return false;
        attackers.Add(attacker);
        return true;
    }

    public void ReleaseAttackPermission(AIFighter attacker)
    {
        foreach (List<AIFighter> attackers in attackPermissions.Values)
            attackers.Remove(attacker);
    }

    public Vector3 GetEngagementPosition(AIFighter seeker, BattleFighter target, bool activeAttacker, float preferredRange)
    {
        Vector3 radial = seeker.transform.position - target.transform.position;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.01f)
            radial = target.transform.forward;
        radial.Normalize();

        if (activeAttacker)
            return target.transform.position + radial * preferredRange;

        List<AIFighter> supporters = new();
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai != seeker && ai.CurrentTarget == target && !ai.HasAttackPermission)
                supporters.Add(ai);
        supporters.Add(seeker);
        supporters.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        int index = supporters.IndexOf(seeker);
        float[] angles = { -72f, 72f, -138f, 138f, 180f, -105f, 105f };
        float angle = angles[index % angles.Length] + index / angles.Length * 18f;
        Vector3 slotDirection = Quaternion.AngleAxis(angle, Vector3.up) * target.transform.forward;
        return target.transform.position + slotDirection.normalized * Mathf.Lerp(2.8f, 3.5f, index % 3 / 2f);
    }

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
    {
        Vector3 result = Vector3.zero;
        foreach (BattleFighter fighter in fighters)
        {
            if (fighter == seeker || !fighter.IsAlive)
                continue;
            Vector3 offset = seeker.transform.position - fighter.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 6.25f && offset.sqrMagnitude > 0.001f)
            {
                float distance = offset.magnitude;
                result += offset.normalized * Mathf.Clamp01((2.5f - distance) / 1.8f);
            }
        }
        return Vector3.ClampMagnitude(result, 1.4f);
    }

    public void PlayAttackSound(Vector3 position, bool player) => effects?.PlaySwing(position, player);

    public void PlayFootstep(Vector3 position, bool player) => effects?.PlayFootstep(position, player);

    public void ReportWhiff(BattleFighter attacker) => effects?.PlayWhiff(attacker.transform.position, attacker.IsPlayer);

    public void ReportImpact(BattleFighter target, BattleFighter attacker, bool blocked)
    {
        effects?.PlayImpact(target.transform.position, blocked);
        bool playerInvolved = target.IsPlayer || attacker != null && attacker.IsPlayer;
        if (playerInvolved)
            cameraRig?.AddShake(target.IsPlayer ? 0.11f : blocked ? 0.065f : 0.045f);
        if (!blocked && attacker != null && attacker.IsPlayer)
        {
            attacker.ApplyHitStop(0.055f);
            target.ApplyHitStop(0.055f);
        }
        if (target.IsPlayer)
            impactFlash = blocked ? 0.1f : 0.28f;
        if (blocked && target.IsPlayer)
        {
            message = "DIRECTIONAL BLOCK";
            messageTimer = 0.5f;
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
            AlliesSurvived = AlliedSoldiersAlive
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

    public bool DebugAuditDirectionalBlock()
    {
        if (Player == null)
            return false;
        BattleFighter attacker = FindNearestOpponent(Player);
        if (attacker == null)
            return false;

        float startingHealth = Player.CurrentHealth;
        Quaternion startingRotation = Player.transform.rotation;
        Vector3 towardAttacker = attacker.transform.position - Player.transform.position;
        towardAttacker.y = 0f;
        Player.transform.rotation = Quaternion.LookRotation(towardAttacker.normalized);

        bool allCorrectBlocksStoppedDamage = true;
        bool allWrongBlocksTookDamage = true;
        foreach (CombatDirection direction in System.Enum.GetValues(typeof(CombatDirection)))
        {
            Player.DebugSetBlock(true, direction);
            Player.ReceiveHit(25f, attacker, direction);
            allCorrectBlocksStoppedDamage &= Mathf.Approximately(Player.CurrentHealth, startingHealth);
            Player.DebugRestoreHealth(startingHealth);

            CombatDirection wrongDirection = (CombatDirection)(((int)direction + 1) % 4);
            Player.DebugSetBlock(true, wrongDirection);
            Player.ReceiveHit(25f, attacker, direction);
            allWrongBlocksTookDamage &= Mathf.Approximately(Player.CurrentHealth, startingHealth - 25f);
            Player.DebugRestoreHealth(startingHealth);
        }

        Player.transform.rotation = Quaternion.LookRotation(-towardAttacker.normalized);
        Player.DebugSetBlock(true, CombatDirection.Up);
        Player.ReceiveHit(25f, attacker, CombatDirection.Up);
        bool rearAttackBypassedBlock = Mathf.Approximately(Player.CurrentHealth, startingHealth - 25f);
        Player.DebugRestoreHealth(startingHealth);
        Player.DebugSetBlock(false, CombatDirection.Right);
        Player.transform.rotation = startingRotation;
        Debug.Log($"Directional combat audit: allCorrectBlocks={allCorrectBlocksStoppedDamage}, allWrongBlocks={allWrongBlocksTookDamage}, rearBypass={rearAttackBypassedBlock}");
        return allCorrectBlocksStoppedDamage && allWrongBlocksTookDamage && rearAttackBypassedBlock;
    }

    public bool DebugAuditResponsiveCombat()
    {
        bool jitterIgnored = !CombatGesture.TryResolve(new Vector2(4f, 3f), out _);
        bool diagonalIgnored = !CombatGesture.TryResolve(new Vector2(10f, 9f), out _);
        bool deliberateLeft = CombatGesture.TryResolve(new Vector2(-12f, 0f), out CombatDirection direction)
            && direction == CombatDirection.Left;

        Player.DebugSetBlock(false, CombatDirection.Right);
        bool prepared = Player.DebugPrepareAttack(CombatDirection.Up);
        Player.DebugSetBlock(true, CombatDirection.Left);
        bool heldAttackCancelledIntoBlock = prepared && Player.Phase == CombatPhase.Idle
            && Player.IsBlocking && Player.BlockDirection == CombatDirection.Left;
        Player.DebugSetBlock(false, CombatDirection.Right);
        Player.DebugRestoreStamina();

        BattleFighter enemy = FindNearestOpponent(Player);
        bool sweptTargetFound = false;
        if (enemy != null)
        {
            Vector3 center = enemy.transform.position + Vector3.up * 1.05f;
            sweptTargetFound = FindSweptStrikeTarget(Player, center + Vector3.left * 0.5f,
                center + Vector3.right * 0.5f, 0.72f) == enemy;
        }

        bool passed = jitterIgnored && diagonalIgnored && deliberateLeft
            && heldAttackCancelledIntoBlock && sweptTargetFound;
        Debug.Log($"Responsive combat audit: jitterIgnored={jitterIgnored}, diagonalIgnored={diagonalIgnored}, deliberateLeft={deliberateLeft}, cancelToBlock={heldAttackCancelledIntoBlock}, sweptTarget={sweptTargetFound}");
        return passed;
    }

    public bool DebugAuditAICoordination()
    {
        CleanupAttackPermissions();
        int playerAttackers = Player != null && attackPermissions.TryGetValue(Player, out List<AIFighter> attackers)
            ? attackers.Count : 0;
        int largestGroup = 0;
        foreach (List<AIFighter> group in attackPermissions.Values)
            largestGroup = Mathf.Max(largestGroup, group.Count);
        bool playerLimit = playerAttackers <= 1;
        bool generalLimit = largestGroup <= 2;
        bool stableTargets = true;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai.IsAlive && ai.CurrentTarget != null)
                stableTargets &= ai.CurrentTarget.Team != ai.Team && ai.CurrentTarget.IsAlive;
        Debug.Log($"AI coordination audit: playerAttackers={playerAttackers}, largestGroup={largestGroup}, playerLimit={playerLimit}, generalLimit={generalLimit}, stableTargets={stableTargets}, {DebugAISummary}");
        return playerLimit && generalLimit && stableTargets;
    }

    private int CountAlive(Team team)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive && fighter.Team == team)
                count++;
        return count;
    }

    private float ScoreTarget(AIFighter seeker, BattleFighter target, bool current)
    {
        float distance = Vector3.Distance(seeker.transform.position, target.transform.position);
        int assigned = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai != seeker && ai.IsAlive && ai.CurrentTarget == target)
                assigned++;
        float score = distance + assigned * 3.1f;
        if (target.IsPlayer)
            score -= 0.65f;
        if (current)
            score -= 2.4f;
        if (target.IsAttackThreatening)
            score += 0.35f;
        return score;
    }

    private int CountAssignedTo(BattleFighter target, AIFighter except)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter is AIFighter ai && ai != except && ai.IsAlive && ai.CurrentTarget == target)
                count++;
        return count;
    }

    private void CleanupAttackPermissions()
    {
        List<BattleFighter> empty = new();
        foreach (KeyValuePair<BattleFighter, List<AIFighter>> pair in attackPermissions)
        {
            pair.Value.RemoveAll(ai => ai == null || !ai.IsAlive || ai.CurrentTarget != pair.Key);
            if (pair.Key == null || !pair.Key.IsAlive || pair.Value.Count == 0)
                empty.Add(pair.Key);
        }
        foreach (BattleFighter target in empty)
            attackPermissions.Remove(target);
    }

    private void UpdateTacticalTelemetry()
    {
        CleanupAttackPermissions();
        int closePairs = 0;
        for (int i = 0; i < fighters.Count; i++)
        {
            if (!fighters[i].IsAlive)
                continue;
            for (int j = i + 1; j < fighters.Count; j++)
            {
                if (!fighters[j].IsAlive)
                    continue;
                float distance = Vector3.Distance(fighters[i].transform.position, fighters[j].transform.position);
                minimumFighterDistance = Mathf.Min(minimumFighterDistance, distance);
                if (distance < 1.05f)
                    closePairs++;
            }
        }
        maxClosePairs = Mathf.Max(maxClosePairs, closePairs);
        foreach (KeyValuePair<BattleFighter, List<AIFighter>> pair in attackPermissions)
        {
            maxTargetAttackers = Mathf.Max(maxTargetAttackers, pair.Value.Count);
            if (pair.Key != null && pair.Key.IsPlayer)
                maxPlayerAttackers = Mathf.Max(maxPlayerAttackers, pair.Value.Count);
        }
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

    private void OnGUI()
    {
        EnsureStyles();
        float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.35f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (State == BattleState.Fighting)
        {
            DrawPanel(new Rect(22f, height - 105f, 330f, 78f), new Color(0.035f, 0.045f, 0.055f, 0.82f));
            GUI.Label(new Rect(35f, height - 100f, 300f, 24f), "THE BLUE CAPTAIN", labelStyle);
            DrawBar(new Rect(35f, height - 73f, 300f, 17f), Player != null ? Player.HealthNormalized : 0f, new Color(0.18f, 0.72f, 0.29f));
            DrawBar(new Rect(35f, height - 49f, 220f, 9f), Player != null ? Player.StaminaNormalized : 0f, new Color(0.9f, 0.68f, 0.2f));
            GUI.Label(new Rect(262f, height - 56f, 70f, 20f), "STAMINA", smallStyle);

            DrawPanel(new Rect(width - 282f, 20f, 260f, 48f), new Color(0.035f, 0.045f, 0.055f, 0.82f));
            GUI.Label(new Rect(width - 268f, 27f, 232f, 32f), $"BLUE  {CountAlive(Team.Allies)}       RED  {CountAlive(Team.Enemies)}", labelStyle);
            GUI.Label(new Rect(width * 0.5f - 10f, height * 0.5f - 14f, 20f, 20f), "+", centerStyle);
            if (Player != null)
            {
                DrawDirectionReticle(width * 0.5f, height * 0.5f);
                string verb = Player.IsBlocking ? "BLOCK" : Player.IsChargingAttack ? "ATTACK" : "AIM";
                CombatDirection shown = Player.IsBlocking ? Player.BlockDirection
                    : Player.IsChargingAttack ? Player.AttackDirection : Player.SelectedDirection;
                GUI.Label(new Rect(width * 0.5f - 100f, height * 0.5f + 44f, 200f, 24f), $"{verb}  {DirectionLabel(shown)}", smallCenterStyle);
            }

            if (messageTimer > 0f)
                GUI.Label(new Rect(width * 0.5f - 220f, 82f, 440f, 36f), message, titleStyle);
            DrawWorldHealthBars(scale);

            if (impactFlash > 0f)
            {
                GUI.color = new Color(0.7f, 0.02f, 0.01f, impactFlash);
                GUI.DrawTexture(new Rect(0f, 0f, width, height), whiteTexture);
                GUI.color = Color.white;
            }
        }
        else
        {
            DrawPanel(new Rect(width * 0.5f - 285f, height * 0.5f - 205f, 570f, 410f), new Color(0.025f, 0.03f, 0.035f, 0.92f));
            string title = State == BattleState.Ready ? "CONQUER OTHERS" : State == BattleState.Victory ? "VICTORY" : "DEFEAT";
            GUI.Label(new Rect(width * 0.5f - 240f, height * 0.5f - 184f, 480f, 50f), title, titleStyle);

            if (State == BattleState.Ready)
            {
                string body = $"ASSAULT ON {EncounterTitle}\nLead the blue soldiers and break the red line.\n\nWASD  Move       Shift  Sprint       Space  Dodge\nHold LMB + move mouse  Aim a swing, release to strike\nHold RMB + move mouse  Raise your shield that way\n\nThe ticks around the crosshair show your direction.\nMatch the incoming attack direction to stop all damage.\n\nPRESS ENTER OR CLICK TO BEGIN";
                GUI.Label(new Rect(width * 0.5f - 235f, height * 0.5f - 126f, 470f, 320f), body, bodyTopStyle);
            }
            else
            {
                string body = $"Battle time  {Mathf.FloorToInt(battleTime / 60f):00}:{Mathf.FloorToInt(battleTime % 60f):00}\n\nYOUR DAMAGE  {playerDamageDealt:0}        ALLIES  {alliesDamageDealt:0}\nYOUR KILLS  {playerKills}        DAMAGE TAKEN  {playerDamageTaken:0}\nBLUE LOSSES  {initialAllies - CountAlive(Team.Allies)} / {initialAllies}        RED LOSSES  {initialEnemies - CountAlive(Team.Enemies)} / {initialEnemies}";
                GUI.Label(new Rect(width * 0.5f - 245f, height * 0.5f - 118f, 490f, 150f), body, bodyTopStyle);

                string buttonLabel = State == BattleState.Victory ? "CLAIM THE TERRITORY" : "RETURN TO THE MAP";
                if (GUI.Button(new Rect(width * 0.5f - 140f, height * 0.5f + 70f, 280f, 40f), buttonLabel, buttonStyle))
                    ConfirmResult();
                GUI.Label(new Rect(width * 0.5f - 140f, height * 0.5f + 116f, 280f, 20f), "or press R to retry the battle", smallCenterStyle);
            }
        }
        GUI.matrix = previous;
    }

    private void DrawWorldHealthBars(float scale)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        foreach (BattleFighter fighter in fighters)
        {
            if (!fighter.IsAlive || fighter.IsPlayer || fighter.HealthNormalized >= 0.995f)
                continue;
            Vector3 screen = camera.WorldToScreenPoint(fighter.transform.position + Vector3.up * 2.25f);
            if (screen.z <= 0f)
                continue;
            DrawBar(new Rect(screen.x / scale - 34f, (Screen.height - screen.y) / scale, 68f, 6f), fighter.HealthNormalized,
                fighter.Team == Team.Allies ? new Color(0.15f, 0.48f, 0.95f) : new Color(0.9f, 0.16f, 0.1f));
        }
    }

    private void DrawDirectionReticle(float cx, float cy)
    {
        if (Player == null)
            return;

        bool blocking = Player.IsBlocking;
        bool charging = Player.IsChargingAttack;
        float charge = Player.AttackChargeNormalized;
        CombatDirection active = blocking ? Player.BlockDirection
            : charging ? Player.AttackDirection : Player.SelectedDirection;

        Color activeColor = blocking ? new Color(0.32f, 0.78f, 1f)
            : charging ? Color.Lerp(new Color(1f, 0.86f, 0.42f), new Color(1f, 0.5f, 0.08f), charge)
            : new Color(0.86f, 0.86f, 0.9f);
        Color idleColor = new Color(0.5f, 0.52f, 0.55f, 0.55f);

        float gap = 22f + (charging ? charge * 7f : 0f);
        DrawReticleMarker(cx - gap, cy, true, active == CombatDirection.Left, activeColor, idleColor);
        DrawReticleMarker(cx + gap, cy, true, active == CombatDirection.Right, activeColor, idleColor);
        DrawReticleMarker(cx, cy - gap, false, active == CombatDirection.Up, activeColor, idleColor);
        DrawReticleMarker(cx, cy + gap, false, active == CombatDirection.Thrust, activeColor, idleColor);
        GUI.color = Color.white;
    }

    private void DrawReticleMarker(float x, float y, bool flank, bool active, Color activeColor, Color idleColor)
    {
        float lengthwise = active ? 22f : 13f;
        float thickness = active ? 6f : 4f;
        float w = flank ? thickness : lengthwise;
        float h = flank ? lengthwise : thickness;
        GUI.color = active ? activeColor : idleColor;
        GUI.DrawTexture(new Rect(x - w * 0.5f, y - h * 0.5f, w, h), whiteTexture);
    }

    private void DrawBar(Rect rect, float amount, Color fill)
    {
        GUI.color = new Color(0.025f, 0.03f, 0.035f, 0.95f);
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * amount, rect.height - 4f), whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawPanel(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = new Color(0.75f, 0.58f, 0.22f, 0.85f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), whiteTexture);
        GUI.color = Color.white;
    }

    private void EnsureStyles()
    {
        if (whiteTexture != null)
            return;
        whiteTexture = Texture2D.whiteTexture;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.94f, 0.78f, 0.33f) }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }
        };
        centerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 17, wordWrap = true, normal = { textColor = Color.white }
        };
        smallStyle = new GUIStyle(labelStyle)
        {
            fontSize = 10, normal = { textColor = new Color(0.78f, 0.8f, 0.82f) }
        };
        smallCenterStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        bodyTopStyle = new GUIStyle(centerStyle) { alignment = TextAnchor.UpperCenter };
    }

    private static string DirectionLabel(CombatDirection direction) => direction switch
    {
        CombatDirection.Up => "HIGH",
        CombatDirection.Thrust => "THRUST",
        _ => direction.ToString().ToUpperInvariant()
    };
}
