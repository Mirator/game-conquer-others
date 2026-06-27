using UnityEngine;

// Per-battle weather (chosen on CampaignState's clock) plus the sky and air-particle
// dressing.
public enum Weather
{
    Clear,
    Rain,
    Snow,
    Mist
}

// The battle's atmosphere layer, extracted from BattleBootstrap: the weather particle
// layer (rain/snow/mist) and its ambience, the camera-following sky dressing (stars,
// moon, clouds), and the ambient air particles (motes by day, fireflies at night).
// Fog/sun/wet-ground tinting from the same Weather value stays in BattleBootstrap's
// lighting/ground code. Quality-gated throughout.
public sealed class ArenaAtmosphere
{
    private readonly GameObject battleRoot;
    private readonly float timeOfDay;
    private readonly BattleEffects effects;

    public ArenaAtmosphere(GameObject battleRoot, float timeOfDay, BattleEffects effects)
    {
        this.battleRoot = battleRoot;
        this.timeOfDay = timeOfDay;
        this.effects = effects;
    }

    // Spawns the weather particle layer and its ambience. Fog/sun/wet-ground are handled
    // in BattleBootstrap's ApplySunAndSky/CreateGround from the same Weather value.
    public void ApplyWeather(Weather weather)
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
        main.maxParticles = 3000; // hard ceiling; ~1960 alive at full rain, so this only bounds spikes
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
    public void BuildSky(Camera camera)
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
        Object.Destroy(moon.GetComponent<Collider>());
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
    public void BuildAmbientParticles()
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
}
