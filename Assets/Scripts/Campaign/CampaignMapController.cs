using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Builds and runs the campaign map: an overhead view of territory nodes the
// player clicks to select and Enter to assault. Reuses BattleBootstrap's runtime
// material factory and the same IMGUI HUD style as the battle.
public sealed class CampaignMapController : MonoBehaviour
{
    private GameDirector director;
    private CampaignState campaign;
    private Camera cam;
    private Territory selected;

    private readonly List<NodeView> nodes = new();

    private Texture2D whiteTexture;
    private GUIStyle titleStyle;
    private GUIStyle labelCenter;
    private GUIStyle smallCenter;

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
        table.GetComponent<Renderer>().material = BattleBootstrap.CreateMaterial(new Color(0.18f, 0.2f, 0.16f));

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
        r.material = BattleBootstrap.CreateMaterial(ColorFor(t));

        GameObject keep = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keep.name = "Keep";
        Destroy(keep.GetComponent<Collider>());
        keep.transform.SetParent(go.transform, false);
        keep.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        keep.transform.localScale = new Vector3(0.5f, 2.2f, 0.5f);
        keep.GetComponent<Renderer>().material = BattleBootstrap.CreateMaterial(ColorFor(t));

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
        edge.GetComponent<Renderer>().material = BattleBootstrap.CreateMaterial(new Color(0.3f, 0.28f, 0.22f));
    }

    private static Color ColorFor(Territory t) => t.Owner switch
    {
        TerritoryOwner.Player => new Color(0.16f, 0.45f, 0.92f),
        TerritoryOwner.Enemy => new Color(0.85f, 0.16f, 0.12f),
        _ => new Color(0.6f, 0.6f, 0.62f)
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
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                foreach (NodeView n in nodes)
                    if (n.Go == hit.collider.gameObject)
                    {
                        selected = n.Territory;
                        break;
                    }
        }

        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame
            && selected != null && campaign.IsAttackable(selected))
            director.LaunchBattle(campaign.BuildSetupFor(selected), selected);

        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (NodeView n in nodes)
        {
            Color color = ColorFor(n.Territory);
            if (campaign.IsAttackable(n.Territory))
                color = Color.Lerp(color, Color.white, pulse);
            if (n.Territory == selected)
                color = Color.Lerp(color, new Color(1f, 0.9f, 0.4f), 0.5f);
            n.Renderer.material.color = color;
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
            DrawMapHud(width, height);

        GUI.matrix = previous;
    }

    private void DrawMapHud(float width, float height)
    {
        DrawPanel(new Rect(width * 0.5f - 270f, 18f, 540f, 56f), new Color(0.03f, 0.04f, 0.05f, 0.88f));
        GUI.Label(new Rect(width * 0.5f - 260f, 24f, 520f, 24f), "CONQUER OTHERS — PLAN YOUR NEXT ASSAULT", labelCenter);
        GUI.Label(new Rect(width * 0.5f - 260f, 47f, 520f, 20f),
            $"WARBAND  {campaign.Roster} soldiers        TERRITORIES  {campaign.PlayerTerritoryCount()} / {campaign.Territories.Count}", smallCenter);

        if (selected == null)
        {
            GUI.Label(new Rect(width * 0.5f - 240f, height - 64f, 480f, 24f),
                "Click a glowing enemy territory to select it.", smallCenter);
            return;
        }

        DrawPanel(new Rect(width * 0.5f - 240f, height - 122f, 480f, 100f), new Color(0.03f, 0.04f, 0.05f, 0.9f));
        string owner = selected.Owner == TerritoryOwner.Player ? "YOURS"
            : selected.Owner == TerritoryOwner.Enemy ? "ENEMY" : "NEUTRAL";
        GUI.Label(new Rect(width * 0.5f - 225f, height - 114f, 450f, 24f),
            $"{selected.Name.ToUpperInvariant()}    [{owner}]    GARRISON  {selected.Garrison}", labelCenter);

        if (campaign.IsAttackable(selected))
        {
            GUI.Label(new Rect(width * 0.5f - 225f, height - 86f, 450f, 22f),
                $"Lead {Mathf.Clamp(campaign.Roster, 0, 12)} soldiers against {selected.Garrison} defenders.", smallCenter);
            GUI.Label(new Rect(width * 0.5f - 225f, height - 60f, 450f, 26f), "PRESS ENTER TO ASSAULT", titleStyle);
        }
        else
        {
            GUI.Label(new Rect(width * 0.5f - 225f, height - 80f, 450f, 22f),
                selected.Owner == TerritoryOwner.Player
                    ? "Already under your banner."
                    : "Not bordering your lands — capture a path to it first.", smallCenter);
        }
    }

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
    }
}
