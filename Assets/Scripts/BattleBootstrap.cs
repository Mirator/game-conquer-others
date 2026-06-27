using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds a complete runtime battle (lighting, camera, fighters) under a root
// GameObject supplied by the GameDirector, delegating the battlefield itself
// (biome terrain/scatter/dressing) to ArenaBuilder. No longer the app entry point;
// the director owns lifecycle and mode switching.
public sealed class BattleBootstrap : MonoBehaviour
{
    private GameObject battleRoot;
    private PresentationCatalog presentation;
    private float timeOfDay = 0.5f;
    private Light sun;
    private BattleEffects effects;
    private ArenaAtmosphere atmosphere;
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
        atmosphere = new ArenaAtmosphere(battleRoot, timeOfDay, effects);

        SetupLighting(setup.Arena);
        ArenaBuilder arenaBuilder = battleRoot.AddComponent<ArenaBuilder>();
        arenaBuilder.Build(setup.Arena, setup.Kind, presentation, timeOfDay, weather);
        PlaceAmbience(setup.Arena);
        atmosphere.ApplyWeather(weather);

        Camera camera = CreateCamera();
        BattlePostProcessing.Apply(camera, battleRoot.transform);
        atmosphere.BuildSky(camera);
        atmosphere.BuildAmbientParticles();
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
}
