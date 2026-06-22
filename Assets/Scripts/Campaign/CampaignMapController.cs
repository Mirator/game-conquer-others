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
    private const float MinCameraHeight = 14f;
    private const float MaxCameraHeight = 78f;
    private const float ZoomStep = 6f;
    private const float PanSpeed = 0.05f;
    private const float StartCameraHeight = 22f; // zoomed in on the warband at campaign start, not the whole map
    private const float MapCameraLimitX = 40f;
    private const float MapCameraLimitZMin = -31f;
    private const float MapCameraLimitZMax = 35f;

    private GameDirector director;
    private CampaignState campaign;
    private OverworldSimulation sim;
    private Camera cam;
    private Light mapSun;
    private GameObject trainingNode;
    private Renderer trainingRenderer;
    private TextMesh mapTooltip;
    private GameObject partyMarker;
    private TextMesh partyCountLabel;
    private Canvas campaignCanvas;
    private Text campaignSummary;
    private Text reportText;
    private Text selectionTitle;
    private Text selectionBody;
    private Text equipmentText;
    private Text trainingEnemyEquipmentText;
    private Text recruitStatus;
    private Text endTitle;
    private RectTransform endScreen;
    private Button actionButton;
    private Text actionButtonText;
    private Button marchButton;
    private Text marchButtonText;
    private readonly Dictionary<Archetype, RecruitWidget> recruitButtons = new();
    private readonly Dictionary<Archetype, RecruitWidget> upgradeButtons = new();
    private Button tierButton;
    private Text tierButtonText;
    private Text promoteTitle;
    private UnitType selectedTier = UnitType.Militia;

    // The Recruit/Promote/Equipment panels are hidden until summoned from the bottom
    // toolbar; a single open-panel field keeps only one visible at a time (opening
    // one closes the others). The slim top strip and action panel stay on screen.
    private enum HudPanel { None, Recruit, Promote, Equipment }
    private HudPanel openPanel = HudPanel.None;
    private GameObject recruitPanel;
    private GameObject promotePanel;
    private GameObject equipmentPanel;

    // Sun/moon dial in the top strip: the orbiting body is repositioned every frame
    // and its phase caption rewritten only when the named phase changes.
    private RectTransform dialBody;
    private Image dialBodyImage;
    private Text dialPhase;
    private int dialPhaseIndex = -1;

    // The currently-selected march destination (set by clicking; confirmed by the
    // march button). Travel no longer fires on click — the player reviews the
    // target's stats and the day cost first.
    private enum SelectionKind { None, Territory, Party, Ground }
    private SelectionKind selectionKind;
    private Territory selectedTerritory;
    private EnemyParty selectedParty;
    private Vector2 selectedGround;
    private Territory hoveredTerritory;
    private EnemyParty hoveredParty;
    private bool hoveredTraining;
    private bool uiDirty;

    private readonly List<Transform> travelDashes = new();
    private Transform travelDashRoot;

    private static readonly Archetype[] RecruitableArchetypes =
        { Archetype.Soldier, Archetype.Shieldbearer, Archetype.Berserker, Archetype.Archer };

    private readonly List<NodeView> nodes = new();
    private readonly List<PartyView> partyViews = new();
    private MaterialPropertyBlock nodeColorProperties;

    // Cached signature of the last rendered HUD state so Update can skip the
    // string rebuild and Text writes on frames where nothing relevant changed.
    private bool uiInitialized;
    private int uiGold = -1;
    private int uiRosterTotal = -1;
    private int uiDay = -1;
    private int uiMorale = -1;
    private int uiRenown = -1;
    private bool uiTravelling;
    private UnitType uiSelectedTier;
    private WeaponType uiWeapon;
    private bool uiEnded;

    private struct PartyView
    {
        public EnemyParty Party;
        public Transform Marker;
        public Renderer Renderer;
    }

    private struct NodeView
    {
        public GameObject Go;
        public Territory Territory;
        public Renderer Renderer;
        public Renderer Halo;
        public TextMesh Label;
        public TextMesh Badge;
    }

    private struct RecruitWidget
    {
        public Button Button;
        public Text Label;
        public RectTransform ProgressFill;
    }

    public void Configure(GameDirector gameDirector, CampaignState state)
    {
        director = gameDirector;
        campaign = state;
        sim = new OverworldSimulation(state);
        nodeColorProperties = new MaterialPropertyBlock();
        BuildVisuals();
        BuildUi();
    }

    private void BuildVisuals()
    {
        GameObject sunObject = new GameObject("Map Sun");
        sunObject.transform.SetParent(transform);
        mapSun = sunObject.AddComponent<Light>();
        mapSun.type = LightType.Directional;
        // Angle/colour/intensity, ambient, fog, and camera background are driven each
        // frame by ApplyMapLighting from the overworld day/night phase.

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.skybox = null; // clear any battle skybox so it doesn't bleed onto the map

        GameObject camObject = new GameObject("Map Camera");
        camObject.transform.SetParent(transform);
        camObject.tag = "MainCamera";
        camObject.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
        cam = camObject.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
        camObject.AddComponent<AudioListener>();
        FocusCameraOn(campaign.PartyPosition, StartCameraHeight);

        new MapDioramaBuilder(transform).Build(campaign);

        foreach (Territory t in campaign.Territories)
            BuildNode(t);
        BuildTrainingNode();
        BuildPartyMarkers();
        BuildMapTooltip();
    }

    private static Vector3 WorldOf(Territory t) => WorldOf(t.MapPosition);
    private static Vector3 WorldOf(Vector2 mapPosition) => new Vector3(mapPosition.x * 1.4f, 0.2f, mapPosition.y * 1.4f);

    private void BuildPartyMarkers()
    {
        partyMarker = BuildPartyFigure("Player Party", new Color(0.2f, 0.55f, 1f), true, false);
        partyMarker.transform.position = WorldOf(campaign.PartyPosition);
        partyCountLabel = AddCountLabel(partyMarker.transform, sim.PlayerStrength);

        foreach (EnemyParty party in campaign.Parties)
        {
            GameObject figure = BuildPartyFigure($"Party {party.Name}", new Color(0.85f, 0.2f, 0.12f), false, true);
            figure.transform.position = WorldOf(party.Position);
            AddCountLabel(figure.transform, party.Strength);
            partyViews.Add(new PartyView { Party = party, Marker = figure.transform, Renderer = figure.GetComponentInChildren<Renderer>() });
        }
    }

    // A small captain-style soldier (body, head, crest). The player wears a gold
    // crest; clickable figures get a root collider so the band can be selected.
    private GameObject BuildPartyFigure(string name, Color color, bool captainCrest, bool clickable)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform);
        MakePart(root.transform, "Body", PrimitiveType.Capsule,
            new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.55f, 0.5f), color);
        MakePart(root.transform, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 1.28f, 0f), Vector3.one * 0.32f, new Color(0.72f, 0.5f, 0.32f));
        MakePart(root.transform, "Crest", PrimitiveType.Cube,
            new Vector3(0f, 1.5f, 0f), new Vector3(0.1f, 0.2f, 0.36f),
            captainCrest ? new Color(0.96f, 0.84f, 0.26f) : color);
        if (clickable)
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.85f, 0f);
            collider.size = new Vector3(0.9f, 1.8f, 0.9f);
        }
        return root;
    }

    private static void MakePart(Transform parent, string name, PrimitiveType type,
        Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
    }

    private static TextMesh AddCountLabel(Transform parent, int count)
    {
        GameObject labelObject = new GameObject("Count");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = count.ToString();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 40;
        label.characterSize = 0.07f;
        label.color = Color.white;
        return label;
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

        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "Selection Halo";
        Destroy(halo.GetComponent<Collider>());
        halo.transform.SetParent(go.transform, false);
        halo.transform.localPosition = new Vector3(0f, -0.16f, 0f);
        halo.transform.localScale = new Vector3(1.55f, 0.04f, 1.55f);
        Renderer haloRenderer = halo.GetComponent<Renderer>();
        haloRenderer.sharedMaterial = RuntimeAssets.Material(new Color(0.96f, 0.77f, 0.2f), true);
        halo.SetActive(false);

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

        GameObject labelObject = new GameObject("Name Label");
        labelObject.transform.SetParent(go.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 3.4f, 0f);
        labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = t.Name;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 38;
        label.characterSize = 0.06f;
        label.color = NameColorFor(t);

        TextMesh badge = CreateWorldLabel(go.transform, "Settlement Badge",
            $"{SettlementCatalog.Label(t.Settlement)}  •  {t.Garrison} GARRISON", 25,
            new Vector3(0f, 2.75f, 0f), new Color(0.95f, 0.9f, 0.76f));

        nodes.Add(new NodeView { Go = go, Territory = t, Renderer = r, Halo = haloRenderer, Label = label, Badge = badge });
    }

    // Hold name color doubles as an at-a-glance assault cue: red names mark enemy
    // holds you can march on and assault, blue names mark your own holds.
    private static Color NameColorFor(Territory t) => t.Owner switch
    {
        TerritoryOwner.Player => new Color(0.55f, 0.78f, 1f),
        _ => new Color(1f, 0.55f, 0.45f)
    };

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

    private static TextMesh CreateWorldLabel(Transform parent, string name, string value, int size, Vector3 localPosition, Color color)
    {
        GameObject labelObject = new GameObject(name);
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = value;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = size;
        label.characterSize = 0.045f;
        label.color = color;
        return label;
    }

    private void BuildMapTooltip()
    {
        GameObject tooltip = new GameObject("Map Hover Tooltip");
        tooltip.transform.SetParent(transform, false);
        mapTooltip = tooltip.AddComponent<TextMesh>();
        mapTooltip.anchor = TextAnchor.MiddleCenter;
        mapTooltip.alignment = TextAlignment.Center;
        mapTooltip.fontSize = 34;
        mapTooltip.characterSize = 0.06f;
        mapTooltip.color = MedievalUi.Gold;
        tooltip.SetActive(false);
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

        // Escape (or the MENU button) opens the pause menu — the way to reach
        // settings, return to title, or quit from the map. While paused the map
        // freezes and ignores camera/click input; the menu canvas sits on top.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            director.TogglePause();
        if (director.IsPaused)
            return;

        CameraControls();

        if (campaign.CampaignOver)
        {
            RefreshUi();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                director.RestartCampaign();
            return;
        }

        if (sim.Travelling)
        {
            ClearHover();
            Dispatch(sim.Tick(Time.deltaTime));
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();

        if (!sim.Travelling)
            UpdateHover();

        float timeOfDay = CampaignState.OverworldSunPhase(campaign.Day, sim.DayFraction);
        ApplyMapLighting(timeOfDay);
        UpdateDial(timeOfDay);

        UpdateMarkers();
        PulseNodes();
        RefreshUi();
    }

    // Routes a simulation outcome to the battle/report side effects.
    private void Dispatch(OverworldOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case OverworldOutcomeKind.FieldBattle:
                director.LaunchFieldBattle(campaign.BuildPartySetup(outcome.Party), outcome.Party);
                break;
            case OverworldOutcomeKind.ArriveEnemy:
                director.LaunchBattle(campaign.BuildSetupFor(outcome.Territory), outcome.Territory);
                break;
            case OverworldOutcomeKind.RestAtHold:
                campaign.LastReport = $"The warband rests at {outcome.Territory.Name}.";
                break;
        }
    }

    // Mouse wheel zooms along the view; right-button drag pans across the map.
    // Centres the fixed-pitch camera over a map position at a given height by
    // sliding back along its view axis, so the point sits under the screen centre.
    private void FocusCameraOn(Vector2 mapPosition, float height)
    {
        Vector3 ground = WorldOf(mapPosition);
        Vector3 forward = cam.transform.forward;
        float back = (height - ground.y) / -forward.y; // forward.y is negative (camera looks down)
        cam.transform.position = ground - forward * back;
    }

    private void CameraControls()
    {
        if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            FocusCameraOn(campaign.PartyPosition, StartCameraHeight);
        if (Mouse.current == null)
            return;
        Transform t = cam.transform;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Vector3 desired = t.position + t.forward * Mathf.Sign(scroll) * ZoomStep;
            if (desired.y >= MinCameraHeight && desired.y <= MaxCameraHeight)
                t.position = desired;
        }

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            Vector3 planarForward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            float scale = PanSpeed * (t.position.y / 30f);
            Vector3 move = (-t.right * delta.x - planarForward * delta.y) * scale;
            move.y = 0f;
            t.position += move;
            ClampCameraToTable();
        }
    }

    // Keep the point under the centre of the map camera on the generated table.
    // This prevents right-dragging into empty space and Home provides an explicit
    // recovery route back to the warband after exploring the map.
    private void ClampCameraToTable()
    {
        Vector3 forward = cam.transform.forward;
        float distance = cam.transform.position.y / Mathf.Max(-forward.y, 0.01f);
        Vector3 center = cam.transform.position + forward * distance;
        center.x = Mathf.Clamp(center.x, -MapCameraLimitX, MapCameraLimitX);
        center.z = Mathf.Clamp(center.z, MapCameraLimitZMin, MapCameraLimitZMax);
        cam.transform.position = center - forward * distance;
    }

    // Clicking selects a march destination (or launches training directly); the
    // player confirms travel with the march button after reviewing the target.
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
                SelectParty(view.Party);
                return;
            }
        foreach (NodeView n in nodes)
            if (n.Go == hit.collider.gameObject)
            {
                SelectTerritory(n.Territory);
                return;
            }
        SelectGround(new Vector2(hit.point.x / 1.4f, hit.point.z / 1.4f));
    }

    // Hover mirrors the click targets, but is deliberately presentation-only: it
    // never changes travel or selection state and disappears above the HUD.
    private void UpdateHover()
    {
        if (Mouse.current == null || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()
            || !Physics.Raycast(cam.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit, 200f))
        {
            ClearHover();
            return;
        }

        Territory territory = null;
        EnemyParty party = null;
        bool training = hit.collider.gameObject == trainingNode;
        if (!training)
        {
            foreach (PartyView view in partyViews)
                if (view.Marker != null && hit.collider.gameObject == view.Marker.gameObject)
                {
                    party = view.Party;
                    break;
                }
            if (party == null)
                foreach (NodeView node in nodes)
                    if (node.Go == hit.collider.gameObject)
                    {
                        territory = node.Territory;
                        break;
                    }
        }

        hoveredTerritory = territory;
        hoveredParty = party;
        hoveredTraining = training;
        RefreshMapTooltip();
    }

    private void ClearHover()
    {
        if (hoveredTerritory == null && hoveredParty == null && !hoveredTraining && (mapTooltip == null || !mapTooltip.gameObject.activeSelf))
            return;
        hoveredTerritory = null;
        hoveredParty = null;
        hoveredTraining = false;
        if (mapTooltip != null)
            mapTooltip.gameObject.SetActive(false);
    }

    private void RefreshMapTooltip()
    {
        if (mapTooltip == null)
            return;
        if (hoveredTerritory != null)
        {
            Territory territory = hoveredTerritory;
            mapTooltip.text = $"{territory.Name.ToUpperInvariant()}\n{SettlementCatalog.Label(territory.Settlement)}  •  GARRISON {territory.Garrison}";
            mapTooltip.transform.position = WorldOf(territory.MapPosition) + Vector3.up * 4.7f;
        }
        else if (hoveredParty != null)
        {
            mapTooltip.text = $"{hoveredParty.Name}\nHOST {hoveredParty.Strength}";
            mapTooltip.transform.position = WorldOf(hoveredParty.Position) + Vector3.up * 3.2f;
        }
        else if (hoveredTraining)
        {
            mapTooltip.text = "TRAINING ARENA\nCONSEQUENCE-FREE PRACTICE";
            mapTooltip.transform.position = trainingNode.transform.position + Vector3.up * 3.2f;
        }
        else
        {
            mapTooltip.gameObject.SetActive(false);
            return;
        }

        mapTooltip.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mapTooltip.gameObject.SetActive(true);
    }

    private void SelectTerritory(Territory t)
    {
        selectionKind = SelectionKind.Territory;
        selectedTerritory = t;
        selectedParty = null;
        MarkSelectionChanged();
    }

    private void SelectParty(EnemyParty party)
    {
        selectionKind = SelectionKind.Party;
        selectedParty = party;
        selectedTerritory = null;
        MarkSelectionChanged();
    }

    private void SelectGround(Vector2 mapPosition)
    {
        selectionKind = SelectionKind.Ground;
        selectedGround = mapPosition;
        selectedTerritory = null;
        selectedParty = null;
        MarkSelectionChanged();
    }

    private void ClearSelection()
    {
        selectionKind = SelectionKind.None;
        selectedTerritory = null;
        selectedParty = null;
        MarkSelectionChanged();
    }

    private void MarkSelectionChanged()
    {
        uiDirty = true;
        UpdateTravelPreview();
    }

    private Vector2 SelectionTarget() => selectionKind switch
    {
        SelectionKind.Territory => selectedTerritory.MapPosition,
        SelectionKind.Party => selectedParty.Position,
        SelectionKind.Ground => selectedGround,
        _ => campaign.PartyPosition
    };

    // Commits travel to the current selection. Travel resolves on arrival: an
    // enemy hold is assaulted, a band is hunted, a friendly hold is rested at.
    private void ConfirmTravel()
    {
        if (sim.Travelling || selectionKind == SelectionKind.None)
            return;
        switch (selectionKind)
        {
            case SelectionKind.Territory:
                sim.BeginTravel(selectedTerritory.MapPosition, selectedTerritory, null);
                break;
            case SelectionKind.Party:
                sim.BeginTravel(selectedParty.Position, null, selectedParty);
                break;
            case SelectionKind.Ground:
                sim.BeginTravel(selectedGround, null, null);
                break;
        }
        ClearSelection();
    }

    private void UpdateMarkers()
    {
        if (partyMarker != null)
            partyMarker.transform.position = WorldOf(campaign.PartyPosition);
        foreach (PartyView view in partyViews)
            if (view.Marker != null)
                view.Marker.position = WorldOf(view.Party.Position);
    }

    private void PulseNodes()
    {
        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (NodeView n in nodes)
        {
            Color color = ColorFor(n.Territory);
            bool selected = selectionKind == SelectionKind.Territory && n.Territory == selectedTerritory;
            bool hovered = n.Territory == hoveredTerritory;
            if (selected || hovered)
                color = Color.Lerp(color, new Color(1f, 0.86f, 0.32f), 0.55f + pulse * 0.45f);
            else if (n.Territory.Owner == TerritoryOwner.Enemy)
                color = Color.Lerp(color, Color.white, pulse * 0.5f);
            nodeColorProperties.SetColor("_BaseColor", color);
            nodeColorProperties.SetColor("_Color", color);
            n.Renderer.SetPropertyBlock(nodeColorProperties);
            if (n.Halo != null)
                n.Halo.gameObject.SetActive(selected || hovered);
        }
        if (trainingRenderer != null)
        {
            float emphasis = hoveredTraining ? 0.68f + pulse * 0.32f : pulse * 0.4f;
            Color color = Color.Lerp(new Color(0.72f, 0.55f, 0.14f), Color.white, emphasis);
            nodeColorProperties.SetColor("_BaseColor", color);
            nodeColorProperties.SetColor("_Color", color);
            trainingRenderer.SetPropertyBlock(nodeColorProperties);
        }
        foreach (PartyView view in partyViews)
        {
            if (view.Renderer == null)
                continue;
            Color color = view.Party == hoveredParty
                ? Color.Lerp(new Color(0.85f, 0.2f, 0.12f), new Color(1f, 0.82f, 0.3f), 0.65f + pulse * 0.35f)
                : new Color(0.85f, 0.2f, 0.12f);
            nodeColorProperties.SetColor("_BaseColor", color);
            nodeColorProperties.SetColor("_Color", color);
            view.Renderer.SetPropertyBlock(nodeColorProperties);
        }
    }

    // Lays a dotted gold trail from the warband to the selected destination so the
    // route and its reach read at a glance; hidden while marching or with no target.
    private void UpdateTravelPreview()
    {
        bool show = !sim.Travelling && selectionKind != SelectionKind.None;
        if (!show)
        {
            foreach (Transform dash in travelDashes)
                if (dash != null)
                    dash.gameObject.SetActive(false);
            return;
        }

        Vector3 from = WorldOf(campaign.PartyPosition);
        Vector3 to = WorldOf(SelectionTarget());
        from.y = to.y = 0.35f;
        Vector3 delta = to - from;
        float length = delta.magnitude;
        Vector3 dir = length > 0.001f ? delta / length : Vector3.forward;
        const float spacing = 1.6f;
        int count = Mathf.Clamp(Mathf.RoundToInt(length / spacing), 1, 60);
        EnsureDashes(count);
        Quaternion rotation = Quaternion.LookRotation(dir);
        for (int i = 0; i < travelDashes.Count; i++)
        {
            if (i < count)
            {
                travelDashes[i].SetPositionAndRotation(from + dir * ((i + 1) * (length / (count + 1))), rotation);
                travelDashes[i].gameObject.SetActive(true);
            }
            else
                travelDashes[i].gameObject.SetActive(false);
        }
    }

    private void EnsureDashes(int count)
    {
        if (travelDashRoot == null)
        {
            travelDashRoot = new GameObject("Travel Preview").transform;
            travelDashRoot.SetParent(transform, false);
        }
        while (travelDashes.Count < count)
        {
            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Dash";
            Destroy(dash.GetComponent<Collider>());
            dash.transform.SetParent(travelDashRoot, false);
            dash.transform.localScale = new Vector3(0.16f, 0.06f, 0.7f);
            dash.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.96f, 0.82f, 0.32f), true);
            travelDashes.Add(dash.transform);
        }
    }

    private static string Stars(int threat)
    {
        int filled = Mathf.Clamp(threat, 0, 5);
        return new string('★', filled) + new string('☆', 5 - filled);
    }

    private void BuildUi()
    {
        campaignCanvas = MedievalUi.CreateCanvas(transform, "Campaign HUD Canvas", 20);

        // Slim top resource strip: the day/night dial on the left, a single compact
        // status line filling the rest. Leaves the map otherwise clear.
        RectTransform top = MedievalUi.Frame(campaignCanvas.transform, "Resource Strip", new Vector2(0.30f, 0.945f),
            new Vector2(0.70f, 0.992f), Vector2.zero, Vector2.zero);
        BuildDial(top);
        campaignSummary = MedievalUi.Label(top, "Summary", "", 16, TextAnchor.MiddleLeft,
            new Vector2(0.10f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        reportText = MedievalUi.Label(campaignCanvas.transform, "Report", "", 16, TextAnchor.MiddleCenter,
            new Vector2(0.30f, 0.905f), new Vector2(0.70f, 0.94f), Vector2.zero, Vector2.zero);

        // Top-right MENU button opens the pause menu (settings / return to title / quit).
        RectTransform menuBar = MedievalUi.Frame(campaignCanvas.transform, "Menu Bar", new Vector2(0.905f, 0.945f),
            new Vector2(0.99f, 0.992f), Vector2.zero, Vector2.zero);
        MedievalUi.Button(menuBar, "Menu", "MENU", new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f),
            Vector2.zero, Vector2.zero, () => director.TogglePause());

        RectTransform legend = MedievalUi.Frame(campaignCanvas.transform, "Map Legend", new Vector2(0.012f, 0.81f),
            new Vector2(0.185f, 0.93f), Vector2.zero, Vector2.zero);
        MedievalUi.Label(legend, "Legend", "MAP KEY\nBLUE  YOUR HOLD\nRED  ENEMY HOLD\nGOLD  TRAINING / SELECTED", 15,
            TextAnchor.MiddleLeft, new Vector2(0.08f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);

        // On-demand panels: hidden until summoned from the bottom toolbar. Only the
        // outer frame rects differ from the old always-on layout; the child widgets
        // (tier button, recruit/upgrade buttons, XP bars, weapon picker) are unchanged.
        RectTransform recruit = MedievalUi.Frame(campaignCanvas.transform, "Recruitment", new Vector2(0.32f, 0.30f),
            new Vector2(0.50f, 0.86f), Vector2.zero, Vector2.zero);
        recruitPanel = recruit.gameObject;
        MedievalUi.Label(recruit, "Title", "RECRUIT WARBAND", 26, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.99f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(recruit, "Recruit Divider", new Vector2(0.12f, 0.838f), new Vector2(0.88f, 0.858f),
            Vector2.zero, Vector2.zero);
        tierButton = MedievalUi.Button(recruit, "Tier", "", new Vector2(0.07f, 0.715f),
            new Vector2(0.93f, 0.825f), Vector2.zero, Vector2.zero, CycleRecruitTier);
        tierButtonText = tierButton.GetComponentInChildren<Text>();
        recruitStatus = MedievalUi.Label(recruit, "Status", "", 15, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.63f), new Vector2(0.95f, 0.705f), Vector2.zero, Vector2.zero);
        float recruitY = 0.49f;
        foreach (Archetype archetype in RecruitableArchetypes)
        {
            AddRecruitButton(recruit, archetype, recruitY);
            recruitY -= 0.135f;
        }

        RectTransform promote = MedievalUi.Frame(campaignCanvas.transform, "Promotion", new Vector2(0.50f, 0.30f),
            new Vector2(0.68f, 0.86f), Vector2.zero, Vector2.zero);
        promotePanel = promote.gameObject;
        promoteTitle = MedievalUi.Label(promote, "Title", "PROMOTE", 27, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(promote, "Promote Divider", new Vector2(0.12f, 0.795f), new Vector2(0.88f, 0.818f),
            Vector2.zero, Vector2.zero);
        float promoteY = 0.62f;
        foreach (Archetype archetype in RecruitableArchetypes)
        {
            AddUpgradeButton(promote, archetype, promoteY);
            promoteY -= 0.18f;
        }

        RectTransform equipment = MedievalUi.Frame(campaignCanvas.transform, "Equipment", new Vector2(0.39f, 0.36f),
            new Vector2(0.61f, 0.82f), Vector2.zero, Vector2.zero);
        equipmentPanel = equipment.gameObject;
        MedievalUi.Label(equipment, "Title", "TRAINING LOADOUT", 27, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Divider(equipment, "Equipment Divider", new Vector2(0.12f, 0.775f), new Vector2(0.88f, 0.798f),
            Vector2.zero, Vector2.zero);
        equipmentText = MedievalUi.Label(equipment, "Weapon", "", 22, TextAnchor.MiddleCenter,
            new Vector2(0.12f, 0.51f), new Vector2(0.88f, 0.75f), Vector2.zero, Vector2.zero);
        MedievalUi.Button(equipment, "Previous Weapon", "<", new Vector2(0.08f, 0.35f), new Vector2(0.32f, 0.48f),
            Vector2.zero, Vector2.zero, () => campaign.PlayerWeapon = WeaponCatalog.Previous(campaign.PlayerWeapon));
        MedievalUi.Button(equipment, "Next Weapon", ">", new Vector2(0.68f, 0.35f), new Vector2(0.92f, 0.48f),
            Vector2.zero, Vector2.zero, () => campaign.PlayerWeapon = WeaponCatalog.Next(campaign.PlayerWeapon));
        trainingEnemyEquipmentText = MedievalUi.Label(equipment, "Training Enemy Weapon", "", 18, TextAnchor.MiddleCenter,
            new Vector2(0.12f, 0.17f), new Vector2(0.88f, 0.34f), Vector2.zero, Vector2.zero);
        MedievalUi.Button(equipment, "Previous Training Enemy Weapon", "<", new Vector2(0.08f, 0.03f), new Vector2(0.32f, 0.14f),
            Vector2.zero, Vector2.zero, () => campaign.TrainingEnemyWeapon = WeaponCatalog.Previous(campaign.TrainingEnemyWeapon));
        MedievalUi.Button(equipment, "Next Training Enemy Weapon", ">", new Vector2(0.68f, 0.03f), new Vector2(0.92f, 0.14f),
            Vector2.zero, Vector2.zero, () => campaign.TrainingEnemyWeapon = WeaponCatalog.Next(campaign.TrainingEnemyWeapon));

        recruitPanel.SetActive(false);
        promotePanel.SetActive(false);
        equipmentPanel.SetActive(false);

        // Bottom icon toolbar: summons the panels above. Each button toggles its own
        // panel and closes the others (single open-panel field).
        RectTransform toolbar = MedievalUi.Frame(campaignCanvas.transform, "Toolbar", new Vector2(0.40f, 0.02f),
            new Vector2(0.60f, 0.075f), Vector2.zero, Vector2.zero);
        MedievalUi.Button(toolbar, "Recruit Toggle", "RECRUIT", new Vector2(0.04f, 0.12f), new Vector2(0.32f, 0.88f),
            Vector2.zero, Vector2.zero, () => TogglePanel(HudPanel.Recruit));
        MedievalUi.Button(toolbar, "Promote Toggle", "PROMOTE", new Vector2(0.34f, 0.12f), new Vector2(0.66f, 0.88f),
            Vector2.zero, Vector2.zero, () => TogglePanel(HudPanel.Promote));
        MedievalUi.Button(toolbar, "Equip Toggle", "EQUIP", new Vector2(0.68f, 0.12f), new Vector2(0.96f, 0.88f),
            Vector2.zero, Vector2.zero, () => TogglePanel(HudPanel.Equipment));

        RectTransform action = MedievalUi.Frame(campaignCanvas.transform, "Selection", new Vector2(0.33f, 0.085f),
            new Vector2(0.67f, 0.20f), Vector2.zero, Vector2.zero);
        selectionTitle = MedievalUi.Label(action, "Title", "", 22, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        selectionBody = MedievalUi.Label(action, "Body", "", 15, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.62f), Vector2.zero, Vector2.zero);
        marchButton = MedievalUi.Button(action, "March", "SELECT A TARGET", new Vector2(0.06f, 0.04f),
            new Vector2(0.49f, 0.28f), Vector2.zero, Vector2.zero, ConfirmTravel);
        marchButtonText = marchButton.GetComponentInChildren<Text>();
        actionButton = MedievalUi.Button(action, "Action", "WAIT A DAY", new Vector2(0.51f, 0.04f),
            new Vector2(0.94f, 0.28f), Vector2.zero, Vector2.zero, PerformSelectedAction);
        actionButtonText = actionButton.GetComponentInChildren<Text>();

        endScreen = MedievalUi.Panel(campaignCanvas.transform, "Campaign End", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.018f, 0.014f, 0.012f, 0.96f));
        endTitle = MedievalUi.Label(endScreen, "End Title", "", 64, TextAnchor.MiddleCenter,
            new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.66f), Vector2.zero, Vector2.zero, MedievalUi.Gold);
        MedievalUi.Label(endScreen, "End Hint", "PRESS R TO BEGIN A NEW CAMPAIGN", 28, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.31f), new Vector2(0.75f, 0.43f), Vector2.zero, Vector2.zero);
        endScreen.gameObject.SetActive(false);
    }

    // Summons one panel and hides the others; clicking the open panel's toolbar
    // button again closes it. Marks the HUD dirty so the now-visible panel's text
    // refreshes this frame (RefreshUi skips hidden panels).
    private void TogglePanel(HudPanel panel)
    {
        openPanel = openPanel == panel ? HudPanel.None : panel;
        recruitPanel.SetActive(openPanel == HudPanel.Recruit);
        promotePanel.SetActive(openPanel == HudPanel.Promote);
        equipmentPanel.SetActive(openPanel == HudPanel.Equipment);
        uiDirty = true;
    }

    // The sun/moon dial: a recessed face with an orbiting body and a phase caption.
    private void BuildDial(RectTransform strip)
    {
        RectTransform face = MedievalUi.Well(strip, "Dial", new Vector2(0.005f, 0.1f),
            new Vector2(0.085f, 0.9f), Vector2.zero, Vector2.zero, new Color(0.10f, 0.10f, 0.13f));
        // Centre-anchored, fixed-size body so anchoredPosition orbits cleanly around
        // the face centre (see UpdateDial).
        dialBody = MedievalUi.Panel(face, "Body", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-6f, -6f), new Vector2(6f, 6f), MedievalUi.Gold);
        dialBodyImage = dialBody.GetComponent<Image>();
        dialBodyImage.raycastTarget = false;
        dialPhase = MedievalUi.Label(face, "Phase", "", 11, TextAnchor.LowerCenter,
            Vector2.zero, new Vector2(1f, 0.24f), Vector2.zero, Vector2.zero, MedievalUi.Bone);
        dialPhase.raycastTarget = false;
    }

    // Drives the overworld Map Sun, ambient, fog, and camera background from a 0..1
    // time of day. A lightweight, arena-free cousin of BattleBootstrap.ApplySunAndSky
    // (the map has no skybox); midday values match the map's former static look.
    private void ApplyMapLighting(float t)
    {
        float daySin = Mathf.Sin(t * Mathf.PI * 2f - Mathf.PI * 0.5f); // -1 night .. +1 midday
        float day = Mathf.Clamp01(daySin * 0.5f + 0.5f);               // 0 night .. 1 midday
        float golden = 1f - Mathf.Abs(daySin);                         // 1 at dawn/dusk

        mapSun.transform.rotation = Quaternion.Euler(daySin * 50f + 8f, -30f + (t - 0.5f) * 50f, 0f);
        mapSun.color = Color.Lerp(new Color(1f, 0.96f, 0.9f), new Color(1f, 0.55f, 0.3f), golden)
            * new Color(1f, 0.95f, 0.85f);
        mapSun.intensity = 1.1f * Mathf.Lerp(0.08f, 1f, day);

        RenderSettings.ambientLight = new Color(0.35f, 0.37f, 0.4f) * Mathf.Lerp(0.4f, 1f, day);
        RenderSettings.fog = day < 0.5f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(new Color(0.04f, 0.05f, 0.08f), new Color(0.34f, 0.36f, 0.4f), day);
        RenderSettings.fogDensity = Mathf.Lerp(0.012f, 0.002f, day);
        cam.backgroundColor = Color.Lerp(new Color(0.02f, 0.025f, 0.05f), new Color(0.07f, 0.10f, 0.14f), day);
    }

    // Orbits the dial body by time of day (gold sun by day, bone moon by night) and
    // rewrites the phase caption only when the named phase changes.
    private void UpdateDial(float t)
    {
        if (dialBody == null)
            return;
        float ang = (t - 0.25f) * Mathf.PI * 2f; // .25 dawn -> right, .5 midday -> top
        dialBody.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 14f;
        bool daytime = t > 0.25f && t < 0.75f;
        dialBodyImage.color = daytime ? MedievalUi.Gold : MedievalUi.Bone;

        int phase = (t < 0.2f || t >= 0.8f) ? 3 : t < 0.35f ? 0 : t < 0.65f ? 1 : 2;
        if (phase != dialPhaseIndex)
        {
            dialPhaseIndex = phase;
            dialPhase.text = phase == 0 ? "DAWN" : phase == 1 ? "MIDDAY" : phase == 2 ? "DUSK" : "NIGHT";
        }
    }

    private void AddRecruitButton(Transform parent, Archetype archetype, float y)
    {
        Button button = MedievalUi.Button(parent, archetype.ToString(), "", new Vector2(0.07f, y),
            new Vector2(0.93f, y + 0.12f), Vector2.zero, Vector2.zero,
            () => campaign.Recruit(selectedTier, archetype, sim.RecruitSettlement()));
        recruitButtons[archetype] = new RecruitWidget { Button = button, Label = button.GetComponentInChildren<Text>() };
    }

    private void AddUpgradeButton(Transform parent, Archetype archetype, float y)
    {
        Button button = MedievalUi.Button(parent, archetype + " Promote", "", new Vector2(0.07f, y),
            new Vector2(0.93f, y + 0.16f), Vector2.zero, Vector2.zero,
            () => campaign.TryUpgrade(selectedTier, archetype));
        Text label = button.GetComponentInChildren<Text>();
        // Lift the label clear of the XP bar that sits along the button's base.
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = new Vector2(0f, 0.26f);
        labelRect.anchorMax = Vector2.one;
        RectTransform track = MedievalUi.Well(button.transform, "XP Track", new Vector2(0.06f, 0.07f),
            new Vector2(0.94f, 0.21f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.1f, 0.07f, 0.95f));
        track.GetComponent<Image>().raycastTarget = false;
        RectTransform fill = MedievalUi.Panel(track, "XP Fill", Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, Vector2.zero, MedievalUi.Gold);
        fill.GetComponent<Image>().raycastTarget = false;
        upgradeButtons[archetype] = new RecruitWidget { Button = button, Label = label, ProgressFill = fill };
    }

    // The first reason recruiting the given tier here is blocked, or null if it is
    // allowed. Surfaced under the recruit panel so a greyed button explains itself.
    private string RecruitBlockReason(UnitType tier, Territory settlement)
    {
        if (sim.Travelling)
            return "On the march - halt to recruit.";
        if (settlement == null)
            return "No settlement in range.";
        if (!SettlementCatalog.Allows(settlement.Settlement, tier))
            return $"{SettlementCatalog.Label(settlement.Settlement)} offers up to {UnitCatalog.Label(SettlementCatalog.MaxTier(settlement.Settlement))}.";
        if (settlement.Recruits <= 0)
            return "No volunteers left here.";
        if (campaign.Roster >= campaign.LeadershipCap)
            return $"Warband full ({campaign.LeadershipCap}) - raise renown.";
        if (campaign.Gold < UnitCatalog.Cost(tier))
            return $"Need {UnitCatalog.Cost(tier)} gold.";
        return null;
    }

    private void CycleRecruitTier() => selectedTier = (UnitType)(((int)selectedTier + 1) % 3);

    private void RefreshUi()
    {
        if (campaignCanvas == null || campaign == null)
            return;
        bool ended = campaign.CampaignOver;
        int rosterTotal = campaign.Roster;
        if (uiInitialized && !uiDirty && ended == uiEnded && campaign.Gold == uiGold && rosterTotal == uiRosterTotal
            && selectedTier == uiSelectedTier && campaign.PlayerWeapon == uiWeapon
            && campaign.Day == uiDay && sim.Travelling == uiTravelling
            && campaign.Morale == uiMorale && campaign.Renown == uiRenown)
            return;
        uiInitialized = true;
        uiDirty = false;
        uiEnded = ended;
        uiGold = campaign.Gold;
        uiRosterTotal = rosterTotal;
        uiSelectedTier = selectedTier;
        uiWeapon = campaign.PlayerWeapon;
        uiDay = campaign.Day;
        uiTravelling = sim.Travelling;
        uiMorale = campaign.Morale;
        uiRenown = campaign.Renown;

        endScreen.gameObject.SetActive(ended);
        if (ended)
        {
            endTitle.text = "THE CAMPAIGN IS LOST";
            return;
        }
        int net = campaign.DailyIncome() - campaign.DailyWage() - campaign.DailyGarrisonUpkeep();
        campaignSummary.text = $"DAY {campaign.Day}   GOLD {campaign.Gold} ({net:+0;-0;0}/d)   " +
            $"MOR {campaign.Morale}   REN {campaign.Renown}   " +
            $"WB {campaign.Roster}/{campaign.LeadershipCap}   HOLDS {campaign.PlayerTerritoryCount()}/{campaign.Territories.Count}";
        reportText.text = campaign.LastReport;
        if (partyCountLabel != null)
            partyCountLabel.text = sim.PlayerStrength.ToString();

        bool travelling = sim.Travelling;
        Territory settlement = sim.RecruitSettlement();

        // Only the open panel does its (relatively costly) string and bar rebuilds;
        // hidden panels are skipped. TogglePanel sets uiDirty so a panel refreshes
        // the instant it opens.
        if (openPanel == HudPanel.Equipment)
        {
            equipmentText.text = $"CAPTAIN: {WeaponCatalog.Label(campaign.PlayerWeapon)}\n{WeaponCatalog.Description(campaign.PlayerWeapon)}";
            trainingEnemyEquipmentText.text = $"TRAINING FOE: {WeaponCatalog.Label(campaign.TrainingEnemyWeapon)}";
        }

        if (openPanel == HudPanel.Recruit)
        {
            tierButtonText.text = $"TIER  <  {UnitCatalog.Label(selectedTier)}  >    {UnitCatalog.Cost(selectedTier)} GOLD";
            foreach (KeyValuePair<Archetype, RecruitWidget> entry in recruitButtons)
            {
                entry.Value.Button.interactable =
                    !travelling && campaign.CanRecruit(selectedTier, entry.Key, settlement);
                entry.Value.Label.text =
                    $"+ {ArchetypeCatalog.Label(entry.Key)}    OWNED {campaign.Units.Count(selectedTier, entry.Key)}";
            }
            string blockReason = RecruitBlockReason(selectedTier, settlement);
            recruitStatus.text = blockReason ?? $"{settlement.Name}: {settlement.Recruits} volunteers ready.";
            recruitStatus.color = blockReason == null ? MedievalUi.Bone : new Color(0.95f, 0.6f, 0.5f);
        }

        if (openPanel == HudPanel.Promote)
        {
            promoteTitle.text = $"PROMOTE {UnitCatalog.Label(selectedTier)}";
            bool topTier = !UnitCatalog.CanUpgrade(selectedTier);
            int needXp = topTier ? 0 : UnitCatalog.UpgradeXp(selectedTier);
            foreach (KeyValuePair<Archetype, RecruitWidget> entry in upgradeButtons)
            {
                entry.Value.Button.interactable = !travelling && campaign.CanUpgrade(selectedTier, entry.Key);
                int xp = campaign.Units.Xp(selectedTier, entry.Key);
                entry.Value.Label.text = topTier
                    ? $"{ArchetypeCatalog.Label(entry.Key)}  -  TOP TIER"
                    : $"^ {ArchetypeCatalog.Label(entry.Key)}  XP {xp}/{needXp}  {UnitCatalog.UpgradeCost(selectedTier)}G";
                float progress = topTier || needXp <= 0 ? 0f : Mathf.Clamp01((float)xp / needXp);
                entry.Value.ProgressFill.anchorMax = new Vector2(progress, 1f);
            }
        }

        RefreshSelection(travelling, settlement);
        actionButtonText.text = "WAIT A DAY";
        actionButton.interactable = !travelling;
    }

    // Drives the destination panel: the inspector body for the current selection,
    // the day-cost ETA, and the march button's contextual verb and enabled state.
    private void RefreshSelection(bool travelling, Territory settlement)
    {
        if (travelling)
        {
            selectionTitle.text = "ON THE MARCH";
            selectionBody.text = $"Day {campaign.Day}  -  the warband moves across the land.";
            marchButtonText.text = "MARCHING...";
            marchButton.interactable = false;
            return;
        }

        selectionTitle.text = $"DAY {campaign.Day}";
        marchButton.interactable = selectionKind != SelectionKind.None;
        switch (selectionKind)
        {
            case SelectionKind.Territory:
            {
                Territory t = selectedTerritory;
                int eta = sim.DaysTo(t.MapPosition);
                bool mine = t.Owner == TerritoryOwner.Player;
                string upkeep = mine ? $"  (-{CampaignState.GarrisonUpkeepPerHold}/day garrison)" : "";
                selectionBody.text =
                    $"{t.Name}  -  {(mine ? "YOUR HOLD" : "ENEMY HOLD")}, {SettlementCatalog.Label(t.Settlement)}\n" +
                    $"THREAT {Stars(t.Threat)}     GARRISON {t.Garrison}\n" +
                    $"REWARD {t.RewardGold}g     INCOME {t.Income}/day{upkeep}";
                marchButtonText.text = mine
                    ? $"REST AT {t.Name.ToUpperInvariant()}  ({TravelTimeLabel(eta)})"
                    : $"ASSAULT {t.Name.ToUpperInvariant()}  ({TravelTimeLabel(eta)})";
                break;
            }
            case SelectionKind.Party:
            {
                EnemyParty p = selectedParty;
                int eta = sim.DaysTo(p.Position);
                string note = sim.IsThreat(p) ? "Strong enough to give chase." : "Too weak to chase your host.";
                selectionBody.text = $"{p.Name}  -  strength {p.Strength}  vs your {sim.PlayerStrength}\n{note}";
                marchButtonText.text = $"HUNT {p.Name}  ({TravelTimeLabel(eta)})";
                break;
            }
            case SelectionKind.Ground:
            {
                int eta = sim.DaysTo(selectedGround);
                selectionBody.text = "Open ground.\nMarch here to scout, reposition, or draw bands out.";
                marchButtonText.text = $"MARCH HERE  ({TravelTimeLabel(eta)})";
                break;
            }
            default:
                selectionBody.text = settlement != null
                    ? $"At {settlement.Name} ({SettlementCatalog.Label(settlement.Settlement)}). Click a hold, a band, or open ground to plan a march."
                    : "Click a hold to assault, a red band to hunt, or open ground to march.";
                marchButtonText.text = "SELECT A TARGET";
                break;
        }
    }

    private static string TravelTimeLabel(int days) => days == 0 ? "SAME DAY" : $"{days}d";

    // The action button passes a day in place: roaming bands advance toward the
    // warband while it holds position.
    private void PerformSelectedAction()
    {
        if (sim.Travelling)
            return;
        Dispatch(sim.WaitOneDay());
    }
}
