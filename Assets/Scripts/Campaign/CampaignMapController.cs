using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Builds and runs the campaign map: an overhead view of territory nodes the
// player clicks to select and assault. Uses shared runtime materials and the
// same IMGUI HUD style as the battle.
public sealed class CampaignMapController : MonoBehaviour
{
    private GameDirector director;
    private CampaignState campaign;
    private Camera cam;
    private Territory selected;

    private readonly List<NodeView> nodes = new();
    private MaterialPropertyBlock nodeColorProperties;

    private Texture2D whiteTexture;
    private GUIStyle titleStyle;
    private GUIStyle labelCenter;
    private GUIStyle smallCenter;
    private GUIStyle buttonStyle;
    private GUIStyle recruitButtonStyle;
    private Rect actionPanelScreenRect;
    private bool actionPanelShown;
    private Rect recruitmentPanelScreenRect;
    private bool recruitmentPanelShown;

    private struct NodeView
    {
        public GameObject Go;
        public Territory Territory;
        public Renderer Renderer;
    }

    public void Configure(GameDirector gameDirector, CampaignState state)
    {
        director = gameDirector;
        campaign = state;
        nodeColorProperties = new MaterialPropertyBlock();
        BuildVisuals();
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
    }

    private static Vector3 WorldOf(Territory t) => new Vector3(t.MapPosition.x * 1.4f, 0.2f, t.MapPosition.y * 1.4f);

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

