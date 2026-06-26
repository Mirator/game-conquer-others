using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds a complete runtime battle (arena, lighting, camera, fighters) under a
// root GameObject supplied by the GameDirector. No longer the app entry point;
// the director owns lifecycle and mode switching.
public sealed class BattleBootstrap : MonoBehaviour
{
    // Terrain extends well past the playable footprint so the rolling rim and the
    // distant silhouette ring sit on continuous ground, not floating over a void.
    private const float TerrainWorldSize = ArenaMetrics.GroundSize + 96f;
    // Keep scatter off the central road/causeway/track so the clash lane stays clear.
    private const float PathClearHalfWidth = 3.75f;

    private enum Weather
    {
        Clear,
        Rain,
        Snow,
        Mist
    }

    private GameObject battleRoot;
    private PresentationCatalog presentation;
    private float timeOfDay = 0.5f;
    private Light sun;
    private BattleEffects effects;
    private WindSway windSway;
    private Weather weather = Weather.Clear;

    public BattleManager Build(GameObject root, BattleSetup setup)
    {
        battleRoot = root;
        presentation = PresentationCatalog.Load();
        timeOfDay = setup.TimeOfDay;
        weather = ChooseWeather(setup.Arena, timeOfDay);

        BattleManager manager = battleRoot.AddComponent<BattleManager>();
        effects = battleRoot.AddComponent<BattleEffects>();
        effects.Initialize(setup.Arena);

        SetupLighting(setup.Arena);
        BuildArena(setup.Arena, setup.Kind);
        PlaceAmbience(setup.Arena);
        ApplyWeather();

        Camera camera = CreateCamera();
        BattlePostProcessing.Apply(camera, battleRoot.transform);
        BuildSky(camera);
        BuildAmbientParticles();
        ThirdPersonCamera cameraRig = camera.gameObject.AddComponent<ThirdPersonCamera>();
        manager.Configure(effects, cameraRig, setup.IsTraining);
        manager.SetDecals(battleRoot.AddComponent<BattleDecals>());
        battleRoot.AddComponent<PerformanceHud>().Configure(manager); // F3 toggles the perf overlay
        PlayerFighter player = SpawnPlayer(manager, new Vector3(0f, 0.05f, ArenaMetrics.PlayerSpawnZ), setup.PlayerWeapon);
        player.SetCamera(cameraRig);
        cameraRig.SetTarget(player.transform);
        // Establishing flyover over the lines before the fight starts (skip on reduced motion).
        bool reduceMotion = SettingsService.Current != null && SettingsService.Current.reduceMotion;
        if (!reduceMotion)
            cameraRig.PlaySweep(new Vector3(ArenaMetrics.HalfWidth * 0.7f, 11f, ArenaMetrics.EnemySpawnZ + 5f),
                new Vector3(0f, 1.5f, 0f), 2.6f);

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
        float baseZ = team == Team.Allies ? ArenaMetrics.AllySpawnZ : ArenaMetrics.EnemySpawnZ;
        float rowStep = team == Team.Allies ? -1.35f : 1.35f;

        for (int i = 0; i < count; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int rowCount = Mathf.Min(perRow, count - row * perRow);
            float x = Mathf.Clamp((col - (rowCount - 1) * 0.5f) * spacing, -ArenaMetrics.HalfWidth, ArenaMetrics.HalfWidth);
            float z = Mathf.Clamp(baseZ + row * rowStep, -ArenaMetrics.SpawnSafeZ, ArenaMetrics.SpawnSafeZ);
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
        camera.farClipPlane = 260f;
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
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        QualitySettings.shadowDistance = GraphicsQuality.ShadowDistance;

        // A cool, shadowless fill opposite the sun keeps silhouettes readable across
        // the larger field; dropped on the low tier to save a directional pass.
        if (!GraphicsQuality.IsLow)
        {
            GameObject fillObject = new GameObject("Fill Light");
            fillObject.transform.SetParent(battleRoot.transform);
            fillObject.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.6f, 0.7f, 0.85f);
            fill.intensity = 0.18f;
            fill.shadows = LightShadows.None;
        }

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
        Color litColor = Color.Lerp(new Color(1f, 0.96f, 0.9f), new Color(1f, 0.55f, 0.3f), golden) * sunBase;
        // Cool moonlight at night instead of a dim warm sun, with a higher floor so
        // fighters stay readable after dark.
        sun.color = Color.Lerp(new Color(0.55f, 0.62f, 0.85f), litColor, day);
        float intensityBase = arena == ArenaType.Forest ? 0.82f : arena == ArenaType.Marsh ? 0.92f : 1.05f;
        sun.intensity = intensityBase * Mathf.Lerp(0.22f, 1f, day);

        Color ambientBase = theme != null ? theme.ambient : arena == ArenaType.Forest ? new Color(0.18f, 0.28f, 0.2f)
            : arena == ArenaType.Marsh ? new Color(0.28f, 0.34f, 0.36f) : new Color(0.28f, 0.32f, 0.36f);
        Color ambient = ambientBase * Mathf.Lerp(0.6f, 1f, day);
        // Trilight gradient: brighter, cooler sky bounce down to a darker ground bounce
        // gives the from-primitives geometry natural shape without baked probes.
        RenderSettings.ambientSkyColor = ambient * 1.15f + new Color(0.02f, 0.03f, 0.05f);
        RenderSettings.ambientEquatorColor = ambient;
        RenderSettings.ambientGroundColor = ambient * 0.5f;
        Color fogBase = theme != null ? theme.fog : arena == ArenaType.Marsh ? new Color(0.42f, 0.52f, 0.52f)
            : arena == ArenaType.Forest ? new Color(0.3f, 0.42f, 0.32f) : new Color(0.48f, 0.55f, 0.58f);
        // Cool blue night fog (not black/grey) so colour and depth survive after dark.
        RenderSettings.fogColor = Color.Lerp(new Color(0.09f, 0.12f, 0.2f), fogBase, day);
        // Thinner overall, and only mildly denser at night, so the field doesn't wash
        // out to a milky grey while the distant tree-line still fades into the sky.
        RenderSettings.fogDensity = (theme != null ? theme.fogDensity : arena == ArenaType.Marsh ? 0.02f : 0.012f)
            * 0.65f * Mathf.Lerp(1.15f, 1f, day);

        // Weather overcasts the sky: thicker, greyer fog and a dimmer sun.
        if (weather == Weather.Rain)
        {
            sun.intensity *= 0.82f;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.5f, 0.53f, 0.57f), 0.5f);
            RenderSettings.fogDensity *= 1.35f;
        }
        else if (weather == Weather.Mist)
        {
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.72f, 0.76f, 0.78f), 0.4f);
            RenderSettings.fogDensity *= 1.4f;
        }
        else if (weather == Weather.Snow)
        {
            sun.intensity *= 0.9f;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.82f, 0.85f, 0.9f), 0.45f);
            RenderSettings.fogDensity *= 1.2f;
        }

        Material skybox = RuntimeAssets.Skybox(arena);
        if (skybox != null)
        {
            // Higher night-exposure floor + thicker atmosphere lift the horizon out of
            // pure black into a scattered dark-blue night sky.
            skybox.SetFloat("_Exposure", Mathf.Lerp(0.55f, 1.15f, day));
            skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.6f, 1f, day));
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
        BuildDistantRing(arena);
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

    // A ring of dark, distant trees (or crags in the highlands) well beyond the
    // boundary, sitting on the rolling terrain rim and fading into the fog — the
    // silhouette horizon that makes the open field feel like a vista. Tinted dark and
    // collider-free; density scales with the quality tier.
    private void BuildDistantRing(ArenaType arena)
    {
        int count = Mathf.RoundToInt(44 * GraphicsQuality.ScatterScale);
        Color dark = arena switch
        {
            ArenaType.Forest => new Color(0.10f, 0.16f, 0.12f),
            ArenaType.Marsh => new Color(0.14f, 0.17f, 0.18f),
            ArenaType.Highlands => new Color(0.16f, 0.16f, 0.18f),
            _ => new Color(0.12f, 0.17f, 0.13f)
        };
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f + Random.Range(-0.06f, 0.06f);
            float dist = Random.Range(ArenaMetrics.WallOffset + 16f, ArenaMetrics.WallOffset + 44f);
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = GroundHeightAt(x, z);
            if (arena == ArenaType.Highlands)
                AuthoredVisual(presentation?.RandomRock(), "Distant Crag", new Vector3(x, y, z),
                    Vector3.one * Random.Range(3f, 7f),
                    new Vector3(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)), dark, false);
            else
            {
                GameObject tree = Random.value < 0.5f ? presentation?.RandomPine() : presentation?.RandomTree();
                AuthoredVisual(tree, "Distant Tree", new Vector3(x, y, z), Vector3.one * Random.Range(0.6f, 1.1f),
                    new Vector3(0f, Random.Range(0f, 360f), 0f), dark, false);
            }
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
        float g = ArenaMetrics.GroundSize;
        CreateGround("Ground", new Color(0.24f, 0.34f, 0.16f), new Color(0.18f, 0.26f, 0.11f));
        CreateBlock("Dirt Road", new Vector3(0f, 0.015f, 0f), new Vector3(9f, 0.035f, g - 4f), new Color(0.34f, 0.24f, 0.13f), false);
        CreateBlock("Cross Road", new Vector3(0f, 0.02f, 0f), new Vector3(g - 8f, 0.04f, 6f), new Color(0.31f, 0.22f, 0.12f), false);
        BuildBoundary(new Color(0.2f, 0.26f, 0.14f), 1.4f);
        ScatterBorderTrees(26);
        ScatterClutter(60);
        ScatterGrass(1100);
    }

    // Assault dressing: a fortified hold on the defender (+z) side — ramparts,
    // gate, corner towers (using the otherwise-unused villageTowerRoof) and banners.
    private void BuildHold(ArenaType arena)
    {
        Color stone = new Color(0.32f, 0.33f, 0.31f);
        Color wood = new Color(0.28f, 0.14f, 0.055f);
        float wz = ArenaMetrics.HoldWallOffset;
        float span = ArenaMetrics.GroundSize + 2f;
        CreateBlock("Hold North Wall", new Vector3(0f, 1.7f, wz), new Vector3(span, 3.4f, 1f), stone);
        CreateBlock("Hold East Wall", new Vector3(wz, 1.7f, 0f), new Vector3(1f, 3.4f, span), stone);
        CreateBlock("Hold West Wall", new Vector3(-wz, 1.7f, 0f), new Vector3(1f, 3.4f, span), stone);
        for (int i = -30; i <= 30; i += 5)
            CreateBlock("Battlement", new Vector3(i, 3.65f, wz - 0.2f), new Vector3(2f, 1.1f, 1.3f), stone);

        AuthoredVisual(presentation?.villageArch, "Hold Gate", new Vector3(0f, 0f, wz - 0.6f),
            new Vector3(3f, 1.35f, 1.5f), new Vector3(0f, 180f, 0f));
        Vector3 wallScale = new(3.05f, 1.1f, 1.5f);
        for (int x = -30; x <= 30; x += 6)
            AuthoredVisual(presentation?.villageWall, "Hold Wall", new Vector3(x, 0f, wz - 0.35f), wallScale, new Vector3(0f, 180f, 0f));
        BuildTower(new Vector3(-wz, 0f, wz));
        BuildTower(new Vector3(wz, 0f, wz));

        for (int x = -20; x <= 20; x += 10)
        {
            CreateBlock("Banner Pole", new Vector3(x, 2f, wz - 0.7f), new Vector3(0.12f, 3.8f, 0.12f), wood, false);
            AuthoredVisual(presentation?.banner, "Defender Banner", new Vector3(x, 1.15f, wz - 0.9f),
                Vector3.one * 1.1f, new Vector3(0f, 180f, 0f), new Color(0.68f, 0.08f, 0.05f));
            CreateBlock("Defender Banner", new Vector3(x + 0.7f, 3.15f, wz - 0.5f), new Vector3(1.35f, 1.25f, 0.08f), new Color(0.68f, 0.08f, 0.05f), false);
        }
        BuildTorch(new Vector3(-10f, 1.35f, wz - 1f));
        BuildTorch(new Vector3(10f, 1.35f, wz - 1f));
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
        BuildCampfire(new Vector3(0f, 0f, 26f));
        BuildTent(new Vector3(-5.5f, 0f, 26.5f), 18f);
        BuildTent(new Vector3(5.8f, 0f, 25.8f), -22f);
        BuildBedroll(new Vector3(-2.4f, 0f, 24.8f));
        BuildBedroll(new Vector3(2.8f, 0f, 24.6f));
        AuthoredVisual(presentation?.villageWagon, "Camp Wagon", new Vector3(-13f, 0f, 26f), Vector3.one * 1.1f, new Vector3(0f, 40f, 0f));
        AuthoredVisual(presentation?.villageWagon, "Camp Wagon", new Vector3(13.5f, 0f, 25.5f), Vector3.one * 1.1f, new Vector3(0f, -35f, 0f));
        AuthoredVisual(presentation?.barrel, "Camp Barrel", new Vector3(-8.5f, 0f, 27f), Vector3.one, Vector3.zero);
        AuthoredVisual(presentation?.campFence, "Camp Palisade", new Vector3(-10.5f, 0f, 24.6f), Vector3.one * 1.4f, new Vector3(0f, 70f, 0f));
        AuthoredVisual(presentation?.campFence, "Camp Palisade", new Vector3(10.5f, 0f, 24.6f), Vector3.one * 1.4f, new Vector3(0f, -70f, 0f));
        for (int i = 0; i < 5; i++)
        {
            Vector3 pos = new(Random.Range(-12f, 12f), 0.32f, Random.Range(ArenaMetrics.StructureMinZ, 30f));
            CreateBlock("Camp Crate", pos, new Vector3(0.8f, 0.6f, 0.8f), new Color(0.3f, 0.18f, 0.08f));
            AuthoredVisual(presentation?.villageCrate, "Camp Supplies", pos + Vector3.up * 0.34f, Vector3.one * 0.75f, new Vector3(0f, i * 30f, 0f));
        }
    }

    private void BuildCampfire(Vector3 pos)
    {
        if (presentation?.campfire != null)
        {
            AuthoredVisual(presentation.campfire, "Campfire", pos, Vector3.one * 1.4f, Vector3.zero);
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
            AuthoredVisual(presentation.tent, "Bandit Tent", pos, Vector3.one * 1.5f, new Vector3(0f, yaw, 0f));
            return;
        }
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        CreateProp("Bandit Tent", pos + rot * new Vector3(-0.5f, 0.7f, 0f), new Vector3(0.12f, 1.7f, 2.4f), new Vector3(0f, yaw, 30f), canvas);
        CreateProp("Bandit Tent", pos + rot * new Vector3(0.5f, 0.7f, 0f), new Vector3(0.12f, 1.7f, 2.4f), new Vector3(0f, yaw, -30f), canvas);
    }

    private void BuildBedroll(Vector3 pos)
    {
        if (presentation?.bedroll != null)
            AuthoredVisual(presentation.bedroll, "Bedroll", pos, Vector3.one * 1.3f, new Vector3(0f, 90f, 0f));
        else
            CreateBlock("Bedroll", pos + Vector3.up * 0.06f, new Vector3(0.9f, 0.12f, 1.9f), new Color(0.42f, 0.3f, 0.2f), false);
    }

    // Neutral practice-yard dressing (no fortress walls): barricades, cover,
    // torches, and a few props for the consequence-free training arena.
    private void BuildTrainingDressing()
    {
        Color stone = new Color(0.32f, 0.33f, 0.31f);
        CreateBlock("Center Barricade", new Vector3(-10f, 0.65f, 3f), new Vector3(4.5f, 1.3f, 0.45f), new Color(0.28f, 0.14f, 0.055f));
        CreateBlock("Center Barricade", new Vector3(10f, 0.65f, -3f), new Vector3(4.5f, 1.3f, 0.45f), new Color(0.28f, 0.14f, 0.055f));
        CreateBlock("Stone Cover", new Vector3(-18f, 0.65f, 9f), new Vector3(2.5f, 1.3f, 2.5f), stone);
        CreateBlock("Stone Cover", new Vector3(18f, 0.65f, -9f), new Vector3(2.5f, 1.3f, 2.5f), stone);
        BuildTorch(new Vector3(-10f, 1.35f, -29f));
        BuildTorch(new Vector3(10f, 1.35f, -29f));
        BuildTorch(new Vector3(-10f, 1.35f, 29f));
        BuildTorch(new Vector3(10f, 1.35f, 29f));
        AuthoredVisual(presentation?.weaponStand, "Weapon Stand", new Vector3(22f, 0f, 9f), Vector3.one * 1.15f, new Vector3(0f, -65f, 0f));
        AuthoredVisual(presentation?.barrel, "Barrel", new Vector3(24f, 0f, 11f), Vector3.one * 1.1f, Vector3.zero);
        AuthoredVisual(presentation?.barrel, "Barrel", new Vector3(-24f, 0f, -12f), Vector3.one * 1.1f, Vector3.zero);
        for (int z = -24; z <= 24; z += 6)
        {
            AuthoredVisual(presentation?.villageFence, "Fence", new Vector3(-26f, 0f, z), Vector3.one, new Vector3(0f, 90f, 0f));
            AuthoredVisual(presentation?.villageFence, "Fence", new Vector3(26f, 0f, z), Vector3.one, new Vector3(0f, 90f, 0f));
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

    // The biome ground: an undulating terrain mesh (flat across the playable core,
    // rolling into hills toward the rim and horizon) surfaced with a tiling, runtime-
    // generated noise texture + normal map. A MeshCollider lets fighters walk it.
    private void CreateGround(string objectName, Color baseColor, Color speckle)
    {
        GameObject ground = new GameObject(objectName);
        ground.transform.SetParent(battleRoot.transform);
        Mesh mesh = BuildTerrainMesh();
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial =
            RuntimeAssets.GroundMaterial(baseColor, speckle, TerrainWorldSize / 4f, weather == Weather.Rain);
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static Mesh BuildTerrainMesh()
    {
        const int seg = 96;
        int vpr = seg + 1;
        Vector3[] vertices = new Vector3[vpr * vpr];
        Vector2[] uvs = new Vector2[vpr * vpr];
        for (int z = 0; z < vpr; z++)
        {
            for (int x = 0; x < vpr; x++)
            {
                float fx = (x / (float)seg - 0.5f) * TerrainWorldSize;
                float fz = (z / (float)seg - 0.5f) * TerrainWorldSize;
                int i = z * vpr + x;
                vertices[i] = new Vector3(fx, TerrainHeight(fx, fz), fz);
                uvs[i] = new Vector2(x / (float)seg, z / (float)seg);
            }
        }
        int[] triangles = new int[seg * seg * 6];
        int t = 0;
        for (int z = 0; z < seg; z++)
        {
            for (int x = 0; x < seg; x++)
            {
                int i = z * vpr + x;
                triangles[t++] = i;
                triangles[t++] = i + vpr;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + vpr;
                triangles[t++] = i + vpr + 1;
            }
        }
        Mesh mesh = new Mesh { name = "Terrain" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Height field for the ground: flat (≈0) across the playable footprint so combat,
    // spawns, and formations are unaffected, ramping into rolling hills beyond it.
    private static float TerrainHeight(float x, float z)
    {
        float r = Mathf.Max(Mathf.Abs(x) / ArenaMetrics.HalfWidth, Mathf.Abs(z) / ArenaMetrics.HalfDepth);
        float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 1.9f, r));
        float n = (Mathf.PerlinNoise(x * 0.045f + 100f, z * 0.045f + 100f) - 0.5f)
            + (Mathf.PerlinNoise(x * 0.12f + 40f, z * 0.12f + 40f) - 0.5f) * 0.4f;
        return n * outside * 7f;
    }

    private static float GroundHeightAt(float x, float z) => TerrainHeight(x, z);

    // Positional ambient emitters give the field spatial life — birdsong in wooded
    // arenas, a frog/insect chorus near the marsh, wind across the highlands.
    private void PlaceAmbience(ArenaType arena)
    {
        if (effects == null)
            return;
        switch (arena)
        {
            case ArenaType.Forest:
                effects.AddBirdsong(new Vector3(-18f, 4f, 12f));
                effects.AddBirdsong(new Vector3(16f, 4f, -14f));
                effects.AddBirdsong(new Vector3(0f, 5f, 24f));
                break;
            case ArenaType.Marsh:
                effects.AddMarshChorus(new Vector3(-16f, 0.5f, 10f));
                effects.AddMarshChorus(new Vector3(15f, 0.5f, -12f));
                break;
            case ArenaType.Highlands:
                effects.AddWindGust(new Vector3(-24f, 6f, 4f));
                effects.AddWindGust(new Vector3(24f, 6f, -4f));
                break;
            default:
                effects.AddBirdsong(new Vector3(-16f, 4f, 14f));
                effects.AddBirdsong(new Vector3(18f, 4f, -10f));
                break;
        }
    }

    // Deterministic from the day clock (so a retried battle looks identical): the marsh
    // is always misty, the highlands sometimes snow, open arenas sometimes rain.
    private static Weather ChooseWeather(ArenaType arena, float timeOfDay)
    {
        float roll = Mathf.Repeat(Mathf.Abs(Mathf.Sin(timeOfDay * 53.13f)) * 7f, 1f);
        return arena switch
        {
            ArenaType.Marsh => Weather.Mist,
            ArenaType.Highlands => roll < 0.4f ? Weather.Snow : Weather.Clear,
            _ => roll < 0.35f ? Weather.Rain : Weather.Clear
        };
    }

    // Spawns the weather particle layer and its ambience. Fog/sun/wet-ground are handled
    // in ApplySunAndSky and CreateGround from the same `weather` value.
    private void ApplyWeather()
    {
        switch (weather)
        {
            case Weather.Rain:
                BuildPrecipitation("Rain", 1400f, -26f, 0.06f, 1.5f, new Color(0.75f, 0.8f, 0.88f, 0.55f), true);
                effects?.PlayRainAmbience();
                break;
            case Weather.Snow:
                BuildPrecipitation("Snow", 380f, -3.5f, 0.13f, 0.4f, new Color(0.96f, 0.97f, 1f, 0.9f), false);
                break;
            case Weather.Mist:
                BuildMist();
                break;
        }
    }

    // Falling precipitation over the field. Rain is stretched and fast; snow is soft,
    // slow, and drifts on a noise field. Emission scales with the quality tier.
    private void BuildPrecipitation(string name, float rate, float fallSpeed, float size, float drift, Color color, bool streak)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = new Vector3(0f, 24f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = streak ? 1.4f : 7f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 5000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.RoundToInt(rate * GraphicsQuality.ScatterScale);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(ArenaMetrics.GroundSize, 1f, ArenaMetrics.GroundSize);
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(fallSpeed);
        velocity.x = new ParticleSystem.MinMaxCurve(-drift);
        if (!streak)
        {
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.2f;
        }
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = RuntimeAssets.SoftParticleMaterial();
        if (streak)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 6f;
            renderer.velocityScale = 0.08f;
        }
        ps.Play();
    }

    private void BuildMist()
    {
        GameObject go = new GameObject("Mist");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = new Vector3(0f, 1.2f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 10f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startColor = new Color(0.72f, 0.76f, 0.79f, 0.09f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.RoundToInt(10f * GraphicsQuality.ScatterScale);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(ArenaMetrics.GroundSize, 2.5f, ArenaMetrics.GroundSize);
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.4f);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = RuntimeAssets.SoftParticleMaterial();
        ps.Play();
    }

    private float DayFactor()
        => Mathf.Clamp01(Mathf.Sin(timeOfDay * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 0.5f);

    // Sky dressing centred on the camera (via SkyDome): a starfield + glowing moon at
    // night, plus a faint drifting cloud band. Quality-gated.
    private void BuildSky(Camera camera)
    {
        if (GraphicsQuality.IsLow || camera == null)
            return;
        GameObject dome = new GameObject("Sky Dome");
        dome.transform.SetParent(battleRoot.transform);
        dome.AddComponent<SkyDome>().Configure(camera.transform);
        float day = DayFactor();
        if (day < 0.45f)
        {
            BuildStars(dome.transform, day);
            BuildMoon(dome.transform);
        }
        BuildClouds(dome.transform);
    }

    private void BuildStars(Transform parent, float day)
    {
        GameObject go = new GameObject("Stars");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startColor = new Color(0.86f, 0.9f, 1f, Mathf.Lerp(0.85f, 0.35f, day)); // brighter the darker it is
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(450 * GraphicsQuality.ScatterScale)) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 175f;
        shape.radiusThickness = 0f; // a shell, so stars sit at a fixed distance
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = RuntimeAssets.SoftParticleMaterial();
        ps.Play();
    }

    private void BuildMoon(Transform parent)
    {
        GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = "Moon";
        moon.transform.SetParent(parent, false);
        moon.transform.localPosition = new Vector3(55f, 115f, 80f);
        moon.transform.localScale = Vector3.one * 14f;
        Destroy(moon.GetComponent<Collider>());
        moon.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.86f, 0.89f, 0.96f), true);
    }

    private void BuildClouds(Transform parent)
    {
        GameObject go = new GameObject("Clouds");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 48f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startSpeed = 0f;
        main.startLifetime = 40f;
        main.startSize = new ParticleSystem.MinMaxCurve(28f, 52f);
        main.startColor = new Color(0.62f, 0.66f, 0.72f, 0.07f); // very faint so it adds depth, not blobs
        main.maxParticles = 16;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.RoundToInt(0.4f * GraphicsQuality.ScatterScale + 0.1f);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(240f, 1f, 240f);
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(2.5f);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = RuntimeAssets.SoftParticleMaterial();
        ps.Play();
    }

    // Faint life in the air: slow dust motes by day, glowing fireflies near the ground
    // at night. Quality-gated; collider-free, so the simulation is untouched.
    private void BuildAmbientParticles()
    {
        if (GraphicsQuality.IsLow)
            return;
        bool day = DayFactor() > 0.5f;
        GameObject go = new GameObject(day ? "Dust Motes" : "Fireflies");
        go.transform.SetParent(battleRoot.transform);
        go.transform.position = new Vector3(0f, day ? 3f : 1.6f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startSpeed = 0f;
        main.startLifetime = day ? 8f : 6f;
        main.startSize = new ParticleSystem.MinMaxCurve(day ? 0.04f : 0.06f, day ? 0.1f : 0.14f);
        main.startColor = day ? new Color(0.9f, 0.88f, 0.8f, 0.18f) : new Color(0.85f, 1f, 0.35f, 1f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.RoundToInt((day ? 40f : 20f) * GraphicsQuality.ScatterScale);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(ArenaMetrics.GroundSize, day ? 6f : 3f, ArenaMetrics.GroundSize);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = day ? 0.4f : 0.7f;
        noise.frequency = day ? 0.15f : 0.25f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (day)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f);
            renderer.sharedMaterial = RuntimeAssets.SoftParticleMaterial();
        }
        else
        {
            renderer.sharedMaterial = RuntimeAssets.Material(new Color(0.85f, 1f, 0.35f), true); // emissive -> bloom glow
        }
        ps.Play();
    }

    private static List<Vector2> ClusterCenters(int count, float halfWidth, float halfDepth)
    {
        List<Vector2> centers = new();
        for (int i = 0; i < count; i++)
            centers.Add(new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfDepth, halfDepth)));
        return centers;
    }

    // A point scattered within `radius` of a random cluster centre (uniform over the
    // disc), clamped to the field. y is left at 0 for the caller to sample onto terrain.
    private static Vector3 ClusteredPoint(List<Vector2> centers, float radius, float halfWidth, float halfDepth)
    {
        Vector2 c = centers[Random.Range(0, centers.Count)];
        float angle = Random.value * Mathf.PI * 2f;
        float r = radius * Mathf.Sqrt(Random.value);
        return new Vector3(
            Mathf.Clamp(c.x + Mathf.Cos(angle) * r, -halfWidth, halfWidth), 0f,
            Mathf.Clamp(c.y + Mathf.Sin(angle) * r, -halfDepth, halfDepth));
    }

    // A glossy, shallow water plane (uses the tunable PBR material for a wet sheen).
    private void CreateWater(string objectName, Vector3 position, Vector3 scale)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(battleRoot.transform);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.WaterMaterial();
        Destroy(block.GetComponent<Collider>());
    }

    // Sprinkles small nature models (grass tufts, ferns, flowers, pebbles) across the
    // playable field so the larger ground reads as living terrain rather than a flat
    // plane. Count scales with the quality tier; collider-free, so movement is unaffected.
    private void ScatterClutter(int baseCount, bool barren = false)
    {
        if (presentation == null)
            return;
        int count = Mathf.RoundToInt(baseCount * GraphicsQuality.ScatterScale);
        float hw = ArenaMetrics.HalfWidth - 1f;
        float hd = ArenaMetrics.HalfDepth - 1f;
        // Seed clumps so detail grows in patches rather than an even sprinkle.
        List<Vector2> clumps = ClusterCenters(Mathf.Max(4, count / 7), hw, hd);
        for (int i = 0; i < count; i++)
        {
            GameObject model = barren ? presentation.RandomBarrenClutter() : presentation.RandomClutter();
            if (model == null)
                continue;
            Vector3 p = ClusteredPoint(clumps, 3.5f, hw, hd);
            if (Mathf.Abs(p.x) < PathClearHalfWidth)
                continue;
            p.y = GroundHeightAt(p.x, p.z);
            GameObject go = AuthoredVisual(model, "Clutter", p,
                Vector3.one * Random.Range(0.45f, 0.8f), new Vector3(0f, Random.Range(0f, 360f), 0f), null, false);
            AddSway(go, 6f, 1.7f);
        }
    }

    // A dense GPU-instanced grass carpet across the playable field (and a little past
    // it). Static instances, drawn in a few batched calls; density scales with quality.
    private void ScatterGrass(int baseCount)
    {
        int count = Mathf.RoundToInt(baseCount * GraphicsQuality.ScatterScale);
        if (count <= 0)
            return;
        float hw = ArenaMetrics.HalfWidth + 2f;
        float hd = ArenaMetrics.HalfDepth + 2f;
        List<Vector2> clumps = ClusterCenters(Mathf.Max(8, count / 50), hw, hd);
        List<Matrix4x4> instances = new(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 p = ClusteredPoint(clumps, 5f, hw, hd);
            if (Mathf.Abs(p.x) < PathClearHalfWidth)
                continue;
            p.y = GroundHeightAt(p.x, p.z);
            float s = Random.Range(0.7f, 1.3f);
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            instances.Add(Matrix4x4.TRS(p, rot, new Vector3(s, s * Random.Range(0.85f, 1.4f), s)));
        }
        GameObject field = new GameObject("Grass Field");
        field.transform.SetParent(battleRoot.transform);
        field.AddComponent<GrassField>().Build(RuntimeAssets.GrassMesh(), RuntimeAssets.GrassMaterial(), instances);
    }

    // A ring of trees in the band between the playable footprint and the boundary,
    // framing the open vista without obstructing the fight.
    private void ScatterBorderTrees(int baseCount)
    {
        int count = Mathf.RoundToInt(baseCount * GraphicsQuality.ScatterScale);
        for (int i = 0; i < count; i++)
        {
            bool onSide = i % 2 == 0;
            float sign = Random.value < 0.5f ? -1f : 1f;
            Vector3 pos = onSide
                ? new Vector3(sign * Random.Range(ArenaMetrics.HalfWidth + 1f, ArenaMetrics.WallOffset - 1f), 0f,
                    Random.Range(-ArenaMetrics.WallOffset, ArenaMetrics.WallOffset))
                : new Vector3(Random.Range(-ArenaMetrics.WallOffset, ArenaMetrics.WallOffset), 0f,
                    sign * Random.Range(ArenaMetrics.HalfDepth + 1f, ArenaMetrics.WallOffset - 1f));
            pos.y = GroundHeightAt(pos.x, pos.z);
            GameObject tree = Random.value < 0.4f ? presentation?.RandomPine() : presentation?.RandomTree();
            GameObject go = AuthoredVisual(tree, "Border Tree", pos, Vector3.one * Random.Range(0.3f, 0.55f),
                new Vector3(0f, Random.Range(0f, 360f), 0f), null, false);
            AddSway(go, 1.6f, 0.85f);
        }
    }

    private void BuildForest()
    {
        Color grass = new Color(0.12f, 0.28f, 0.1f);
        Color earth = new Color(0.26f, 0.18f, 0.08f);
        Color bark = new Color(0.19f, 0.1f, 0.035f);
        CreateGround("Forest Floor", grass, new Color(0.16f, 0.22f, 0.08f));
        CreateBlock("Forest Track", new Vector3(0f, 0.015f, 0f), new Vector3(7f, 0.035f, ArenaMetrics.GroundSize - 4f), earth, false);
        BuildBoundary(new Color(0.17f, 0.2f, 0.14f), 1.8f);
        int trees = Mathf.RoundToInt(40 * GraphicsQuality.ScatterScale);
        // Trees grow in copses to the sides of the central track rather than evenly.
        List<Vector2> copses = new();
        int copseCount = Mathf.Max(4, trees / 5);
        for (int c = 0; c < copseCount; c++)
            copses.Add(new Vector2((c % 2 == 0 ? -1f : 1f) * Random.Range(10f, 30f), Random.Range(-28f, 28f)));
        for (int i = 0; i < trees; i++)
        {
            Vector3 p = ClusteredPoint(copses, 4.5f, 31f, 31f);
            p.y = GroundHeightAt(p.x, p.z);
            GameObject tree = i % 3 == 0 ? presentation?.RandomPine() : presentation?.RandomTree();
            GameObject go = AuthoredVisual(tree, "Forest Tree", p,
                Vector3.one * Random.Range(0.3f, 0.5f), new Vector3(0f, Random.Range(0f, 360f), 0f));
            AddSway(go, 1.6f, 0.85f);
        }
        CreateBlock("Fallen Log", new Vector3(-7f, 0.55f, 4f), new Vector3(5f, 1.1f, 0.8f), bark);
        CreateBlock("Fallen Log", new Vector3(8f, 0.55f, -5f), new Vector3(4f, 1.1f, 0.8f), bark);
        ScatterClutter(70);
        ScatterGrass(1200);
    }

    private void BuildMarsh()
    {
        Color mud = new Color(0.19f, 0.23f, 0.16f);
        CreateGround("Marsh Ground", mud, new Color(0.14f, 0.18f, 0.12f));
        BuildBoundary(new Color(0.2f, 0.24f, 0.2f), 1.2f);
        int pools = Mathf.RoundToInt(16 * GraphicsQuality.ScatterScale);
        for (int i = 0; i < pools; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 pool = new Vector3(side * Random.Range(8f, 24f), 0.02f, Random.Range(-24f, 24f));
            CreateWater("Shallow Water", pool, new Vector3(Random.Range(3f, 7f), 0.04f, Random.Range(2.5f, 6f)));
            for (int r = 0; r < 3; r++)
            {
                float rx = pool.x + Random.Range(-2.5f, 2.5f);
                float rz = pool.z + Random.Range(-2f, 2f);
                GameObject reed = AuthoredVisual(presentation?.RandomTallGrass(), "Reeds",
                    new Vector3(rx, GroundHeightAt(rx, rz), rz),
                    Vector3.one * Random.Range(0.7f, 1.2f), new Vector3(0f, Random.Range(0f, 360f), 0f), null, false);
                AddSway(reed, 9f, 1.9f);
            }
        }
        CreateBlock("Old Causeway", new Vector3(0f, 0.04f, 0f), new Vector3(5.5f, 0.08f, ArenaMetrics.GroundSize - 4f), new Color(0.3f, 0.25f, 0.16f), false);
        AuthoredVisual(presentation?.villageWagon, "Marsh Wrecked Wagon", new Vector3(-8f, 0f, 2f), Vector3.one * 1.15f,
            new Vector3(0f, 25f, 12f));
        int landmarks = Mathf.RoundToInt(12 * GraphicsQuality.ScatterScale);
        for (int i = 0; i < landmarks; i++)
        {
            float lx = (i % 2 == 0 ? -1f : 1f) * Random.Range(10f, 28f);
            float lz = Random.Range(-26f, 26f);
            GameObject landmark = AuthoredVisual(i % 2 == 0 ? presentation?.RandomDeadTree() : presentation?.bush,
                "Marsh Landmark", new Vector3(lx, GroundHeightAt(lx, lz), lz),
                Vector3.one * Random.Range(0.9f, 1.4f), new Vector3(0f, Random.Range(0f, 360f), 0f), null, false);
            AddSway(landmark, 1.4f, 0.8f);
        }
        GameObject waterAnimator = new GameObject("Water Animator");
        waterAnimator.transform.SetParent(battleRoot.transform);
        waterAnimator.AddComponent<WaterAnimator>().Configure(RuntimeAssets.WaterMaterial(), new Vector2(0.03f, 0.02f));
        ScatterClutter(40); // marsh stays a wetland: reeds at the pools, no grass carpet
    }

    private void BuildHighlands()
    {
        Color rock = new Color(0.32f, 0.31f, 0.29f);
        Color moss = new Color(0.24f, 0.3f, 0.15f);
        CreateGround("Highland Ground", moss, new Color(0.3f, 0.3f, 0.26f));
        BuildBoundary(rock, 2.5f);
        CreateBlock("West Ridge", new Vector3(-22f, 1f, 2f), new Vector3(6f, 2.4f, 30f), rock);
        CreateBlock("East Ridge", new Vector3(22f, 1f, -2f), new Vector3(6f, 2.4f, 30f), rock);
        CreateBlock("North Menhir", new Vector3(-7f, 1.8f, 7f), new Vector3(1.3f, 3.6f, 1.2f), rock);
        CreateBlock("South Menhir", new Vector3(7f, 1.8f, -7f), new Vector3(1.3f, 3.6f, 1.2f), rock);
        int boulders = Mathf.RoundToInt(16 * GraphicsQuality.ScatterScale);
        for (int i = 0; i < boulders; i++)
        {
            float bx = Random.Range(-18f, 18f);
            float bz = Random.Range(-26f, 26f);
            AuthoredVisual(presentation?.RandomRock(), "Boulder", new Vector3(bx, GroundHeightAt(bx, bz) + 0.1f, bz),
                Vector3.one * Random.Range(0.2f, 0.5f),
                new Vector3(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f)), null, false);
        }
        ScatterClutter(50, barren: true); // rocky highlands: pebbles/mushrooms, no flowers
        ScatterGrass(500);
    }

    // Invisible containment: keeps the CharacterController-blocking colliders but hides
    // the wall meshes, so the open vista is framed by the tree-line, not a stone ring.
    // Walls are raised tall so nothing can be seen or escape over them.
    private void BuildBoundary(Color color, float height)
    {
        float w = ArenaMetrics.WallOffset;
        float span = ArenaMetrics.GroundSize + 2f;
        float wallHeight = Mathf.Max(height, 6f);
        CreateBlock("North Boundary", new Vector3(0f, wallHeight * 0.5f, w), new Vector3(span, wallHeight, 1f), color, true, false);
        CreateBlock("South Boundary", new Vector3(0f, wallHeight * 0.5f, -w), new Vector3(span, wallHeight, 1f), color, true, false);
        CreateBlock("East Boundary", new Vector3(w, wallHeight * 0.5f, 0f), new Vector3(1f, wallHeight, span), color, true, false);
        CreateBlock("West Boundary", new Vector3(-w, wallHeight * 0.5f, 0f), new Vector3(1f, wallHeight, span), color, true, false);
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

    private GameObject AuthoredVisual(GameObject prefab, string objectName, Vector3 position, Vector3 scale, Vector3 rotation,
        Color? tintOverride = null, bool castShadows = true)
    {
        if (prefab == null)
            return null;
        GameObject visual = Instantiate(prefab, battleRoot.transform);
        visual.name = objectName;
        visual.transform.position = position;
        visual.transform.localScale = scale;
        visual.transform.eulerAngles = rotation;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
            Destroy(collider);
        // Models keep their own imported (textured) materials; tint only when a
        // heraldry/team colour is explicitly requested (e.g. defender banners).
        if (tintOverride.HasValue)
            TintVisual(visual, tintOverride.Value);
        // Dense/distant scatter skips the shadow pass — its shadows add little but cost
        // a lot at hundreds of instances; structures and hero trees keep theirs.
        if (!castShadows)
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        return visual;
    }

    private static void TintVisual(GameObject visual, Color tint)
    {
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = RuntimeAssets.Material(tint);
    }

    private void AddSway(GameObject visual, float amplitudeDegrees, float frequency)
    {
        if (visual == null || GraphicsQuality.IsLow)
            return;
        if (SettingsService.Current != null && SettingsService.Current.reduceMotion)
            return;
        if (windSway == null)
            windSway = battleRoot.AddComponent<WindSway>();
        windSway.Add(visual.transform, amplitudeDegrees, frequency);
    }

}
