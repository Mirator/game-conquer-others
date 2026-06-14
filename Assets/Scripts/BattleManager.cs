using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BattleManager : MonoBehaviour
{
    public enum BattleState { Ready, Fighting, Victory, Defeat }

    public bool IsBattleRunning => State == BattleState.Fighting;
    public BattleState State { get; private set; } = BattleState.Ready;
    public PlayerFighter Player { get; private set; }
    public string DebugSummary => $"State={State}, Blue={CountAlive(Team.Allies)}, Red={CountAlive(Team.Enemies)}, Time={battleTime:0.0}";

    private readonly List<BattleFighter> fighters = new();
    private BattleEffects effects;
    private ThirdPersonCamera cameraRig;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle centerStyle;
    private GUIStyle smallStyle;
    private GUIStyle smallCenterStyle;
    private Texture2D whiteTexture;
    private float battleTime;
    private float impactFlash;
    private float messageTimer;
    private string message;

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
        }

        if (Keyboard.current == null)
            return;
        if (Keyboard.current.rKey.wasPressedThisFrame)
            BattleBootstrap.Instance.ResetBattle();
        if (State == BattleState.Ready && (Keyboard.current.enterKey.wasPressedThisFrame || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
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

    public BattleFighter FindBestTarget(BattleFighter attacker, float range, float coneAngle)
    {
        BattleFighter best = null;
        float bestDistance = float.MaxValue;
        foreach (BattleFighter fighter in fighters)
        {
            if (!fighter.IsAlive || fighter.Team == attacker.Team)
                continue;
            Vector3 offset = fighter.transform.position - attacker.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > range || Vector3.Angle(attacker.transform.forward, offset) > coneAngle * 0.5f)
                continue;
            if (distance < bestDistance)
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
            if (offset.sqrMagnitude < 3.2f && offset.sqrMagnitude > 0.001f)
                result += offset.normalized * (1.8f - offset.magnitude);
        }
        return result * 0.95f;
    }

    public void PlayAttackSound(Vector3 position, bool player) => effects?.PlaySwing(position, player);

    public void PlayFootstep(Vector3 position, bool player) => effects?.PlayFootstep(position, player);

    public void ReportImpact(BattleFighter target, bool blocked, float damage)
    {
        effects?.PlayImpact(target.transform.position, blocked);
        cameraRig?.AddShake(target.IsPlayer ? 0.13f : 0.06f);
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

    private int CountAlive(Team team)
    {
        int count = 0;
        foreach (BattleFighter fighter in fighters)
            if (fighter.IsAlive && fighter.Team == team)
                count++;
        return count;
    }

    public void BeginBattle()
    {
        State = BattleState.Fighting;
        battleTime = 0f;
        message = "BREAK THE RED LINE";
        messageTimer = 2.2f;
        LockCursor();
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
            GUI.Label(new Rect(width * 0.5f - 240f, height * 0.5f - 155f, 480f, 52f), title, titleStyle);
            string body = State == BattleState.Ready
                ? "THE OLD COURTYARD\nLead the blue soldiers and break the red line.\n\nWASD  Move       Shift  Sprint       Space  Dodge\nHold LMB + move mouse  Aim a swing, release to strike\nHold RMB + move mouse  Raise your shield that way\n\nThe ticks around the crosshair show your direction.\nMatch the incoming attack direction to stop all damage.\n\nPRESS ENTER OR CLICK TO BEGIN"
                : $"Battle time  {Mathf.FloorToInt(battleTime / 60f):00}:{Mathf.FloorToInt(battleTime % 60f):00}\nBlue remaining  {CountAlive(Team.Allies)}       Red remaining  {CountAlive(Team.Enemies)}\n\nPRESS R TO FIGHT AGAIN";
            GUI.Label(new Rect(width * 0.5f - 245f, height * 0.5f - 82f, 490f, 260f), body, centerStyle);
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
    }

    private static string DirectionLabel(CombatDirection direction) => direction switch
    {
        CombatDirection.Up => "HIGH",
        CombatDirection.Thrust => "THRUST",
        _ => direction.ToString().ToUpperInvariant()
    };
}
