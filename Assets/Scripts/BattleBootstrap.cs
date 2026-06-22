using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds a complete runtime battle (arena, lighting, camera, fighters) under a
// root GameObject supplied by the GameDirector. No longer the app entry point;
// the director owns lifecycle and mode switching.
public sealed class BattleBootstrap : MonoBehaviour
{
    private const float StructureMinZ = 13f; // keep dressing clear of the +z enemy spawn lane

    private GameObject battleRoot;
    private PresentationCatalog presentation;
    private float timeOfDay = 0.5f;
    private Light sun;

    public BattleManager Build(GameObject root, BattleSetup setup)
    {
        battleRoot = root;
        presentation = PresentationCatalog.Load();
        timeOfDay = setup.TimeOfDay;

        BattleManager manager = battleRoot.AddComponent<BattleManager>();
        BattleEffects effects = battleRoot.AddComponent<BattleEffects>();
        effects.Initialize(setup.Arena);

        SetupLighting(setup.Arena);
        BuildArena(setup.Arena, setup.Kind);

        Camera camera = CreateCamera();
        ThirdPersonCamera cameraRig = camera.gameObject.AddComponent<ThirdPersonCamera>();
        manager.Configure(effects, cameraRig, setup.IsTraining);
        PlayerFighter player = SpawnPlayer(manager, new Vector3(0f, 0.05f, -10f), setup.PlayerWeapon);
        player.SetCamera(cameraRig);
        cameraRig.SetTarget(player.transform);

        SpawnRoster(manager, Team.Allies, BuildAlliedRoster(setup), 1f, false, setup.TrainingEnemyWeapon);
        SpawnRoster(manager, Team.Enemies, BuildEnemyRoster(setup), setup.EnemyHealthScale, setup.IsTraining, setup.TrainingEnemyWeapon);
        return manager;
    }

    private static List<UnitSpec> BuildAlliedRoster(BattleSetup setup)
    {
        if (setup.AllyComposition != null && setup.AllyComposition.Count > 0)
        {
            List<UnitSpec> composed = new();
            foreach (UnitSpec spec in setup.AllyComposition)
            {
                if (composed.Count >= BattleSetup.MaxDeployed)
                    break;
                composed.Add(spec);
            }
            return composed;
        }

        List<UnitSpec> units = BuildTierRoster(setup.AllyMilitia, setup.AllyVeterans, setup.AllyGuards);
        if (units.Count == 0)
            for (int i = 0; i < Mathf.Clamp(setup.AllyCount, 0, BattleSetup.MaxDeployed); i++)
                units.Add(Soldier(UnitType.Militia));
        return units;
    }

    private static List<UnitSpec> BuildEnemyRoster(BattleSetup setup)
    {
        if (setup.EnemyComposition != null && setup.EnemyComposition.Count > 0)
        {
            List<UnitSpec> composed = new();
            foreach (UnitSpec spec in setup.EnemyComposition)
            {
                if (composed.Count >= BattleSetup.MaxDeployed)
                    break;
                composed.Add(spec);
            }
            return composed;
        }

        int guards = Mathf.Clamp(setup.EnemyGuards, 0, setup.EnemyCount);
        int veterans = Mathf.Clamp(setup.EnemyVeterans, 0, setup.EnemyCount - guards);
        int militia = Mathf.Max(0, setup.EnemyCount - guards - veterans);
        return BuildTierRoster(militia, veterans, guards);
    }

    // Tier-only fallback (no archetype composition): preserves the original
    // tier-default weapons with baseline Soldier behavior.
    private static List<UnitSpec> BuildTierRoster(int militia, int veterans, int guards)
    {
        List<UnitSpec> units = new();
        for (int i = 0; i < guards && units.Count < BattleSetup.MaxDeployed; i++)
            units.Add(Soldier(UnitType.Guard));
        for (int i = 0; i < veterans && units.Count < BattleSetup.MaxDeployed; i++)
            units.Add(Soldier(UnitType.Veteran));
        for (int i = 0; i < militia && units.Count < BattleSetup.MaxDeployed; i++)
            units.Add(Soldier(UnitType.Militia));
        return units;
    }

    private static UnitSpec Soldier(UnitType tier)
        => new UnitSpec(tier, Archetype.Soldier, WeaponCatalog.DefaultFor(tier));

