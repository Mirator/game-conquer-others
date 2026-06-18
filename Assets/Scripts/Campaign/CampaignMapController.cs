using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds and runs the campaign overworld: an overhead map where the player
// marches a warband party between settlements and hunts roaming bandit parties.
// Click to move; time advances while travelling; collisions trigger battles.
public sealed class CampaignMapController : MonoBehaviour
{
    private const float TravelSpeed = 6f;        // map units per second while marching
    private const float DistancePerDay = 3f;     // map units that elapse one campaign day
    private const float EnemySightRange = 7f;    // bandits chase the player within this range
    private const float EnemyChaseRatio = 0.55f; // bandit speed as a fraction of the player's
    private const float EncounterRadius = 1.1f;  // party collision distance

    private GameDirector director;
    private CampaignState campaign;
    private Camera cam;
    private GameObject trainingNode;
    private Renderer trainingRenderer;
    private GameObject partyMarker;
    private Canvas campaignCanvas;
    private Text campaignSummary;
    private Text reportText;
    private Text selectionTitle;
    private Text selectionBody;
    private Text equipmentText;
    private Text endTitle;
    private RectTransform endScreen;
    private Button actionButton;
    private Text actionButtonText;
    private readonly Dictionary<Archetype, RecruitWidget> recruitButtons = new();
    private Button tierButton;
    private Text tierButtonText;
    private UnitType selectedTier = UnitType.Militia;

    private static readonly Archetype[] RecruitableArchetypes =
        { Archetype.Soldier, Archetype.Shieldbearer, Archetype.Berserker, Archetype.Archer };

    private readonly List<NodeView> nodes = new();
    private readonly List<PartyView> partyViews = new();
    private MaterialPropertyBlock nodeColorProperties;

    // Travel state: while travelling the party glides toward travelTarget and the
    // world advances; a pending territory/party resolves on arrival.
    private bool travelling;
    private Vector2 travelTarget;
    private Territory pendingTerritory;
    private EnemyParty pendingParty;
    private float dayAccumulator;

    // Cached signature of the last rendered HUD state so Update can skip the
    // string rebuild and Text writes on frames where nothing relevant changed.
    private bool uiInitialized;
    private int uiGold = -1;
    private int uiRosterTotal = -1;
    private int uiDay = -1;
    private bool uiTravelling;
    private UnitType uiSelectedTier;
    private WeaponType uiWeapon;
    private bool uiEnded;

    private struct PartyView
    {
        public EnemyParty Party;
        public Transform Marker;
    }

    private struct NodeView
    {
        public GameObject Go;
        public Territory Territory;
        public Renderer Renderer;
    }

    private struct RecruitWidget
    {
        public Button Button;
        public Text Label;
    }

    public void Configure(GameDirector gameDirector, CampaignState state)
    {
        director = gameDirector;
        campaign = state;
        nodeColorProperties = new MaterialPropertyBlock();
        BuildVisuals();
        BuildUi();
    }

    private void BuildVisuals()
    {
        GameObject sunObject = new GameObject("Map Sun");
        sunObject.transform.SetParent(transform);
        sunObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.95f, 0.85f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.37f, 0.4f);
        RenderSettings.fog = false;