        bool ended = campaign.AllConquered() || campaign.CampaignOver;
        if (ended)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                director.RestartCampaign();
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 topLeft = new Vector2(mouse.x, Screen.height - mouse.y);
            bool overUi = actionPanelShown && actionPanelScreenRect.Contains(topLeft)
                || recruitmentPanelShown && recruitmentPanelScreenRect.Contains(topLeft);
            if (!overUi && Physics.Raycast(cam.ScreenPointToRay(mouse), out RaycastHit hit, 200f))
                foreach (NodeView n in nodes)
                    if (n.Go == hit.collider.gameObject)
                    {
                        selected = n.Territory;
                        break;
                    }
        }

        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (NodeView n in nodes)
        {
            Color color = ColorFor(n.Territory);
            if (campaign.IsAttackable(n.Territory))
                color = Color.Lerp(color, Color.white, pulse);
            if (n.Territory == selected)
                color = Color.Lerp(color, new Color(1f, 0.9f, 0.4f), 0.5f);
            nodeColorProperties.SetColor("_BaseColor", color);
            nodeColorProperties.SetColor("_Color", color);
            n.Renderer.SetPropertyBlock(nodeColorProperties);
        }
    }

    private void OnGUI()
    {
        if (campaign == null)
            return;
        EnsureStyles();
        float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.35f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (campaign.AllConquered())
            DrawEndScreen(width, height, "THE LAND IS YOURS",
                "Every territory flies your colors.\n\nPRESS R TO BEGIN A NEW CAMPAIGN");
        else if (campaign.CampaignOver)
            DrawEndScreen(width, height, "THE CAMPAIGN IS LOST",
                "Your captain has fallen on the field.\n\nPRESS R TO BEGIN A NEW CAMPAIGN");
        else
            DrawMapHud(width, height, scale);

        GUI.matrix = previous;
    }

    private void DrawMapHud(float width, float height, float scale)
    {
        DrawRecruitment(scale);
        DrawPanel(new Rect(width * 0.5f - 270f, 18f, 540f, 56f), new Color(0.03f, 0.04f, 0.05f, 0.88f));
        GUI.Label(new Rect(width * 0.5f - 260f, 24f, 520f, 24f), "CONQUER OTHERS — PLAN YOUR NEXT ASSAULT", labelCenter);
        GUI.Label(new Rect(width * 0.5f - 260f, 47f, 520f, 20f),
            $"GOLD {campaign.Gold}    INCOME +{campaign.IncomePerVictory()}    WARBAND {campaign.Roster}/{CampaignState.WarbandCap}    LANDS {campaign.PlayerTerritoryCount()}/{campaign.Territories.Count}", smallCenter);
        GUI.Label(new Rect(width * 0.5f - 300f, 76f, 600f, 20f), campaign.LastReport, smallCenter);

        if (selected == null)
        {
            actionPanelShown = false;
            GUI.Label(new Rect(width * 0.5f - 240f, height - 64f, 480f, 24f),
                "Click a glowing enemy territory to select it.", smallCenter);
            return;
        }

        Rect panel = new Rect(width * 0.5f - 310f, height - 158f, 620f, 136f);
        DrawPanel(panel, new Color(0.03f, 0.04f, 0.05f, 0.9f));
        actionPanelScreenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
        actionPanelShown = true;

        string owner = selected.Owner == TerritoryOwner.Player ? "YOURS"
            : selected.Owner == TerritoryOwner.Enemy ? "ENEMY" : "NEUTRAL";
        GUI.Label(new Rect(width * 0.5f - 295f, height - 150f, 590f, 24f),
            $"{selected.Name.ToUpperInvariant()}    [{owner}]    {ArenaLabel(selected.Arena)}", labelCenter);
        GUI.Label(new Rect(width * 0.5f - 295f, height - 126f, 590f, 22f),
            $"THREAT {ThreatLabel(selected.Threat)}    GARRISON {selected.Garrison}    REWARD {selected.RewardGold} GOLD    INCOME +{selected.Income}", smallCenter);

        if (campaign.IsAttackable(selected))
        {
            GUI.Label(new Rect(width * 0.5f - 295f, height - 102f, 590f, 22f),
                $"Deploy {campaign.Units.Militia} militia, {campaign.Units.Veterans} veterans, {campaign.Units.Guards} guards against {selected.Garrison} defenders.", smallCenter);
            if (GUI.Button(new Rect(width * 0.5f - 130f, height - 72f, 260f, 38f), $"ASSAULT {selected.Name.ToUpperInvariant()}", buttonStyle))
                director.LaunchBattle(campaign.BuildSetupFor(selected), selected);
        }
        else
        {
            GUI.Label(new Rect(width * 0.5f - 225f, height - 80f, 450f, 22f),
                selected.Owner == TerritoryOwner.Player
                    ? "Already under your banner."
                    : "Not bordering your lands — capture a path to it first.", smallCenter);
        }
    }

    private void DrawRecruitment(float scale)
    {
        Rect panel = new Rect(16f, 112f, 314f, 250f);
        DrawPanel(panel, new Color(0.03f, 0.04f, 0.05f, 0.92f));
        recruitmentPanelScreenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
        recruitmentPanelShown = true;

        GUI.Label(new Rect(28f, 122f, 290f, 25f), "RECRUIT WARBAND", labelCenter);
        GUI.Label(new Rect(28f, 148f, 290f, 38f),
            $"Capacity {campaign.Roster} / {CampaignState.WarbandCap}\nDeaths are permanent. Choose quality or numbers.", smallCenter);
        DrawRecruitButton(UnitType.Militia, 28f, 192f, "Affordable line fighter");
        DrawRecruitButton(UnitType.Veteran, 28f, 243f, "Tougher and more dangerous");
        DrawRecruitButton(UnitType.Guard, 28f, 294f, "Elite armor and damage");
    }

    private void DrawRecruitButton(UnitType type, float x, float y, string description)
    {
        bool previous = GUI.enabled;
        GUI.enabled = campaign.CanRecruit(type);
        if (GUI.Button(new Rect(x, y, 290f, 29f),
            $"+ {UnitCatalog.Label(type)}    {UnitCatalog.Cost(type)} GOLD    OWNED {campaign.Units.Get(type)}", recruitButtonStyle))
            campaign.Recruit(type);
        GUI.enabled = previous;
        GUI.Label(new Rect(x, y + 28f, 290f, 18f), description, smallCenter);
    }

    private static string ArenaLabel(ArenaType arena) => arena switch
    {
        ArenaType.Forest => "DEEP FOREST",
        ArenaType.Marsh => "FOGGY MARSH",
        ArenaType.Highlands => "ROCKY HIGHLANDS",
        _ => "FORTIFIED COURTYARD"
    };

    private static string ThreatLabel(int threat) => new string('*', Mathf.Clamp(threat, 1, 5));

    private void DrawEndScreen(float width, float height, string title, string body)
    {
        DrawPanel(new Rect(width * 0.5f - 285f, height * 0.5f - 160f, 570f, 320f), new Color(0.025f, 0.03f, 0.035f, 0.93f));
        GUI.Label(new Rect(width * 0.5f - 260f, height * 0.5f - 120f, 520f, 52f), title, titleStyle);
        GUI.Label(new Rect(width * 0.5f - 245f, height * 0.5f - 40f, 490f, 160f), body, smallCenter);
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
            alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.94f, 0.78f, 0.33f) }
        };
        labelCenter = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        smallCenter = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true,
            normal = { textColor = new Color(0.82f, 0.84f, 0.86f) }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        recruitButtonStyle = new GUIStyle(buttonStyle) { fontSize = 13 };
    }
}
