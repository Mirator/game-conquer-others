using UnityEngine;
using UnityEngine.UI;

// Responsive uGUI battle presentation. Gameplay remains owned by BattleManager.
public sealed class BattleHud : MonoBehaviour
{
    private BattleManager battle;
    private Canvas canvas;
    private RectTransform fighting;
    private RectTransform stateScreen;
    private RectTransform victoryPrompt;
    private Text healthLabel;
    private Image healthFill;
    private Image healthChip;
    private Image staminaFill;
    private float shownHealth = 1f;
    private float shownChip = 1f;
    private float shownStamina = 1f;
    private string lastMessage = "";
    private float messageAge;
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
            new Color(0.18f, 0.72f, 0.29f), out healthChip, new Color(0.78f, 0.16f, 0.1f));
        staminaFill = BuildBar(captain, "Stamina", new Vector2(0.04f, 0.14f), new Vector2(0.7f, 0.28f),
            new Color(0.9f, 0.68f, 0.2f));
        MedievalUi.Label(captain, "Stamina Label", "STAMINA", 15, TextAnchor.MiddleLeft,
            new Vector2(0.73f, 0.08f), new Vector2(0.96f, 0.32f), Vector2.zero, Vector2.zero);

        RectTransform scorePanel = MedievalUi.Frame(fighting, "Score", new Vector2(0.77f, 0.925f),
            new Vector2(0.985f, 0.985f), Vector2.zero, Vector2.zero);
        score = MedievalUi.Label(scorePanel, "Score Label", "", 25, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform orderPanel = MedievalUi.Frame(fighting, "Order", new Vector2(0.015f, 0.83f),
            new Vector2(0.37f, 0.985f), Vector2.zero, Vector2.zero);
        order = MedievalUi.Label(orderPanel, "Order Label", "", 16, TextAnchor.MiddleCenter,
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
        MedievalUi.Divider(card, "Title Divider", new Vector2(0.2f, 0.74f), new Vector2(0.8f, 0.765f),
            Vector2.zero, Vector2.zero);
        stateBody = MedievalUi.Label(card, "Body", "", 26, TextAnchor.UpperCenter,
            new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
        stateButton = MedievalUi.Button(card, "Confirm", "CONTINUE", new Vector2(0.28f, 0.07f),
            new Vector2(0.72f, 0.19f), Vector2.zero, Vector2.zero, () => battle.ConfirmResult());
        stateButtonLabel = stateButton.GetComponentInChildren<Text>();

        // Shown in the bottom-left over the live battlefield the moment a battle is
        // won, holding the result screen back until the player presses E.
        victoryPrompt = MedievalUi.Frame(canvas.transform, "Victory Prompt",
            new Vector2(0.015f, 0.04f), new Vector2(0.34f, 0.12f), Vector2.zero, Vector2.zero);
        MedievalUi.Label(victoryPrompt, "Victory Prompt Label", "BATTLE WON!   PRESS E TO CONTINUE",
            22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(8f, 4f),
            new Vector2(-8f, -4f), MedievalUi.Gold);
        victoryPrompt.gameObject.SetActive(false);
    }

    private static Image BuildBar(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        MedievalUi.Well(parent, name + " Back", min, max, Vector2.zero, Vector2.zero, new Color(0.01f, 0.01f, 0.01f, 0.9f));
        return MakeFill(parent, name + " Fill", min, max, color);
    }

    // A bar with a "chip" layer behind the fill: when the value drops, the fill
    // catches up quickly while the chip lags, leaving a coloured sliver that
    // shows how much was just lost. The chip is created before the fill so it
    // draws behind it.
    private static Image BuildBar(Transform parent, string name, Vector2 min, Vector2 max, Color color,
        out Image chip, Color chipColor)
    {
        MedievalUi.Well(parent, name + " Back", min, max, Vector2.zero, Vector2.zero, new Color(0.01f, 0.01f, 0.01f, 0.9f));
        chip = MakeFill(parent, name + " Chip", min, max, chipColor);
        return MakeFill(parent, name + " Fill", min, max, color);
    }

    private static Image MakeFill(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        Image fill = MedievalUi.Panel(parent, name, min, max, new Vector2(3f, 3f),
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
        // While a win awaits acknowledgement, hide both the combat HUD and the
        // result overlay so only the corner prompt sits over the won battlefield.
        bool awaitingAck = battle.AwaitingVictoryAck;
        fighting.gameObject.SetActive(isFighting);
        victoryPrompt.gameObject.SetActive(awaitingAck);
        stateScreen.gameObject.SetActive(!isFighting && !awaitingAck);
        if (isFighting)
            RefreshFight();
        else if (!awaitingAck)
            RefreshState();
    }

    private void RefreshFight()
    {
        float targetHealth = battle.Player != null ? battle.Player.HealthNormalized : 0f;
        float targetStamina = battle.Player != null ? battle.Player.StaminaNormalized : 0f;
        bool reduceMotion = SettingsService.Current is { reduceMotion: true };
        if (reduceMotion)
        {
            shownHealth = targetHealth;
            shownStamina = targetStamina;
            shownChip = targetHealth;
        }
        else
        {
            float dt = Time.deltaTime;
            shownHealth = Mathf.MoveTowards(shownHealth, targetHealth, dt * 1.8f);
            shownStamina = Mathf.MoveTowards(shownStamina, targetStamina, dt * 2.6f);
            // The chip rises instantly with healing, then drains slowly to expose the loss.
            shownChip = Mathf.Max(shownChip, shownHealth);
            shownChip = Mathf.MoveTowards(shownChip, shownHealth, dt * 0.5f);
        }
        healthFill.fillAmount = shownHealth;
        healthChip.fillAmount = shownChip;
        staminaFill.fillAmount = shownStamina;
        healthLabel.text = battle.Player != null ? $"THE BLUE CAPTAIN   {battle.Player.CurrentHealth:0}" : "THE BLUE CAPTAIN";
        score.text = $"BLUE  {battle.CountAlive(Team.Allies)}      RED  {battle.CountAlive(Team.Enemies)}";
        order.transform.parent.gameObject.SetActive(!battle.IsTraining);
        string holdFire = battle.AllyHoldFire ? "    [HOLD FIRE]" : "";
        order.text =
            $"ORDER: {CommandLabel(battle.CurrentAllyCommand)}    FORMATION: {BattleManager.FormationName(battle.CurrentFormation)}{holdFire}\n" +
            "1 FOLLOW   2 HOLD   3 CHARGE   4 ADVANCE\n" +
            "F FORMATION    H HOLD FIRE";
        AnimateMessage();
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
            stateTitle.text = battle.EncounterKind switch
            {
                BattleKind.Training => "TRAINING ARENA",
                BattleKind.BanditField => $"ROUT THE\n{battle.EncounterTitle}",
                _ => $"ASSAULT ON\n{battle.EncounterTitle}"
            };
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
        // Only a won settlement assault claims a hold; a bandit rout (or any defeat)
        // simply returns to the map, so the button must not promise territory.
        bool claimsTerritory = battle.State == BattleManager.BattleState.Victory
            && !battle.IsTraining && battle.EncounterKind == BattleKind.SettlementAssault;
        stateButtonLabel.text = claimsTerritory ? "CLAIM TERRITORY" : "RETURN TO MAP";
    }

    // The centre cue fades in with a brief scale punch and fades out at the end
    // of its timer, instead of snapping on and off. Freshness is tracked by
    // watching for the message text to change.
    private void AnimateMessage()
    {
        bool show = battle.MessageTimer > 0f;
        message.gameObject.SetActive(show);
        if (!show)
        {
            messageAge = 0f;
            return;
        }
        if (battle.Message != lastMessage)
        {
            lastMessage = battle.Message;
            messageAge = 0f;
            message.text = battle.Message;
        }
        if (SettingsService.Current is { reduceMotion: true })
        {
            message.rectTransform.localScale = Vector3.one;
            message.color = new Color(MedievalUi.Gold.r, MedievalUi.Gold.g, MedievalUi.Gold.b, 1f);
            return;
        }
        messageAge += Time.deltaTime;
        float fadeIn = Mathf.Clamp01(messageAge / 0.12f);
        float fadeOut = Mathf.Clamp01(battle.MessageTimer / 0.18f);
        float alpha = Mathf.Min(fadeIn, fadeOut);
        float punch = 1f + 0.18f * (1f - Mathf.Clamp01(messageAge / 0.14f));
        message.rectTransform.localScale = Vector3.one * punch;
        message.color = new Color(MedievalUi.Gold.r, MedievalUi.Gold.g, MedievalUi.Gold.b, alpha);
    }

    private static string CommandLabel(BattleManager.AllyCommand command) => command switch
    {
        BattleManager.AllyCommand.Follow => "FOLLOW",
        BattleManager.AllyCommand.Hold => "HOLD",
        BattleManager.AllyCommand.Advance => "ADVANCE",
        _ => "CHARGE"
    };
}
