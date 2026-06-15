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
            tactics.UpdateTelemetry();
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

    private int CountAlive(Team team)
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
                if (Player.IsCounterReady)
                    GUI.Label(new Rect(width * 0.5f - 120f, height * 0.5f + 66f, 240f, 24f), "COUNTER READY - STRIKE NOW", smallCenterStyle);
                DrawPrimaryThreatCue(width, height);
            }

            if (messageTimer > 0f)
                GUI.Label(new Rect(width * 0.5f - 350f, 78f, 700f, 48f), message, titleStyle);
            DrawWorldHealthBars(scale);

            if (impactFlash > 0f)
            {
                GUI.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, impactFlash);
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
                string body = $"ASSAULT ON {EncounterTitle}\nLead the blue soldiers and break the red line.\n\nWASD  Move       Shift  Sprint       Space  Dodge\nHold LMB + move mouse  Aim a swing, release to strike\nHold RMB + move mouse  Raise your shield that way\n\nMatch the incoming direction to block all damage.\nRaise the correct block at the last moment for a perfect block,\nthen strike during the counter window for bonus damage.\n\nCLICK TO BEGIN";
                GUI.Label(new Rect(width * 0.5f - 235f, height * 0.5f - 126f, 470f, 320f), body, bodyTopStyle);
            }
            else
            {
                string body = $"Battle time  {Mathf.FloorToInt(battleTime / 60f):00}:{Mathf.FloorToInt(battleTime % 60f):00}\n\nYOUR DAMAGE  {playerDamageDealt:0}        ALLIES  {alliesDamageDealt:0}\nYOUR KILLS  {playerKills}        DAMAGE TAKEN  {playerDamageTaken:0}\nPERFECT BLOCKS  {playerPerfectBlocks}        COUNTERS  {playerCounterHits}\nBLUE LOSSES  {initialAllies - CountAlive(Team.Allies)} / {initialAllies}        RED LOSSES  {initialEnemies - CountAlive(Team.Enemies)} / {initialEnemies}";
                GUI.Label(new Rect(width * 0.5f - 245f, height * 0.5f - 118f, 490f, 150f), body, bodyTopStyle);

                string buttonLabel = State == BattleState.Victory ? "CLAIM THE TERRITORY" : "RETURN TO THE MAP";
                if (GUI.Button(new Rect(width * 0.5f - 140f, height * 0.5f + 70f, 280f, 40f), buttonLabel, buttonStyle))
                    ConfirmResult();
            }
        }
        GUI.matrix = previous;
    }

    private void DrawWorldHealthBars(float scale)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        BattleFighter primaryThreat = Player != null ? FindIncomingThreat(Player) : null;
        foreach (BattleFighter fighter in fighters)
        {
            if (!fighter.IsAlive || fighter.IsPlayer || !fighter.ShouldShowHealthBar && fighter != primaryThreat)
                continue;
            Vector3 screen = camera.WorldToScreenPoint(fighter.transform.position + Vector3.up * 2.25f);
            if (screen.z <= 0f)
                continue;
            DrawBar(new Rect(screen.x / scale - 34f, (Screen.height - screen.y) / scale, 68f, 6f), fighter.HealthNormalized,
                fighter.Team == Team.Allies ? new Color(0.15f, 0.48f, 0.95f) : new Color(0.9f, 0.16f, 0.1f));
        }
    }

    private void DrawPrimaryThreatCue(float width, float height)
    {
        BattleFighter threat = FindIncomingThreat(Player);
        if (threat == null)
            return;

        float progress = threat.AttackTelegraphProgress;
        Color cue = threat.Phase == CombatPhase.AttackRelease
            ? new Color(1f, 0.18f, 0.08f) : Color.Lerp(new Color(1f, 0.82f, 0.18f), new Color(1f, 0.35f, 0.08f), progress);
        string direction = DirectionLabel(threat.AttackDirection);
        GUI.color = cue;
        GUI.Label(new Rect(width * 0.5f - 110f, height * 0.5f - 82f, 220f, 26f), $"INCOMING  {direction}", smallCenterStyle);
        GUI.color = Color.white;

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

        Color activeColor = Player.IsCounterReady ? new Color(1f, 0.82f, 0.18f)
            : blocking ? new Color(0.32f, 0.78f, 1f)
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