    private PlayerFighter SpawnPlayer(BattleManager manager, Vector3 position, WeaponType weapon)
    {
        GameObject go = new GameObject("Player");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        PlayerFighter fighter = go.AddComponent<PlayerFighter>();
        fighter.Configure(manager, Team.Allies, true, 1f, UnitType.Militia, weapon);
        manager.Register(fighter);
        return fighter;
    }

    // Lays soldiers out in ranks centered on each team's spawn line, scaled to
    // however many fighters the encounter calls for and clamped inside the walls.
    private void SpawnRoster(BattleManager manager, Team team, List<UnitSpec> units, float healthScale,
        bool forceWeapon, WeaponType forcedWeapon)
    {
        int count = units.Count;
        if (count == 0)
            return;
        const float spacing = 2.2f;
        // Widen the rank with the roster so big forces read as a battle line rather
        // than a deep column: small fights keep the familiar 6-wide rows, large ones
        // grow toward 12-wide and stay shallow enough to fit between the arena walls
        // (ground is +/-17, walls at +/-16.6). The z-clamp is a hard safety net so no
        // fighter ever spawns inside a wall regardless of count.
        int perRow = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(count * 2f)), 6, 12);
        float baseZ = team == Team.Allies ? -8f : 9f;
        float rowStep = team == Team.Allies ? -1.35f : 1.35f;

        for (int i = 0; i < count; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int rowCount = Mathf.Min(perRow, count - row * perRow);
            float x = Mathf.Clamp((col - (rowCount - 1) * 0.5f) * spacing, -13f, 13f);
            float z = Mathf.Clamp(baseZ + row * rowStep, -15.8f, 15.8f);
            UnitSpec spec = units[i];
            WeaponType weapon = forceWeapon ? forcedWeapon : spec.Weapon;
            SpawnAI(manager, team, new Vector3(x, 0.05f, z), healthScale, spec.Tier, weapon, spec.Archetype);
        }
    }

    private void SpawnAI(BattleManager manager, Team team, Vector3 position, float healthScale,
        UnitType unitType, WeaponType weapon, Archetype archetype)
    {
        GameObject go = new GameObject(team == Team.Allies ? "Allied Soldier" : "Enemy Soldier");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        if (team == Team.Enemies)
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        AIFighter fighter = go.AddComponent<AIFighter>();
        fighter.Configure(manager, team, false, healthScale, unitType, weapon, archetype);
        fighter.SetProfile(ArchetypeCatalog.Profile(archetype));
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
        camera.clearFlags = CameraClearFlags.Skybox; // show the per-region procedural sky
        go.AddComponent<AudioListener>();
        return camera;
    }

    private void SetupLighting(ArenaType arena)
    {
        GameObject sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(battleRoot.transform);
        sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = true;
        ApplySunAndSky(timeOfDay, arena);
    }

    // Drives sun angle/color/intensity, ambient, fog, and skybox from a 0..1 time
    // of day so a battle looks like the hour the player arrived.
    private void ApplySunAndSky(float t, ArenaType arena)
    {
        ArenaThemeDefinition theme = presentation?.Theme(arena);
        float daySin = Mathf.Sin(t * Mathf.PI * 2f - Mathf.PI * 0.5f); // -1 night, 0 dawn/dusk, +1 midday
        float day = Mathf.Clamp01(daySin * 0.5f + 0.5f);               // 0 night .. 1 midday
        float golden = 1f - Mathf.Abs(daySin);                         // 1 at dawn/dusk

        sun.transform.rotation = Quaternion.Euler(daySin * 55f + 6f, -28f + (t - 0.5f) * 50f, 0f);
        Color sunBase = theme != null ? theme.sunlight : arena == ArenaType.Marsh ? new Color(0.72f, 0.82f, 0.84f)
            : arena == ArenaType.Forest ? new Color(0.84f, 0.9f, 0.7f) : new Color(1f, 0.78f, 0.58f);
        sun.color = Color.Lerp(new Color(1f, 0.96f, 0.9f), new Color(1f, 0.55f, 0.3f), golden) * sunBase;
        float intensityBase = arena == ArenaType.Forest ? 0.82f : arena == ArenaType.Marsh ? 0.92f : 1.05f;
        sun.intensity = intensityBase * Mathf.Lerp(0.06f, 1f, day);

        Color ambientBase = theme != null ? theme.ambient : arena == ArenaType.Forest ? new Color(0.18f, 0.28f, 0.2f)
            : arena == ArenaType.Marsh ? new Color(0.28f, 0.34f, 0.36f) : new Color(0.28f, 0.32f, 0.36f);
        RenderSettings.ambientLight = ambientBase * Mathf.Lerp(0.35f, 1f, day);
        Color fogBase = theme != null ? theme.fog : arena == ArenaType.Marsh ? new Color(0.42f, 0.52f, 0.52f)
            : arena == ArenaType.Forest ? new Color(0.3f, 0.42f, 0.32f) : new Color(0.48f, 0.55f, 0.58f);
        RenderSettings.fogColor = Color.Lerp(new Color(0.05f, 0.06f, 0.1f), fogBase, day);
        RenderSettings.fogDensity = (theme != null ? theme.fogDensity : arena == ArenaType.Marsh ? 0.02f : 0.012f) * Mathf.Lerp(1.4f, 1f, day);

        Material skybox = RuntimeAssets.Skybox(arena);
        if (skybox != null)
        {
            skybox.SetFloat("_Exposure", Mathf.Lerp(0.28f, 1.1f, day));
            RenderSettings.skybox = skybox;
        }
        DynamicGI.UpdateEnvironment();
    }

    // Torch/campfire brightness for the current time of day: barely lit at midday,
    // strong at night.
    private float FireIntensity(float midday, float night)
    {
        float day = Mathf.Clamp01(Mathf.Sin(timeOfDay * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 0.5f);
        return Mathf.Lerp(night, midday, day);
    }

    private void BuildArena(ArenaType arena, BattleKind kind)
    {
        ArenaThemeDefinition theme = presentation?.Theme(arena);
        if (theme != null && theme.visualPrefab != null)
        {
            GameObject visuals = Instantiate(theme.visualPrefab, battleRoot.transform);
            visuals.name = $"{arena} Authored Visuals";
        }

        BuildBiome(arena);
        switch (kind)
        {
            case BattleKind.SettlementAssault:
                BuildHold(arena);
                break;
            case BattleKind.BanditField:
                BuildBanditCamp(arena);
                break;
            default:
                BuildTrainingDressing();
                break;
        }
    }

    // Shared terrain for every encounter kind: ground, biome scatter, and a
    // containment boundary. Structures (hold / camp / training props) layer on top.
    private void BuildBiome(ArenaType arena)
    {
        if (arena == ArenaType.Forest)
            BuildForest();
        else if (arena == ArenaType.Marsh)
            BuildMarsh();
        else if (arena == ArenaType.Highlands)
            BuildHighlands();
        else
            BuildCourtyardBiome();
    }

    private void BuildCourtyardBiome()
    {
        CreateBlock("Ground", new Vector3(0f, -0.35f, 0f), new Vector3(34f, 0.7f, 34f), new Color(0.24f, 0.34f, 0.16f));
        CreateBlock("Dirt Road", new Vector3(0f, 0.015f, 0f), new Vector3(8f, 0.035f, 31f), new Color(0.34f, 0.24f, 0.13f), false);
        CreateBlock("Cross Road", new Vector3(0f, 0.02f, 0f), new Vector3(28f, 0.04f, 5f), new Color(0.31f, 0.22f, 0.12f), false);
        BuildBoundary(new Color(0.2f, 0.26f, 0.14f), 1.4f);
    }

    // Assault dressing: a fortified hold on the defender (+z) side — ramparts,
    // gate, corner towers (using the otherwise-unused villageTowerRoof) and banners.
    private void BuildHold(ArenaType arena)
    {
        Color stone = new Color(0.32f, 0.33f, 0.31f);
        Color wood = new Color(0.28f, 0.14f, 0.055f);
        CreateBlock("Hold North Wall", new Vector3(0f, 1.7f, 16.6f), new Vector3(36f, 3.4f, 1f), stone);
        CreateBlock("Hold East Wall", new Vector3(16.6f, 1.7f, 0f), new Vector3(1f, 3.4f, 36f), stone);
        CreateBlock("Hold West Wall", new Vector3(-16.6f, 1.7f, 0f), new Vector3(1f, 3.4f, 36f), stone);
        for (int i = -14; i <= 14; i += 4)
            CreateBlock("Battlement", new Vector3(i, 3.65f, 16.4f), new Vector3(2f, 1.1f, 1.3f), stone);

        AuthoredVisual(presentation?.villageArch, "Hold Gate", new Vector3(0f, 0f, 16.0f),
            new Vector3(3f, 1.35f, 1.5f), new Vector3(0f, 180f, 0f));
        Vector3 wallScale = new(3.05f, 1.1f, 1.5f);
        for (int x = -15; x <= 15; x += 6)
            AuthoredVisual(presentation?.villageWall, "Hold Wall", new Vector3(x, 0f, 16.25f), wallScale, new Vector3(0f, 180f, 0f));
        BuildTower(new Vector3(-15.5f, 0f, 16f));
        BuildTower(new Vector3(15.5f, 0f, 16f));

        for (int x = -12; x <= 12; x += 8)
        {
            CreateBlock("Banner Pole", new Vector3(x, 2f, 15.9f), new Vector3(0.12f, 3.8f, 0.12f), wood, false);
            AuthoredVisual(presentation?.banner, "Defender Banner", new Vector3(x, 1.15f, 15.7f),
                Vector3.one * 1.1f, new Vector3(0f, 180f, 0f), new Color(0.68f, 0.08f, 0.05f));
            CreateBlock("Defender Banner", new Vector3(x + 0.7f, 3.15f, 16.1f), new Vector3(1.35f, 1.25f, 0.08f), new Color(0.68f, 0.08f, 0.05f), false);
        }
        BuildTorch(new Vector3(-6f, 1.35f, 15.6f));
        BuildTorch(new Vector3(6f, 1.35f, 15.6f));
    }

    private void BuildTower(Vector3 basePos)
    {
        Color stone = new Color(0.34f, 0.35f, 0.33f);
        CreatePrimitive("Tower Body", PrimitiveType.Cylinder, basePos + Vector3.up * 2.4f, new Vector3(2.4f, 2.4f, 2.4f), stone, false);
        if (presentation?.villageTowerRoof != null)
            AuthoredVisual(presentation.villageTowerRoof, "Tower Roof", basePos + Vector3.up * 4.8f, Vector3.one * 1.6f, Vector3.zero);
        else
            CreatePrimitive("Tower Roof", PrimitiveType.Cylinder, basePos + Vector3.up * 5.2f, new Vector3(2.7f, 0.9f, 2.7f), new Color(0.4f, 0.22f, 0.1f), false);
        BuildTorch(basePos + new Vector3(0f, 3.2f, -1.2f));
    }

    // Field dressing: an open bandit camp behind the enemy (+z) line. No fortress
    // walls — the biome boundary is the only enclosure, so it reads as an ambush.
    private void BuildBanditCamp(ArenaType arena)
    {
        BuildCampfire(new Vector3(0f, 0f, 14.5f));
        BuildTent(new Vector3(-4.5f, 0f, 15f), 18f);
        BuildTent(new Vector3(4.8f, 0f, 14.2f), -22f);
        BuildBedroll(new Vector3(-2f, 0f, 13.4f));
        BuildBedroll(new Vector3(2.4f, 0f, 13.2f));
        AuthoredVisual(presentation?.villageWagon, "Camp Wagon", new Vector3(-9f, 0f, 15f), Vector3.one * 1.1f, new Vector3(0f, 40f, 0f));
        AuthoredVisual(presentation?.villageWagon, "Camp Wagon", new Vector3(9.5f, 0f, 14.5f), Vector3.one * 1.1f, new Vector3(0f, -35f, 0f));
        AuthoredVisual(presentation?.barrel, "Camp Barrel", new Vector3(-6.5f, 0f, 15.8f), Vector3.one, Vector3.zero);
        AuthoredVisual(presentation?.campFence, "Camp Palisade", new Vector3(-7.5f, 0f, 13.4f), Vector3.one * 1.4f, new Vector3(0f, 70f, 0f));
        AuthoredVisual(presentation?.campFence, "Camp Palisade", new Vector3(7.5f, 0f, 13.4f), Vector3.one * 1.4f, new Vector3(0f, -70f, 0f));
        for (int i = 0; i < 5; i++)
        {
            Vector3 pos = new(Random.Range(-8f, 8f), 0.32f, Random.Range(StructureMinZ, 16f));
            CreateBlock("Camp Crate", pos, new Vector3(0.8f, 0.6f, 0.8f), new Color(0.3f, 0.18f, 0.08f));
            AuthoredVisual(presentation?.villageCrate, "Camp Supplies", pos + Vector3.up * 0.34f, Vector3.one * 0.75f, new Vector3(0f, i * 30f, 0f));
        }
    }

    private void BuildCampfire(Vector3 pos)
    {
        if (presentation?.campfire != null)
        {
            AuthoredVisual(presentation.campfire, "Campfire", pos, Vector3.one * 1.4f, Vector3.zero, new Color(0.32f, 0.22f, 0.13f));
        }
        else
        {
            CreatePrimitive("Fire Pit", PrimitiveType.Cylinder, pos + Vector3.up * 0.08f, new Vector3(1.4f, 0.08f, 1.4f), new Color(0.18f, 0.16f, 0.14f), false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                CreateProp("Firewood", pos + new Vector3(Mathf.Cos(a) * 0.4f, 0.18f, Mathf.Sin(a) * 0.4f),
                    new Vector3(0.9f, 0.16f, 0.16f), new Vector3(0f, i * 45f, 0f), new Color(0.22f, 0.12f, 0.05f));
            }
        }
        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Campfire Flame";
        flame.transform.SetParent(battleRoot.transform);
        flame.transform.position = pos + Vector3.up * 0.45f;
        flame.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
        Destroy(flame.GetComponent<Collider>());
        flame.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(1f, 0.4f, 0.08f), true);
        Light light = flame.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.18f);
        light.range = 9f;
        light.shadows = LightShadows.None;
        light.intensity = FireIntensity(0.6f, 3.2f);
    }

    private void BuildTent(Vector3 pos, float yaw)
    {
        Color canvas = new Color(0.5f, 0.46f, 0.36f);
        if (presentation?.tent != null)
        {
            AuthoredVisual(presentation.tent, "Bandit Tent", pos, Vector3.one * 1.5f, new Vector3(0f, yaw, 0f), canvas);
            return;
        }
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        CreateProp("Bandit Tent", pos + rot * new Vector3(-0.5f, 0.7f, 0f), new Vector3(0.12f, 1.7f, 2.4f), new Vector3(0f, yaw, 30f), canvas);
        CreateProp("Bandit Tent", pos + rot * new Vector3(0.5f, 0.7f, 0f), new Vector3(0.12f, 1.7f, 2.4f), new Vector3(0f, yaw, -30f), canvas);
    }

    private void BuildBedroll(Vector3 pos)
    {
        if (presentation?.bedroll != null)
            AuthoredVisual(presentation.bedroll, "Bedroll", pos, Vector3.one * 1.3f, new Vector3(0f, 90f, 0f), new Color(0.42f, 0.3f, 0.2f));
        else
            CreateBlock("Bedroll", pos + Vector3.up * 0.06f, new Vector3(0.9f, 0.12f, 1.9f), new Color(0.42f, 0.3f, 0.2f), false);
    }

    // Neutral practice-yard dressing (no fortress walls): barricades, cover,
    // torches, and a few props for the consequence-free training arena.
    private void BuildTrainingDressing()
    {
        Color stone = new Color(0.32f, 0.33f, 0.31f);
        CreateBlock("Center Barricade", new Vector3(-7f, 0.65f, 1.8f), new Vector3(4.5f, 1.3f, 0.45f), new Color(0.28f, 0.14f, 0.055f));
        CreateBlock("Center Barricade", new Vector3(7f, 0.65f, -1.8f), new Vector3(4.5f, 1.3f, 0.45f), new Color(0.28f, 0.14f, 0.055f));
        CreateBlock("Stone Cover", new Vector3(-10f, 0.65f, 5f), new Vector3(2.5f, 1.3f, 2.5f), stone);
        CreateBlock("Stone Cover", new Vector3(10f, 0.65f, -5f), new Vector3(2.5f, 1.3f, 2.5f), stone);
        BuildTorch(new Vector3(-6f, 1.35f, -15.6f));
        BuildTorch(new Vector3(6f, 1.35f, -15.6f));
        BuildTorch(new Vector3(-6f, 1.35f, 15.6f));
        BuildTorch(new Vector3(6f, 1.35f, 15.6f));
        AuthoredVisual(presentation?.weaponStand, "Weapon Stand", new Vector3(11.8f, 0f, 5.2f), Vector3.one * 1.15f, new Vector3(0f, -65f, 0f));
        AuthoredVisual(presentation?.barrel, "Barrel", new Vector3(13.2f, 0f, 6.3f), Vector3.one * 1.1f, Vector3.zero);
        AuthoredVisual(presentation?.barrel, "Barrel", new Vector3(-13.2f, 0f, -7.1f), Vector3.one * 1.1f, Vector3.zero);
        for (int z = -12; z <= 12; z += 6)
        {
            AuthoredVisual(presentation?.villageFence, "Fence", new Vector3(-15f, 0f, z), Vector3.one, new Vector3(0f, 90f, 0f));
            AuthoredVisual(presentation?.villageFence, "Fence", new Vector3(15f, 0f, z), Vector3.one, new Vector3(0f, 90f, 0f));
        }
    }

    private void CreateProp(string objectName, Vector3 position, Vector3 scale, Vector3 euler, Color color)
    {
        GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prop.name = objectName;
        prop.transform.SetParent(battleRoot.transform);
        prop.transform.position = position;
        prop.transform.localScale = scale;
        prop.transform.eulerAngles = euler;
        prop.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        Destroy(prop.GetComponent<Collider>());
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
        for (int i = 0; i < 16; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * Random.Range(13f, 15.8f);
            float z = Random.Range(-14f, 14f);
            CreateBlock("Tree Trunk", new Vector3(x, 1.25f, z), new Vector3(0.48f, 2.45f, 0.48f), bark);
            CreatePrimitive("Tree Crown", PrimitiveType.Sphere, new Vector3(x, 2.85f, z), new Vector3(1.45f, 1.65f, 1.45f), leaves, false);
            GameObject tree = i % 3 == 0 ? presentation?.pineTree : presentation?.commonTree;
            AuthoredVisual(tree, "Authored Forest Tree", new Vector3(x, 0f, z), Vector3.one * Random.Range(0.28f, 0.45f),
                new Vector3(0f, Random.Range(0f, 360f), 0f));
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
        AuthoredVisual(presentation?.villageWagon, "Marsh Wrecked Wagon", new Vector3(-5f, 0f, 1f), Vector3.one * 1.15f,
            new Vector3(0f, 25f, 12f));
        for (int i = 0; i < 7; i++)
            AuthoredVisual(i % 2 == 0 ? presentation?.deadTree : presentation?.bush, "Marsh Landmark",
                new Vector3((i % 2 == 0 ? -1f : 1f) * Random.Range(9f, 14f), 0f, Random.Range(-14f, 14f)),
                Vector3.one * Random.Range(0.9f, 1.4f), new Vector3(0f, Random.Range(0f, 360f), 0f));
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
        {
            Vector3 position = new(Random.Range(-8f, 8f), 0.55f, Random.Range(-14f, 14f));
            Vector3 scale = new(Random.Range(1f, 2f), Random.Range(0.8f, 1.5f), Random.Range(1f, 2f));
            CreateBlock("Boulder", position, scale, rock);
            AuthoredVisual(presentation?.rock, "Authored Boulder", position + Vector3.up * 0.2f, scale,
                new Vector3(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)));
        }
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
        light.intensity = FireIntensity(0.4f, 2.4f);
        light.range = 6f;
        light.shadows = LightShadows.None;
    }

    private void CreateBlock(string blockName, Vector3 position, Vector3 scale, Color color, bool collider = true,
        bool visible = true)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = blockName;
        block.transform.SetParent(battleRoot.transform);
        block.transform.position = position;
        block.transform.localScale = scale;
        Renderer renderer = block.GetComponent<Renderer>();
        renderer.sharedMaterial = RuntimeAssets.Material(color);
        renderer.enabled = visible;
        if (!collider)
            Destroy(block.GetComponent<Collider>());
    }

    private void AuthoredVisual(GameObject prefab, string objectName, Vector3 position, Vector3 scale, Vector3 rotation,
        Color? tintOverride = null)
    {
        if (prefab == null)
            return;
        GameObject visual = Instantiate(prefab, battleRoot.transform);
        visual.name = objectName;
        visual.transform.position = position;
        visual.transform.localScale = prefab == presentation?.rock ? scale * 0.24f : scale;
        visual.transform.eulerAngles = rotation;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
            Destroy(collider);
        Color tint = tintOverride ?? (prefab == presentation?.commonTree || prefab == presentation?.pineTree || prefab == presentation?.bush
            ? new Color(0.2f, 0.46f, 0.16f)
            : prefab == presentation?.deadTree ? new Color(0.36f, 0.23f, 0.12f)
            : prefab == presentation?.rock ? new Color(0.42f, 0.43f, 0.42f)
            : new Color(0.5f, 0.38f, 0.24f));
        TintVisual(visual, tint);
    }

    private static void TintVisual(GameObject visual, Color tint)
    {
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = RuntimeAssets.Material(tint);
    }

}
