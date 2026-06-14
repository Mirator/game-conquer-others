using UnityEngine;
using UnityEngine.Rendering;

public sealed class BattleBootstrap : MonoBehaviour
{
    public static BattleBootstrap Instance { get; private set; }

    private GameObject battleRoot;
    private static Shader litShader;
    private static bool smokeStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (FindFirstObjectByType<BattleBootstrap>() != null)
            return;

        new GameObject("Battle Bootstrap").AddComponent<BattleBootstrap>();
    }

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        BuildBattle();
    }

    public void ResetBattle()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (battleRoot != null)
            Destroy(battleRoot);
        BuildBattle();
    }

    private void BuildBattle()
    {
        battleRoot = new GameObject("Conquer Others MVP");
        BattleManager manager = battleRoot.AddComponent<BattleManager>();
        BattleEffects effects = battleRoot.AddComponent<BattleEffects>();

        SetupLighting();
        BuildArena();

        Camera camera = CreateCamera();
        ThirdPersonCamera cameraRig = camera.gameObject.AddComponent<ThirdPersonCamera>();
        manager.Configure(effects, cameraRig);
        PlayerFighter player = SpawnPlayer(manager, new Vector3(0f, 0.05f, -10f));
        player.SetCamera(cameraRig);
        cameraRig.SetTarget(player.transform);

        if (!smokeStarted && System.Array.Exists(System.Environment.GetCommandLineArgs(), argument => argument == "-smoketest"))
        {
            smokeStarted = true;
            gameObject.AddComponent<BattleRuntimeSmoke>().Configure(manager);
        }

        SpawnAI(manager, Team.Allies, new Vector3(-3f, 0.05f, -9f));
        SpawnAI(manager, Team.Allies, new Vector3(3f, 0.05f, -9f));
        SpawnAI(manager, Team.Allies, new Vector3(0f, 0.05f, -7f));

        SpawnAI(manager, Team.Enemies, new Vector3(-4.5f, 0.05f, 9f));
        SpawnAI(manager, Team.Enemies, new Vector3(-1.5f, 0.05f, 10f));
        SpawnAI(manager, Team.Enemies, new Vector3(1.5f, 0.05f, 10f));
        SpawnAI(manager, Team.Enemies, new Vector3(4.5f, 0.05f, 9f));
    }

    private PlayerFighter SpawnPlayer(BattleManager manager, Vector3 position)
    {
        GameObject go = new GameObject("Player");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        PlayerFighter fighter = go.AddComponent<PlayerFighter>();
        fighter.Configure(manager, Team.Allies, true);
        manager.Register(fighter);
        return fighter;
    }

    private void SpawnAI(BattleManager manager, Team team, Vector3 position)
    {
        GameObject go = new GameObject(team == Team.Allies ? "Allied Soldier" : "Enemy Soldier");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        if (team == Team.Enemies)
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        AIFighter fighter = go.AddComponent<AIFighter>();
        fighter.Configure(manager, team, false);
        manager.Register(fighter);
    }

    private Camera CreateCamera()
    {
        foreach (Camera existing in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            existing.gameObject.SetActive(false);

        GameObject go = new GameObject("Battle Camera");
        go.transform.SetParent(battleRoot.transform);
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        camera.fieldOfView = 62f;
        camera.nearClipPlane = 0.15f;
        camera.farClipPlane = 180f;
        go.AddComponent<AudioListener>();
        return camera;
    }

    private void SetupLighting()
    {
        foreach (Light existing in FindObjectsByType<Light>(FindObjectsSortMode.None))
            existing.gameObject.SetActive(false);

        GameObject sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(battleRoot.transform);
        sunObject.transform.rotation = Quaternion.Euler(35f, -28f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.05f;
        sun.color = new Color(1f, 0.78f, 0.58f);
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.36f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.48f, 0.55f, 0.58f);
        RenderSettings.fogDensity = 0.012f;
    }

    private void BuildArena()
    {
        CreateBlock("Ground", new Vector3(0f, -0.35f, 0f), new Vector3(34f, 0.7f, 34f), new Color(0.24f, 0.34f, 0.16f));
        CreateBlock("Dirt Road", new Vector3(0f, 0.015f, 0f), new Vector3(8f, 0.035f, 31f), new Color(0.34f, 0.24f, 0.13f), false);
        CreateBlock("Cross Road", new Vector3(0f, 0.02f, 0f), new Vector3(28f, 0.04f, 5f), new Color(0.31f, 0.22f, 0.12f), false);
        Color stone = new Color(0.32f, 0.33f, 0.31f);
        CreateBlock("North Wall", new Vector3(0f, 1.5f, 17f), new Vector3(36f, 3.4f, 1f), stone);
        CreateBlock("South Wall", new Vector3(0f, 1.5f, -17f), new Vector3(36f, 3.4f, 1f), stone);
        CreateBlock("East Wall", new Vector3(17f, 1.5f, 0f), new Vector3(1f, 3.4f, 36f), stone);
        CreateBlock("West Wall", new Vector3(-17f, 1.5f, 0f), new Vector3(1f, 3.4f, 36f), stone);

        Color wood = new Color(0.28f, 0.14f, 0.055f);
        CreateBlock("Center Barricade", new Vector3(-7f, 0.65f, 1.8f), new Vector3(4.5f, 1.3f, 0.45f), wood);
        CreateBlock("Center Barricade", new Vector3(7f, 0.65f, -1.8f), new Vector3(4.5f, 1.3f, 0.45f), wood);
        CreateBlock("Stone Cover", new Vector3(-10f, 0.65f, 5f), new Vector3(2.5f, 1.3f, 2.5f), stone);
        CreateBlock("Stone Cover", new Vector3(10f, 0.65f, -5f), new Vector3(2.5f, 1.3f, 2.5f), stone);

        for (int i = -14; i <= 14; i += 4)
        {
            CreateBlock("Battlement", new Vector3(i, 3.65f, 16.8f), new Vector3(2f, 1.1f, 1.3f), stone);
            CreateBlock("Battlement", new Vector3(i, 3.65f, -16.8f), new Vector3(2f, 1.1f, 1.3f), stone);
        }

        for (int i = -12; i <= 12; i += 8)
        {
            CreateBlock("Blue Banner Pole", new Vector3(i, 2f, -16.15f), new Vector3(0.12f, 3.8f, 0.12f), wood, false);
            CreateBlock("Blue Banner", new Vector3(i + 0.7f, 3.15f, -16.1f), new Vector3(1.35f, 1.25f, 0.08f), new Color(0.08f, 0.32f, 0.78f), false);
            CreateBlock("Red Banner Pole", new Vector3(i, 2f, 16.15f), new Vector3(0.12f, 3.8f, 0.12f), wood, false);
            CreateBlock("Red Banner", new Vector3(i + 0.7f, 3.15f, 16.1f), new Vector3(1.35f, 1.25f, 0.08f), new Color(0.68f, 0.08f, 0.05f), false);
        }

        BuildTorch(new Vector3(-6f, 1.35f, -15.8f));
        BuildTorch(new Vector3(6f, 1.35f, -15.8f));
        BuildTorch(new Vector3(-6f, 1.35f, 15.8f));
        BuildTorch(new Vector3(6f, 1.35f, 15.8f));

        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            CreateBlock("Supply Crate", new Vector3(side * Random.Range(11.5f, 14.5f), 0.32f, Random.Range(-13f, 13f)),
                new Vector3(0.85f, 0.65f, 0.85f), wood);
        }
    }

    private void BuildTorch(Vector3 position)
    {
        Color wood = new Color(0.22f, 0.1f, 0.035f);
        Color flame = new Color(1f, 0.32f, 0.035f);
        CreateBlock("Torch Pole", position, new Vector3(0.13f, 2.7f, 0.13f), wood, false);

        GameObject flameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flameObject.name = "Torch Flame";
        flameObject.transform.SetParent(battleRoot.transform);
        flameObject.transform.position = position + Vector3.up * 1.55f;
        flameObject.transform.localScale = new Vector3(0.22f, 0.42f, 0.22f);
        Destroy(flameObject.GetComponent<Collider>());
        flameObject.GetComponent<Renderer>().material = CreateMaterial(flame, true);

        Light light = flameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.42f, 0.12f);
        light.intensity = 2.2f;
        light.range = 6f;
        light.shadows = LightShadows.Soft;
    }

    private void CreateBlock(string blockName, Vector3 position, Vector3 scale, Color color, bool collider = true)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = blockName;
        block.transform.SetParent(battleRoot.transform);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().material = CreateMaterial(color);
        if (!collider)
            Destroy(block.GetComponent<Collider>());
    }

    public static Material CreateMaterial(Color color, bool emissive = false)
    {
        if (litShader == null)
            litShader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
        litShader ??= Shader.Find("Legacy Shaders/Diffuse");
        litShader ??= Shader.Find("Sprites/Default");

        Material material = new Material(litShader);
        material.color = color;
        material.SetFloat("_Smoothness", 0.18f);
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }
        return material;
    }
}
