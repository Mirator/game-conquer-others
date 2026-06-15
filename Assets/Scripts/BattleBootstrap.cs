using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds a complete runtime battle (arena, lighting, camera, fighters) under a
// root GameObject supplied by the GameDirector. No longer the app entry point;
// the director owns lifecycle and mode switching.
public sealed class BattleBootstrap : MonoBehaviour
{
    private GameObject battleRoot;

    public BattleManager Build(GameObject root, BattleSetup setup)
    {
        battleRoot = root;

        BattleManager manager = battleRoot.AddComponent<BattleManager>();
        BattleEffects effects = battleRoot.AddComponent<BattleEffects>();

        SetupLighting(setup.Arena);
        BuildArena(setup.Arena);

        Camera camera = CreateCamera();
        ThirdPersonCamera cameraRig = camera.gameObject.AddComponent<ThirdPersonCamera>();
        manager.Configure(effects, cameraRig);
        PlayerFighter player = SpawnPlayer(manager, new Vector3(0f, 0.05f, -10f));
        player.SetCamera(cameraRig);
        cameraRig.SetTarget(player.transform);

        SpawnRoster(manager, Team.Allies, BuildAlliedRoster(setup), 1f);
        SpawnRoster(manager, Team.Enemies, BuildEnemyRoster(setup), setup.EnemyHealthScale);
        return manager;
    }

    private static List<UnitType> BuildAlliedRoster(BattleSetup setup)
    {
        List<UnitType> units = BuildRoster(setup.AllyMilitia, setup.AllyVeterans, setup.AllyGuards);
        if (units.Count == 0)
            for (int i = 0; i < Mathf.Clamp(setup.AllyCount, 0, 16); i++)
                units.Add(UnitType.Militia);
        return units;
    }

    private static List<UnitType> BuildEnemyRoster(BattleSetup setup)
    {
        int guards = Mathf.Clamp(setup.EnemyGuards, 0, setup.EnemyCount);
        int veterans = Mathf.Clamp(setup.EnemyVeterans, 0, setup.EnemyCount - guards);
        int militia = Mathf.Max(0, setup.EnemyCount - guards - veterans);
        return BuildRoster(militia, veterans, guards);
    }

