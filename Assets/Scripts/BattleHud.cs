using UnityEngine;
using UnityEngine.UI;

// Responsive uGUI battle presentation. Gameplay remains owned by BattleManager.
public sealed class BattleHud : MonoBehaviour
{
    private BattleManager battle;
    private Canvas canvas;
    private RectTransform fighting;
    private RectTransform stateScreen;
    private Text healthLabel;
    private Image healthFill;
    private Image staminaFill;
    private Text score;
    private Text order;
    private Text message;
    private Text stateTitle;
    private Text stateBody;
    private Text reticle;
    private Button stateButton;
    private Text stateButtonLabel;
    private Image impactFlash;

    public void Configure(BattleManager manager)
    {
        battle = manager;
        Build();
    }

    private void Build()
    {
        canvas = MedievalUi.CreateCanvas(transform, "Battle HUD Canvas", 20);
        fighting = MedievalUi.Panel(canvas.transform, "Fighting HUD", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, Color.clear);
        fighting.GetComponent<Image>().raycastTarget = false;

        RectTransform captain = MedievalUi.Frame(fighting, "Captain Status", new Vector2(0.015f, 0.025f),
            new Vector2(0.29f, 0.145f), Vector2.zero, Vector2.zero);
        healthLabel = MedievalUi.Label(captain, "Captain", "THE BLUE CAPTAIN", 24, TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero);
        healthFill = BuildBar(captain, "Health", new Vector2(0.04f, 0.39f), new Vector2(0.96f, 0.62f),
            new Color(0.18f, 0.72f, 0.29f));
        staminaFill = BuildBar(captain, "Stamina", new Vector2(0.04f, 0.14f), new Vector2(0.7f, 0.28f),
            new Color(0.9f, 0.68f, 0.2f));
        MedievalUi.Label(captain, "Stamina Label", "STAMINA", 15, TextAnchor.MiddleLeft,
            new Vector2(0.73f, 0.08f), new Vector2(0.96f, 0.32f), Vector2.zero, Vector2.zero);

        RectTransform scorePanel = MedievalUi.Frame(fighting, "Score", new Vector2(0.77f, 0.925f),
            new Vector2(0.985f, 0.985f), Vector2.zero, Vector2.zero);
        score = MedievalUi.Label(scorePanel, "Score Label", "", 25, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform orderPanel = MedievalUi.Frame(fighting, "Order", new Vector2(0.015f, 0.89f),
            new Vector2(0.27f, 0.985f), Vector2.zero, Vector2.zero);
        order = MedievalUi.Label(orderPanel, "Order Label", "", 20, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

        message = MedievalUi.Label(fighting, "Battle Message", "", 42, TextAnchor.MiddleCenter,
            new Vector2(0.24f, 0.82f), new Vector2(0.76f, 0.92f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        reticle = MedievalUi.Label(fighting, "Reticle", "+", 36, TextAnchor.MiddleCenter,
            new Vector2(0.485f, 0.47f), new Vector2(0.515f, 0.53f), Vector2.zero, Vector2.zero);
        impactFlash = MedievalUi.Panel(fighting, "Impact Flash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            Color.clear).GetComponent<Image>();
        impactFlash.raycastTarget = false;

        stateScreen = MedievalUi.Panel(canvas.transform, "Battle State", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.018f, 0.014f, 0.012f, 0.93f));
        RectTransform card = MedievalUi.Frame(stateScreen, "State Card", new Vector2(0.25f, 0.18f),
            new Vector2(0.75f, 0.82f), Vector2.zero, Vector2.zero);
        stateTitle = MedievalUi.Label(card, "Title", "", 58, TextAnchor.MiddleCenter,
            new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        stateBody = MedievalUi.Label(card, "Body", "", 26, TextAnchor.UpperCenter,
            new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
        stateButton = MedievalUi.Button(card, "Confirm", "CONTINUE", new Vector2(0.28f, 0.07f),
            new Vector2(0.72f, 0.19f), Vector2.zero, Vector2.zero, () => battle.ConfirmResult());
        stateButtonLabel = stateButton.GetComponentInChildren<Text>();
    }

    private static Image BuildBar(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        MedievalUi.Panel(parent, name + " Back", min, max, Vector2.zero, Vector2.zero, new Color(0.01f, 0.01f, 0.01f, 0.9f));
        Image fill = MedievalUi.Panel(parent, name + " Fill", min, max, new Vector2(3f, 3f),
            new Vector2(-3f, -3f), color).GetComponent<Image>();
        // A Filled image needs a sprite to honour fillAmount; without one the mesh stays
        // full width and the bar never visibly depletes.
        fill.sprite = MedievalUi.WhiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;
        return fill;
    }

    private void Update()
    {
        if (battle == null || canvas == null)
            return;
        bool isFighting = battle.State == BattleManager.BattleState.Fighting;
        fighting.gameObject.SetActive(isFighting);
        stateScreen.gameObject.SetActive(!isFighting);
        if (isFighting)
            RefreshFight();
        else
            RefreshState();
    }

    private void RefreshFight()
    {
        healthFill.fillAmount = battle.Player != null ? battle.Player.HealthNormalized : 0f;
        staminaFill.fillAmount = battle.Player != null ? battle.Player.StaminaNormalized : 0f;
        healthLabel.text = battle.Player != null ? $"THE BLUE CAPTAIN   {battle.Player.CurrentHealth:0}" : "THE BLUE CAPTAIN";
        score.text = $"BLUE  {battle.CountAlive(Team.Allies)}      RED  {battle.CountAlive(Team.Enemies)}";
        order.transform.parent.gameObject.SetActive(!battle.IsTraining);
        order.text = $"ORDER: {CommandLabel(battle.CurrentAllyCommand)}\n1 FOLLOW    2 HOLD    3 CHARGE";
        message.gameObject.SetActive(battle.MessageTimer > 0f);
        message.text = battle.Message;
        reticle.text = battle.Player != null && battle.Player.IsRanged
            ? battle.Player.IsChargingAttack ? (battle.Player.BowPrecisionReady ? "STEADY" : "DRAW") : "+"
            : battle.Player != null && battle.Player.IsCounterReady ? "COUNTER" : "+";
        Color flash = battle.ImpactFlashColor;
        impactFlash.color = new Color(flash.r, flash.g, flash.b, battle.ImpactFlash);
    }

    private void RefreshState()
    {
        stateButton.gameObject.SetActive(battle.State != BattleManager.BattleState.Ready);
        if (battle.State == BattleManager.BattleState.Ready)
        {
            stateTitle.text = battle.IsTraining ? "TRAINING ARENA" : $"ASSAULT ON\n{battle.EncounterTitle}";
            string weapon = battle.Player != null ? WeaponCatalog.Label(battle.Player.Weapon) : "";
            stateBody.text = $"EQUIPPED: {weapon}\n\nWASD  MOVE      SHIFT  SPRINT      SPACE  DODGE\n\n" +
                (battle.Player != null && battle.Player.IsRanged
                    ? "HOLD LMB TO DRAW AND AIM. RELEASE TO FIRE."
                    : "HOLD LMB AND MOVE MOUSE TO AIM A SWING.\nHOLD RMB AND MOVE MOUSE TO GUARD.") +
                "\n\nCLICK TO BEGIN";
            return;
        }

        stateTitle.text = battle.State == BattleManager.BattleState.Victory ? "VICTORY" : "DEFEAT";
        stateBody.text = $"BATTLE TIME  {Mathf.FloorToInt(battle.BattleTime / 60f):00}:{Mathf.FloorToInt(battle.BattleTime % 60f):00}\n\n" +
            $"YOUR DAMAGE  {battle.PlayerDamageDealt:0}      ALLIES  {battle.AlliesDamageDealt:0}\n" +
            $"YOUR KILLS  {battle.PlayerKills}      DAMAGE TAKEN  {battle.PlayerDamageTaken:0}\n" +
            $"PERFECT BLOCKS  {battle.PlayerPerfectBlocks}      COUNTERS  {battle.PlayerCounterHits}\n\n" +
            $"BLUE LOSSES  {battle.InitialAllies - battle.CountAlive(Team.Allies)} / {battle.InitialAllies}      " +
            $"RED LOSSES  {battle.InitialEnemies - battle.CountAlive(Team.Enemies)} / {battle.InitialEnemies}";
        stateButtonLabel.text = battle.IsTraining ? "RETURN TO MAP"
            : battle.State == BattleManager.BattleState.Victory ? "CLAIM TERRITORY" : "RETURN TO MAP";
    }

    private static string CommandLabel(BattleManager.AllyCommand command) => command switch
    {
        BattleManager.AllyCommand.Follow => "FOLLOW",
        BattleManager.AllyCommand.Hold => "HOLD",
        _ => "CHARGE"
    };
}
