using UnityEngine;

// Owns battle presentation and delegates every state transition back to the
// BattleManager gameplay facade.
public sealed class BattleHud : MonoBehaviour
{
    private BattleManager battle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle centerStyle;
    private GUIStyle smallStyle;
    private GUIStyle smallCenterStyle;
    private GUIStyle buttonStyle;
    private GUIStyle bodyTopStyle;
    private Texture2D whiteTexture;

    public void Configure(BattleManager manager) => battle = manager;

    private void OnGUI()
    {
        if (battle == null)
            return;
        EnsureStyles();
        float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.35f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (battle.State == BattleManager.BattleState.Fighting)
            DrawFightingHud(width, height, scale);
        else
            DrawStateScreen(width, height);
        GUI.matrix = previous;
    }

    private void DrawFightingHud(float width, float height, float scale)
    {
        DrawPanel(new Rect(22f, height - 105f, 330f, 78f), new Color(0.035f, 0.045f, 0.055f, 0.82f));
        GUI.Label(new Rect(35f, height - 100f, 300f, 24f), "THE BLUE CAPTAIN", labelStyle);
        DrawBar(new Rect(35f, height - 73f, 300f, 17f), battle.Player != null ? battle.Player.HealthNormalized : 0f, new Color(0.18f, 0.72f, 0.29f));
        DrawBar(new Rect(35f, height - 49f, 220f, 9f), battle.Player != null ? battle.Player.StaminaNormalized : 0f, new Color(0.9f, 0.68f, 0.2f));
        GUI.Label(new Rect(262f, height - 56f, 70f, 20f), "STAMINA", smallStyle);

        DrawPanel(new Rect(width - 282f, 20f, 260f, 48f), new Color(0.035f, 0.045f, 0.055f, 0.82f));
        GUI.Label(new Rect(width - 268f, 27f, 232f, 32f), $"BLUE  {battle.CountAlive(Team.Allies)}       RED  {battle.CountAlive(Team.Enemies)}", labelStyle);
        if (!battle.IsTraining)
        {
            DrawPanel(new Rect(22f, 20f, 275f, 66f), new Color(0.035f, 0.045f, 0.055f, 0.82f));
            GUI.Label(new Rect(35f, 25f, 245f, 24f), $"ORDER: {CommandLabel(battle.CurrentAllyCommand)}", labelStyle);
            GUI.Label(new Rect(35f, 51f, 245f, 20f), "1 FOLLOW    2 HOLD    3 CHARGE", smallStyle);
        }
        if (battle.Player != null)
        {
            if (battle.Player.IsRanged)
                DrawBowReticle(width * 0.5f, height * 0.5f);
            else
            {
                GUI.Label(new Rect(width * 0.5f - 10f, height * 0.5f - 14f, 20f, 20f), "+", centerStyle);
                DrawDirectionReticle(width * 0.5f, height * 0.5f);
            }
            if (battle.Player.IsCounterReady)
                GUI.Label(new Rect(width * 0.5f - 120f, height * 0.5f + 66f, 240f, 24f), "COUNTER READY - STRIKE NOW", smallCenterStyle);
        }

        if (battle.MessageTimer > 0f)
            GUI.Label(new Rect(width * 0.5f - 350f, 78f, 700f, 48f), battle.Message, titleStyle);
        DrawWorldHealthBars(scale);

        if (battle.ImpactFlash > 0f)
        {
            Color color = battle.ImpactFlashColor;
            GUI.color = new Color(color.r, color.g, color.b, battle.ImpactFlash);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), whiteTexture);
            GUI.color = Color.white;
        }
    }

    private void DrawBowReticle(float cx, float cy)
    {
        bool drawing = battle.Player.IsChargingAttack;
        float draw = drawing ? battle.Player.BowDrawNormalized : 0f;
        float precision = drawing ? battle.Player.BowPrecisionNormalized : 0f;
        float radius = drawing ? Mathf.Lerp(42f, 11f, Mathf.SmoothStep(0f, 1f, precision)) : 31f;
        Color color = !drawing ? new Color(0.78f, 0.8f, 0.82f, 0.7f)
            : !battle.Player.BowPrecisionReady ? new Color(1f, 0.48f, 0.12f)
            : Color.Lerp(new Color(1f, 0.78f, 0.18f), new Color(0.32f, 0.95f, 0.48f), precision);

        GUI.color = color;
        GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), whiteTexture);
        DrawReticleBracket(cx - radius, cy, true);
        DrawReticleBracket(cx + radius, cy, true);
        DrawReticleBracket(cx, cy - radius, false);
        DrawReticleBracket(cx, cy + radius, false);

        Rect track = new Rect(cx - 54f, cy + 58f, 108f, 5f);
        GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.85f);
        GUI.DrawTexture(track, whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(track.x + 1f, track.y + 1f, (track.width - 2f) * draw, track.height - 2f), whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(track.x + track.width * battle.Player.BowPrecisionThresholdNormalized,
            track.y - 2f, 2f, track.height + 4f), whiteTexture);
        if (drawing)
        {
            string state = precision >= 0.99f ? "STEADY" : battle.Player.BowPrecisionReady ? "TIGHTENING" : "DRAW";
            GUI.color = color;
            GUI.Label(new Rect(cx - 60f, cy + 63f, 120f, 18f), state, smallCenterStyle);
        }
        GUI.color = Color.white;
    }

    private void DrawReticleBracket(float x, float y, bool vertical)
    {
        float w = vertical ? 3f : 13f;
        float h = vertical ? 13f : 3f;
        GUI.DrawTexture(new Rect(x - w * 0.5f, y - h * 0.5f, w, h), whiteTexture);
    }

    private void DrawStateScreen(float width, float height)
    {
        DrawPanel(new Rect(width * 0.5f - 285f, height * 0.5f - 205f, 570f, 410f), new Color(0.025f, 0.03f, 0.035f, 0.92f));
        string title = battle.State == BattleManager.BattleState.Ready ? "CONQUER OTHERS"
            : battle.State == BattleManager.BattleState.Victory ? "VICTORY" : "DEFEAT";
        GUI.Label(new Rect(width * 0.5f - 240f, height * 0.5f - 184f, 480f, 50f), title, titleStyle);

        if (battle.State == BattleManager.BattleState.Ready)
        {
            bool bow = battle.Player != null && battle.Player.IsRanged;
            string controls = bow
                ? "Hold LMB  Draw bow and aim with camera\nHold until STEADY for best precision, release to fire\nBow users cannot block - keep your distance"
                : "Hold LMB + move mouse  Aim a swing, release to strike\nHold RMB + move mouse  Raise your guard that way\n\nMatch attack direction to block all damage.\nTime the block at the last moment, then counter.";
            string encounter = battle.IsTraining ? "TRAINING ARENA" : $"ASSAULT ON {battle.EncounterTitle}";
            string orders = battle.IsTraining ? "" : "\n1 Follow    2 Hold    3 Charge\n";
            string body = $"{encounter}\nEQUIPPED: {(battle.Player != null ? WeaponCatalog.Label(battle.Player.Weapon) : "")}\n\nWASD  Move       Shift  Sprint       Space  Dodge\n{controls}{orders}\nCLICK TO BEGIN";
            GUI.Label(new Rect(width * 0.5f - 235f, height * 0.5f - 126f, 470f, 320f), body, bodyTopStyle);
            return;
        }

        string result = $"Battle time  {Mathf.FloorToInt(battle.BattleTime / 60f):00}:{Mathf.FloorToInt(battle.BattleTime % 60f):00}\n\nYOUR DAMAGE  {battle.PlayerDamageDealt:0}        ALLIES  {battle.AlliesDamageDealt:0}\nYOUR KILLS  {battle.PlayerKills}        DAMAGE TAKEN  {battle.PlayerDamageTaken:0}\nPERFECT BLOCKS  {battle.PlayerPerfectBlocks}        COUNTERS  {battle.PlayerCounterHits}\nBLUE LOSSES  {battle.InitialAllies - battle.CountAlive(Team.Allies)} / {battle.InitialAllies}        RED LOSSES  {battle.InitialEnemies - battle.CountAlive(Team.Enemies)} / {battle.InitialEnemies}";
        GUI.Label(new Rect(width * 0.5f - 245f, height * 0.5f - 118f, 490f, 150f), result, bodyTopStyle);
        string buttonLabel = battle.IsTraining ? "RETURN TO THE MAP"
            : battle.State == BattleManager.BattleState.Victory ? "CLAIM THE TERRITORY" : "RETURN TO THE MAP";
        if (GUI.Button(new Rect(width * 0.5f - 140f, height * 0.5f + 70f, 280f, 40f), buttonLabel, buttonStyle))
            battle.ConfirmResult();
    }

    private void DrawWorldHealthBars(float scale)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        BattleFighter primaryThreat = battle.Player != null ? battle.FindIncomingThreat(battle.Player) : null;
        foreach (BattleFighter fighter in battle.Fighters)
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

    private void DrawDirectionReticle(float cx, float cy)
    {
        bool blocking = battle.Player.IsBlocking;
        bool charging = battle.Player.IsChargingAttack;
        float charge = battle.Player.AttackChargeNormalized;
        CombatDirection active = blocking ? battle.Player.BlockDirection
            : charging ? battle.Player.AttackDirection : battle.Player.SelectedDirection;
        Color activeColor = battle.Player.IsCounterReady ? new Color(1f, 0.82f, 0.18f)
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

    private static string CommandLabel(BattleManager.AllyCommand command) => command switch
    {
        BattleManager.AllyCommand.Follow => "FOLLOW",
        BattleManager.AllyCommand.Hold => "HOLD",
        _ => "CHARGE"
    };

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

}