    private static List<UnitType> BuildRoster(int militia, int veterans, int guards)
    {
        List<UnitType> units = new();
        for (int i = 0; i < guards && units.Count < 16; i++)
            units.Add(UnitType.Guard);
        for (int i = 0; i < veterans && units.Count < 16; i++)
            units.Add(UnitType.Veteran);
        for (int i = 0; i < militia && units.Count < 16; i++)
            units.Add(UnitType.Militia);
        return units;
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

    // Lays soldiers out in ranks centered on each team's spawn line, scaled to
    // however many fighters the encounter calls for and clamped inside the walls.
    private void SpawnRoster(BattleManager manager, Team team, List<UnitType> units, float healthScale)
    {
        const float spacing = 2.2f;
        const int perRow = 6;
        float baseZ = team == Team.Allies ? -8f : 9f;
        float rowStep = team == Team.Allies ? -1.6f : 1.6f;

        for (int i = 0; i < units.Count; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int rowCount = Mathf.Min(perRow, units.Count - row * perRow);
            float x = Mathf.Clamp((col - (rowCount - 1) * 0.5f) * spacing, -13f, 13f);
            float z = baseZ + row * rowStep;
            SpawnAI(manager, team, new Vector3(x, 0.05f, z), healthScale, units[i]);
        }
    }

    private void SpawnAI(BattleManager manager, Team team, Vector3 position, float healthScale, UnitType unitType)
    {
        GameObject go = new GameObject(team == Team.Allies ? "Allied Soldier" : "Enemy Soldier");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        if (team == Team.Enemies)
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        AIFighter fighter = go.AddComponent<AIFighter>();
        fighter.Configure(manager, team, false, healthScale, unitType);
        manager.Register(fighter);
    }

    private Camera CreateCamera()
    {
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

    private void SetupLighting(ArenaType arena)
    {
        GameObject sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(battleRoot.transform);
        sunObject.transform.rotation = Quaternion.Euler(35f, -28f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = arena == ArenaType.Forest ? 0.82f : arena == ArenaType.Marsh ? 0.92f : 1.05f;
        sun.color = arena == ArenaType.Marsh ? new Color(0.72f, 0.82f, 0.84f)
            : arena == ArenaType.Forest ? new Color(0.84f, 0.9f, 0.7f) : new Color(1f, 0.78f, 0.58f);
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = arena == ArenaType.Forest ? new Color(0.18f, 0.28f, 0.2f)
            : arena == ArenaType.Marsh ? new Color(0.28f, 0.34f, 0.36f) : new Color(0.28f, 0.32f, 0.36f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = arena == ArenaType.Marsh ? new Color(0.42f, 0.52f, 0.52f)
            : arena == ArenaType.Forest ? new Color(0.3f, 0.42f, 0.32f) : new Color(0.48f, 0.55f, 0.58f);
        RenderSettings.fogDensity = arena == ArenaType.Marsh ? 0.02f : 0.012f;
    }

    private void BuildArena(ArenaType arena)
    {
        if (arena == ArenaType.Forest)
            BuildForest();
        else if (arena == ArenaType.Marsh)
            BuildMarsh();
        else if (arena == ArenaType.Highlands)
            BuildHighlands();
        else
            BuildCourtyard();
    }

    private void BuildCourtyard()
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

    private void BuildForest()
    {
        Color grass = new Color(0.12f, 0.28f, 0.1f);
        Color earth = new Color(0.26f, 0.18f, 0.08f);
        Color bark = new Color(0.19f, 0.1f, 0.035f);
        Color leaves = new Color(0.08f, 0.24f, 0.07f);
        CreateBlock("Forest Floor", new Vector3(0f, -0.35f, 0f), new Vector3(34f, 0.7f, 34f), grass);
        CreateBlock("Forest Track", new Vector3(0f, 0.015f, 0f), new Vector3(6f, 0.035f, 31f), earth, false);
        BuildBoundary(new Color(0.17f, 0.2f, 0.14f), 1.8f);
        for (int i = 0; i < 22; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * Random.Range(8.5f, 14.8f);
            float z = Random.Range(-14f, 14f);
            CreateBlock("Tree Trunk", new Vector3(x, 1.45f, z), new Vector3(0.65f, 2.9f, 0.65f), bark);
            CreatePrimitive("Tree Crown", PrimitiveType.Sphere, new Vector3(x, 3.15f, z), new Vector3(2.3f, 2.5f, 2.3f), leaves, false);
        }
        CreateBlock("Fallen Log", new Vector3(-4.8f, 0.55f, 2f), new Vector3(5f, 1.1f, 0.8f), bark);
        CreateBlock("Fallen Log", new Vector3(5.5f, 0.55f, -3f), new Vector3(4f, 1.1f, 0.8f), bark);
    }

    private void BuildMarsh()
    {
        Color mud = new Color(0.19f, 0.23f, 0.16f);
        Color water = new Color(0.12f, 0.28f, 0.3f);
        Color reed = new Color(0.32f, 0.37f, 0.12f);
        CreateBlock("Marsh Ground", new Vector3(0f, -0.35f, 0f), new Vector3(34f, 0.7f, 34f), mud);
        BuildBoundary(new Color(0.2f, 0.24f, 0.2f), 1.2f);
        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 pool = new Vector3(side * Random.Range(7f, 13f), 0.02f, Random.Range(-13f, 13f));
            CreateBlock("Shallow Water", pool, new Vector3(Random.Range(3f, 6f), 0.04f, Random.Range(2f, 5f)), water, false);
            for (int r = 0; r < 3; r++)
                CreateBlock("Reeds", pool + new Vector3(Random.Range(-2f, 2f), 0.45f, Random.Range(-1.5f, 1.5f)),
                    new Vector3(0.1f, Random.Range(0.7f, 1.2f), 0.1f), reed, false);
        }
        CreateBlock("Old Causeway", new Vector3(0f, 0.04f, 0f), new Vector3(5.5f, 0.08f, 31f), new Color(0.3f, 0.25f, 0.16f), false);
        CreateBlock("Wrecked Cart", new Vector3(-5f, 0.65f, 1f), new Vector3(3.8f, 1.3f, 1f), new Color(0.25f, 0.13f, 0.04f));
        CreateBlock("Stone Rise", new Vector3(6f, 0.55f, -2f), new Vector3(3f, 1.1f, 3f), new Color(0.3f, 0.32f, 0.28f));
    }

    private void BuildHighlands()
    {
        Color rock = new Color(0.32f, 0.31f, 0.29f);
        Color moss = new Color(0.24f, 0.3f, 0.15f);
        CreateBlock("Highland Ground", new Vector3(0f, -0.35f, 0f), new Vector3(34f, 0.7f, 34f), moss);
        BuildBoundary(rock, 2.5f);
        CreateBlock("West Ridge", new Vector3(-11f, 1f, 1f), new Vector3(5f, 2f, 17f), rock);
        CreateBlock("East Ridge", new Vector3(11f, 1f, -1f), new Vector3(5f, 2f, 17f), rock);
        CreateBlock("North Menhir", new Vector3(-4f, 1.8f, 4f), new Vector3(1.3f, 3.6f, 1.2f), rock);
        CreateBlock("South Menhir", new Vector3(4f, 1.8f, -4f), new Vector3(1.3f, 3.6f, 1.2f), rock);
        for (int i = 0; i < 8; i++)
            CreateBlock("Boulder", new Vector3(Random.Range(-8f, 8f), 0.55f, Random.Range(-14f, 14f)),
                new Vector3(Random.Range(1f, 2f), Random.Range(0.8f, 1.5f), Random.Range(1f, 2f)), rock);
    }

    private void BuildBoundary(Color color, float height)
    {
        CreateBlock("North Boundary", new Vector3(0f, height * 0.5f, 17f), new Vector3(36f, height, 1f), color);
        CreateBlock("South Boundary", new Vector3(0f, height * 0.5f, -17f), new Vector3(36f, height, 1f), color);
        CreateBlock("East Boundary", new Vector3(17f, height * 0.5f, 0f), new Vector3(1f, height, 36f), color);
        CreateBlock("West Boundary", new Vector3(-17f, height * 0.5f, 0f), new Vector3(1f, height, 36f), color);
    }

    private void CreatePrimitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool collider)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = objectName;
        primitive.transform.SetParent(battleRoot.transform);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        if (!collider)
            Destroy(primitive.GetComponent<Collider>());
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
        flameObject.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(flame, true);

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
        block.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        if (!collider)
            Destroy(block.GetComponent<Collider>());
    }

}