        GameObject camObject = new GameObject("Map Camera");
        camObject.transform.SetParent(transform);
        camObject.tag = "MainCamera";
        camObject.transform.position = new Vector3(0f, 30f, -14f);
        camObject.transform.rotation = Quaternion.Euler(64f, 0f, 0f);
        cam = camObject.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
        camObject.AddComponent<AudioListener>();

        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Map Table";
        table.transform.SetParent(transform);
        table.transform.position = new Vector3(0f, -0.5f, 2f);
        table.transform.localScale = new Vector3(46f, 1f, 38f);
        table.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.18f, 0.2f, 0.16f));

        foreach (Territory t in campaign.Territories)
            foreach (int adj in t.AdjacentIds)
                if (adj > t.Id)
                    BuildEdge(t, campaign.GetById(adj));

        foreach (Territory t in campaign.Territories)
            BuildNode(t);
        BuildTrainingNode();
        BuildPartyMarkers();
    }

    private static Vector3 WorldOf(Territory t) => WorldOf(t.MapPosition);
    private static Vector3 WorldOf(Vector2 mapPosition) => new Vector3(mapPosition.x * 1.4f, 0.2f, mapPosition.y * 1.4f);

    private void BuildPartyMarkers()
    {
        partyMarker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        partyMarker.name = "Player Party";
        Destroy(partyMarker.GetComponent<Collider>());
        partyMarker.transform.SetParent(transform);
        partyMarker.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        partyMarker.transform.position = WorldOf(campaign.PartyPosition) + Vector3.up * 0.9f;
        partyMarker.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.2f, 0.55f, 1f), true);

        foreach (EnemyParty party in campaign.Parties)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = $"Party {party.Name}";
            marker.transform.SetParent(transform);
            marker.transform.localScale = new Vector3(0.62f, 0.8f, 0.62f);
            marker.transform.position = WorldOf(party.Position) + Vector3.up * 0.8f;
            marker.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.85f, 0.2f, 0.12f), true);
            partyViews.Add(new PartyView { Party = party, Marker = marker.transform });
        }
    }

    private void BuildNode(Territory t)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Node {t.Name}";
        go.transform.SetParent(transform);
        go.transform.position = WorldOf(t) + Vector3.up * 0.2f;
        go.transform.localScale = new Vector3(1.6f, 0.3f, 1.6f);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = RuntimeAssets.Material(Color.white);

        GameObject keep = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keep.name = "Keep";
        Destroy(keep.GetComponent<Collider>());
        keep.transform.SetParent(go.transform, false);
        keep.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        keep.transform.localScale = new Vector3(0.5f, 2.2f, 0.5f);
        keep.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(ColorFor(t));

        PrimitiveType markerType = t.Arena == ArenaType.Forest ? PrimitiveType.Sphere
            : t.Arena == ArenaType.Marsh ? PrimitiveType.Cylinder : PrimitiveType.Cube;
        GameObject marker = GameObject.CreatePrimitive(markerType);
        marker.name = $"{t.Arena} Marker";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(go.transform, false);
        marker.transform.localPosition = new Vector3(0.7f, 0.75f, 0.3f);
        marker.transform.localScale = t.Arena == ArenaType.Forest ? new Vector3(0.45f, 0.7f, 0.45f)
            : t.Arena == ArenaType.Marsh ? new Vector3(0.55f, 0.08f, 0.55f)
            : t.Arena == ArenaType.Highlands ? new Vector3(0.38f, 0.85f, 0.38f) : new Vector3(0.45f, 0.45f, 0.45f);
        marker.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(ArenaColor(t.Arena));
        GameObject landmarkPrefab = t.Arena switch
        {
            ArenaType.Forest => PresentationCatalog.Load()?.commonTree,
            ArenaType.Marsh => PresentationCatalog.Load()?.deadTree,
            ArenaType.Highlands => PresentationCatalog.Load()?.rock,
            _ => PresentationCatalog.Load()?.villageArch
        };
        if (landmarkPrefab != null)
        {
            GameObject landmark = Instantiate(landmarkPrefab, go.transform);
            landmark.name = $"{t.Arena} Authored Landmark";
            landmark.transform.localPosition = new Vector3(-0.35f, 0.45f, 0.25f);
            landmark.transform.localScale = Vector3.one * 0.45f;
            foreach (Collider collider in landmark.GetComponentsInChildren<Collider>())
                Destroy(collider);
            MaterialPropertyBlock landmarkProperties = new();
            Color landmarkColor = t.Arena switch
            {
                ArenaType.Forest => new Color(0.16f, 0.48f, 0.16f),
                ArenaType.Marsh => new Color(0.36f, 0.23f, 0.12f),
                ArenaType.Highlands => new Color(0.48f, 0.48f, 0.46f),
                _ => new Color(0.58f, 0.42f, 0.25f)
            };
            foreach (Renderer renderer in landmark.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(landmarkProperties);
                landmarkProperties.SetColor("_BaseColor", landmarkColor);
                landmarkProperties.SetColor("_Color", landmarkColor);
                renderer.SetPropertyBlock(landmarkProperties);
            }
        }
        PresentationCatalog presentation = PresentationCatalog.Load();
        GameObject bannerPrefab = presentation != null ? presentation.banner : null;
        if (bannerPrefab != null)
        {
            GameObject banner = Instantiate(bannerPrefab, go.transform);
            banner.name = "Ownership Banner";
            banner.transform.localPosition = new Vector3(0.15f, 0.7f, -0.45f);
            banner.transform.localScale = Vector3.one * 0.35f;
            banner.transform.localRotation = Quaternion.Euler(0f, t.Owner == TerritoryOwner.Enemy ? 180f : 0f, 0f);
            foreach (Collider collider in banner.GetComponentsInChildren<Collider>())
                Destroy(collider);
            MaterialPropertyBlock bannerProperties = new();
            foreach (Renderer renderer in banner.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(bannerProperties);
                bannerProperties.SetColor("_BaseColor", ColorFor(t));
                bannerProperties.SetColor("_Color", ColorFor(t));
                renderer.SetPropertyBlock(bannerProperties);
            }
        }

        nodes.Add(new NodeView { Go = go, Territory = t, Renderer = r });
    }

    private void BuildEdge(Territory a, Territory b)
    {
        Vector3 pa = WorldOf(a);
        Vector3 pb = WorldOf(b);
        Vector3 mid = (pa + pb) * 0.5f;
        mid.y = 0.05f;
        GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edge.name = "Edge";
        Destroy(edge.GetComponent<Collider>());
        edge.transform.SetParent(transform);
        edge.transform.position = mid;
        edge.transform.localScale = new Vector3(0.18f, 0.05f, Vector3.Distance(pa, pb));
        edge.transform.rotation = Quaternion.LookRotation((pb - pa).normalized);
        edge.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.3f, 0.28f, 0.22f));
    }

    private void BuildTrainingNode()
    {
        trainingNode = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trainingNode.name = "Training Arena Node";
        trainingNode.transform.SetParent(transform);
        trainingNode.transform.position = new Vector3(14f, 0.4f, -9f);
        trainingNode.transform.localScale = new Vector3(2.15f, 0.38f, 2.15f);
        trainingRenderer = trainingNode.GetComponent<Renderer>();
        trainingRenderer.sharedMaterial = RuntimeAssets.Material(new Color(0.72f, 0.55f, 0.14f));

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Training Ring";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(trainingNode.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        ring.transform.localScale = new Vector3(0.75f, 0.18f, 0.75f);
        ring.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.28f, 0.16f, 0.06f));

        GameObject labelObject = new GameObject("Training Arena Label");
        labelObject.transform.SetParent(trainingNode.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "TRAINING ARENA";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 42;
        label.characterSize = 0.065f;
        label.color = new Color(1f, 0.84f, 0.32f);
    }

    private static Color ColorFor(Territory t) => t.Owner switch
    {
        TerritoryOwner.Player => new Color(0.16f, 0.45f, 0.92f),
        TerritoryOwner.Enemy => new Color(0.85f, 0.16f, 0.12f),
        _ => new Color(0.6f, 0.6f, 0.62f)
    };

    private static Color ArenaColor(ArenaType arena) => arena switch
    {
        ArenaType.Forest => new Color(0.16f, 0.48f, 0.16f),
        ArenaType.Marsh => new Color(0.15f, 0.48f, 0.52f),
        ArenaType.Highlands => new Color(0.58f, 0.56f, 0.52f),
        _ => new Color(0.65f, 0.5f, 0.3f)
    };

    private void Update()
    {
        if (campaign == null || cam == null)
            return;

        if (campaign.CampaignOver)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                director.RestartCampaign();
            return;
        }

        if (travelling)
            UpdateTravel();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();

        UpdateMarkers();
        PulseNodes();
        RefreshUi();
    }

    private void HandleClick()
    {
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUi || !Physics.Raycast(cam.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit, 200f))
            return;

        if (hit.collider.gameObject == trainingNode)
        {
            director.LaunchBattle(campaign.BuildTrainingSetup());
            return;
        }
        foreach (PartyView view in partyViews)
            if (view.Marker != null && hit.collider.gameObject == view.Marker.gameObject)
            {
                BeginTravel(view.Party.Position, null, view.Party);
                return;
            }
        foreach (NodeView n in nodes)
            if (n.Go == hit.collider.gameObject)
            {
                BeginTravel(n.Territory.MapPosition, n.Territory, null);
                return;
            }
        BeginTravel(new Vector2(hit.point.x / 1.4f, hit.point.z / 1.4f), null, null);
    }

    private void BeginTravel(Vector2 target, Territory territory, EnemyParty party)
    {
        travelTarget = target;
        pendingTerritory = territory;
        pendingParty = party;
        travelling = true;
    }

    private void UpdateTravel()
    {
        Vector2 toDest = travelTarget - campaign.PartyPosition;
        float distance = toDest.magnitude;
        float step = TravelSpeed * Time.deltaTime;
        Vector2 move = distance <= step ? toDest : toDest.normalized * step;
        campaign.PartyPosition += move;

        dayAccumulator += move.magnitude;
        while (dayAccumulator >= DistancePerDay)
        {
            dayAccumulator -= DistancePerDay;
            campaign.Day++;
        }

        StepEnemyParties(move.magnitude);

        // A bandit catching the player (or being marched into) starts a battle.
        foreach (PartyView view in partyViews)
            if ((view.Party.Position - campaign.PartyPosition).magnitude <= EncounterRadius)
            {
                StartFieldBattle(view.Party);
                return;
            }

        if (distance <= step + 0.001f)
        {
            travelling = false;
            if (pendingParty != null)
                StartFieldBattle(pendingParty);
            else if (pendingTerritory != null)
                ArriveAtSettlement(pendingTerritory);
            pendingTerritory = null;
            pendingParty = null;
        }
    }

    private void StepEnemyParties(float playerStep)
    {
        float chase = playerStep * EnemyChaseRatio;
        foreach (PartyView view in partyViews)
        {
            Vector2 toPlayer = campaign.PartyPosition - view.Party.Position;
            float d = toPlayer.magnitude;
            if (d > 0.001f && d < EnemySightRange)
                view.Party.Position += toPlayer.normalized * Mathf.Min(chase, d);
        }
    }

    private void StartFieldBattle(EnemyParty party)
    {
        travelling = false;
        pendingTerritory = null;
        pendingParty = null;
        director.LaunchFieldBattle(campaign.BuildPartySetup(party), party);
    }

    private void ArriveAtSettlement(Territory t)
    {
        if (t.Owner == TerritoryOwner.Enemy)
            director.LaunchBattle(campaign.BuildSetupFor(t), t);
        else
            campaign.LastReport = $"The warband rests at {t.Name}.";
    }

    private void UpdateMarkers()
    {
        if (partyMarker != null)
            partyMarker.transform.position = WorldOf(campaign.PartyPosition) + Vector3.up * 0.9f;
        foreach (PartyView view in partyViews)
            if (view.Marker != null)
                view.Marker.position = WorldOf(view.Party.Position) + Vector3.up * 0.8f;
    }

    private void PulseNodes()
    {
        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (NodeView n in nodes)
        {
            Color color = ColorFor(n.Territory);
            if (n.Territory.Owner == TerritoryOwner.Enemy)
                color = Color.Lerp(color, Color.white, pulse * 0.5f);
            nodeColorProperties.SetColor("_BaseColor", color);
            nodeColorProperties.SetColor("_Color", color);
            n.Renderer.SetPropertyBlock(nodeColorProperties);
        }
        if (trainingRenderer != null)
        {
            Color color = Color.Lerp(new Color(0.72f, 0.55f, 0.14f), Color.white, pulse * 0.4f);
            nodeColorProperties.SetColor("_BaseColor", color);
            nodeColorProperties.SetColor("_Color", color);
            trainingRenderer.SetPropertyBlock(nodeColorProperties);
        }
    }

    private void BuildUi()
    {
        campaignCanvas = MedievalUi.CreateCanvas(transform, "Campaign HUD Canvas", 20);
        RectTransform top = MedievalUi.Frame(campaignCanvas.transform, "Campaign Header", new Vector2(0.22f, 0.9f),
            new Vector2(0.78f, 0.985f), Vector2.zero, Vector2.zero);
        MedievalUi.Label(top, "Title", "CONQUER OTHERS  -  PLAN YOUR NEXT ASSAULT", 30, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.48f), Vector2.one, Vector2.zero, Vector2.zero, MedievalUi.Gold);
        campaignSummary = MedievalUi.Label(top, "Summary", "", 18, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        reportText = MedievalUi.Label(campaignCanvas.transform, "Report", "", 18, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.855f), new Vector2(0.75f, 0.9f), Vector2.zero, Vector2.zero);

        RectTransform recruit = MedievalUi.Frame(campaignCanvas.transform, "Recruitment", new Vector2(0.012f, 0.55f),
            new Vector2(0.25f, 0.88f), Vector2.zero, Vector2.zero);
        MedievalUi.Label(recruit, "Title", "RECRUIT WARBAND", 27, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(recruit, "Recruit Divider", new Vector2(0.12f, 0.795f), new Vector2(0.88f, 0.818f),
            Vector2.zero, Vector2.zero);
        tierButton = MedievalUi.Button(recruit, "Tier", "", new Vector2(0.07f, 0.66f),
            new Vector2(0.93f, 0.78f), Vector2.zero, Vector2.zero, CycleRecruitTier);
        tierButtonText = tierButton.GetComponentInChildren<Text>();
        float recruitY = 0.5f;
        foreach (Archetype archetype in RecruitableArchetypes)
        {
            AddRecruitButton(recruit, archetype, recruitY);
            recruitY -= 0.14f;
        }

        RectTransform equipment = MedievalUi.Frame(campaignCanvas.transform, "Equipment", new Vector2(0.75f, 0.61f),
            new Vector2(0.988f, 0.88f), Vector2.zero, Vector2.zero);
        MedievalUi.Label(equipment, "Title", "CAPTAIN EQUIPMENT", 27, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(equipment, "Equipment Divider", new Vector2(0.12f, 0.695f), new Vector2(0.88f, 0.718f),
            Vector2.zero, Vector2.zero);
        equipmentText = MedievalUi.Label(equipment, "Weapon", "", 22, TextAnchor.MiddleCenter,
            new Vector2(0.12f, 0.35f), new Vector2(0.88f, 0.72f), Vector2.zero, Vector2.zero);
        MedievalUi.Button(equipment, "Previous Weapon", "<", new Vector2(0.08f, 0.08f), new Vector2(0.32f, 0.31f),
            Vector2.zero, Vector2.zero, () => campaign.PlayerWeapon = WeaponCatalog.Previous(campaign.PlayerWeapon));
        MedievalUi.Button(equipment, "Next Weapon", ">", new Vector2(0.68f, 0.08f), new Vector2(0.92f, 0.31f),
            Vector2.zero, Vector2.zero, () => campaign.PlayerWeapon = WeaponCatalog.Next(campaign.PlayerWeapon));

        RectTransform action = MedievalUi.Frame(campaignCanvas.transform, "Selection", new Vector2(0.26f, 0.02f),
            new Vector2(0.74f, 0.19f), Vector2.zero, Vector2.zero);
        selectionTitle = MedievalUi.Label(action, "Title", "", 27, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        selectionBody = MedievalUi.Label(action, "Body", "", 18, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.3f), new Vector2(0.96f, 0.67f), Vector2.zero, Vector2.zero);
        actionButton = MedievalUi.Button(action, "Action", "ASSAULT", new Vector2(0.28f, 0.03f),
            new Vector2(0.72f, 0.29f), Vector2.zero, Vector2.zero, PerformSelectedAction);
        actionButtonText = actionButton.GetComponentInChildren<Text>();

        endScreen = MedievalUi.Panel(campaignCanvas.transform, "Campaign End", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.018f, 0.014f, 0.012f, 0.96f));
        endTitle = MedievalUi.Label(endScreen, "End Title", "", 64, TextAnchor.MiddleCenter,
            new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.66f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Label(endScreen, "End Hint", "PRESS R TO BEGIN A NEW CAMPAIGN", 28, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.31f), new Vector2(0.75f, 0.43f), Vector2.zero, Vector2.zero);
    }

    private void AddRecruitButton(Transform parent, Archetype archetype, float y)
    {
        Button button = MedievalUi.Button(parent, archetype.ToString(), "", new Vector2(0.07f, y),
            new Vector2(0.93f, y + 0.12f), Vector2.zero, Vector2.zero, () => campaign.Recruit(selectedTier, archetype));
        recruitButtons[archetype] = new RecruitWidget { Button = button, Label = button.GetComponentInChildren<Text>() };
    }

    private void CycleRecruitTier() => selectedTier = (UnitType)(((int)selectedTier + 1) % 3);

    private void RefreshUi()
    {
        if (campaignCanvas == null || campaign == null)
            return;
        bool ended = campaign.CampaignOver;
        int rosterTotal = campaign.Roster;
        if (uiInitialized && ended == uiEnded && campaign.Gold == uiGold && rosterTotal == uiRosterTotal
            && selectedTier == uiSelectedTier && campaign.PlayerWeapon == uiWeapon
            && campaign.Day == uiDay && travelling == uiTravelling)
            return;
        uiInitialized = true;
        uiEnded = ended;
        uiGold = campaign.Gold;
        uiRosterTotal = rosterTotal;
        uiSelectedTier = selectedTier;
        uiWeapon = campaign.PlayerWeapon;
        uiDay = campaign.Day;
        uiTravelling = travelling;

        endScreen.gameObject.SetActive(ended);
        if (ended)
        {
            endTitle.text = "THE CAMPAIGN IS LOST";
            return;
        }
        campaignSummary.text = $"DAY {campaign.Day}    GOLD {campaign.Gold}    INCOME +{campaign.IncomePerVictory()}    " +
            $"WARBAND {campaign.Roster}/{CampaignState.WarbandCap}    HOLDS {campaign.PlayerTerritoryCount()}/{campaign.Territories.Count}";
        reportText.text = campaign.LastReport;
        equipmentText.text = $"{WeaponCatalog.Label(campaign.PlayerWeapon)}\n{WeaponCatalog.Description(campaign.PlayerWeapon)}";
        tierButtonText.text = $"TIER  <  {UnitCatalog.Label(selectedTier)}  >    {UnitCatalog.Cost(selectedTier)} GOLD";
        foreach (KeyValuePair<Archetype, RecruitWidget> entry in recruitButtons)
        {
            entry.Value.Button.interactable = !travelling && campaign.CanRecruit(selectedTier, entry.Key);
            entry.Value.Label.text =
                $"+ {ArchetypeCatalog.Label(entry.Key)}    OWNED {campaign.Units.Count(selectedTier, entry.Key)}";
        }

        if (travelling)
        {
            selectionTitle.text = "ON THE MARCH";
            selectionBody.text = $"Day {campaign.Day}  -  the warband moves across the land.";
        }
        else
        {
            selectionTitle.text = $"DAY {campaign.Day}";
            selectionBody.text = "Click a hold to assault, a red band to hunt, or open ground to march. Click the Training Arena to spar.";
        }
        actionButtonText.text = "WAIT A DAY";
        actionButton.interactable = !travelling;
    }

    // The action button passes a day in place: roaming bands advance toward the
    // warband while it holds position.
    private void PerformSelectedAction()
    {
        if (travelling)
            return;
        campaign.Day++;
        StepEnemyParties(DistancePerDay);
        foreach (PartyView view in partyViews)
            if ((view.Party.Position - campaign.PartyPosition).magnitude <= EncounterRadius)
            {
                StartFieldBattle(view.Party);
                return;
            }
    }
}
