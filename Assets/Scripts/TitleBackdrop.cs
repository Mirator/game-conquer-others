using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A presentation-only scene that gives the title screen a place in the game
// world. It deliberately owns no gameplay components, physics, or input: the
// persistent FrontendUi turns it on only while the director is in Title mode.
public sealed class TitleBackdrop : MonoBehaviour
{
    public Camera TitleCamera => titleCamera;
    public bool IsVisible => sceneRoot != null && sceneRoot.activeSelf;

    private GameObject sceneRoot;
    private Camera titleCamera;
    private Light sun;
    private Light campfireLight;
    private Transform cameraRig;
    private Transform banner;
    private Transform flame;
    private Vector3 cameraRestPosition;
    private Quaternion cameraRestRotation;
    private Material titleSkybox;
    private float ambientTime;

    // Shared anchor points for the camp layout: the camp's centre x (the terrain stays
    // flat around it) and the hearth the fire/figures sit on (kept clear of scatter).
    private const float CampCenterX = 4f;
    private static readonly Vector2 CampHearth = new Vector2(5.5f, 5f);

    public void Configure()
    {
        if (sceneRoot != null)
            return;

        PresentationCatalog catalog = PresentationCatalog.Load();
        sceneRoot = new GameObject("Title Backdrop");
        sceneRoot.transform.SetParent(transform, false);

        BuildLighting();
        BuildCamera();
        BuildCamp(catalog);
        sceneRoot.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (sceneRoot == null)
            Configure();
        if (sceneRoot == null)
            return;

        if (visible)
        {
            ApplyDawnLighting();
            sceneRoot.SetActive(true);
            titleCamera.enabled = true;
        }
        else
        {
            if (titleCamera != null)
                titleCamera.enabled = false;
            sceneRoot.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!IsVisible)
            return;

        bool reducedMotion = SettingsService.Current != null && SettingsService.Current.reduceMotion;
        if (reducedMotion)
        {
            cameraRig.localPosition = cameraRestPosition;
            cameraRig.localRotation = cameraRestRotation;
            if (banner != null)
                banner.localRotation = Quaternion.identity;
            if (flame != null)
                flame.localScale = new Vector3(0.48f, 0.72f, 0.48f);
            if (campfireLight != null)
                campfireLight.intensity = 3.1f;
            return;
        }

        ambientTime += Time.unscaledDeltaTime;
        float drift = Mathf.Sin(ambientTime * 0.32f);
        cameraRig.localPosition = cameraRestPosition + new Vector3(drift * 0.16f, Mathf.Sin(ambientTime * 0.21f) * 0.05f, 0f);
        cameraRig.localRotation = cameraRestRotation * Quaternion.Euler(Mathf.Sin(ambientTime * 0.27f) * 0.25f, drift * 0.55f, 0f);
        if (banner != null)
            banner.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(ambientTime * 0.75f) * 3.5f);
        if (flame != null)
            flame.localScale = new Vector3(0.48f, 0.72f + Mathf.Sin(ambientTime * 4.7f) * 0.08f, 0.48f);
        if (campfireLight != null)
            campfireLight.intensity = 3.1f + Mathf.Sin(ambientTime * 5.3f) * 0.25f;
    }

    private void BuildLighting()
    {
        GameObject sunObject = new GameObject("Title Dawn Sun");
        sunObject.transform.SetParent(sceneRoot.transform, false);
        // A low, raking dawn sun that sits roughly behind the camp (in view to the
        // right of the menu) so the procedural sky paints a warm horizon glow there
        // and the camp casts long morning shadows toward the camera.
        sunObject.transform.rotation = Quaternion.Euler(9f, 28f, 0f);
        sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.72f, 0.46f);
        sun.intensity = 1.18f;
        sun.shadows = LightShadows.Soft;

        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader != null)
        {
            titleSkybox = new Material(shader) { name = "Title Dawn Skybox" };
            titleSkybox.SetColor("_SkyTint", new Color(0.50f, 0.46f, 0.52f));
            titleSkybox.SetColor("_GroundColor", new Color(0.06f, 0.05f, 0.045f));
            titleSkybox.SetFloat("_AtmosphereThickness", 1.5f);
            titleSkybox.SetFloat("_Exposure", 1.02f);
            titleSkybox.SetFloat("_SunSize", 0.06f);
            titleSkybox.SetFloat("_SunSizeConvergence", 3f);
        }
    }

    private void ApplyDawnLighting()
    {
        RenderSettings.sun = sun;
        // Trilight ambient: a warm sky glow, a soft amber band at the horizon, and a
        // dark cool ground bounce. This makes the camp read as lit by a sunrise
        // rather than a flat grey overcast.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.43f, 0.50f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.32f, 0.26f);
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.09f, 0.09f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        // Warm morning mist that matches the horizon, dense enough to swallow the far
        // edges of the ground (so it never reads as a floating slab) yet thin enough
        // to keep the camp crisp.
        RenderSettings.fogColor = new Color(0.50f, 0.43f, 0.42f);
        RenderSettings.fogDensity = 0.017f;
        if (titleSkybox != null)
            RenderSettings.skybox = titleSkybox;
        DynamicGI.UpdateEnvironment();
    }

    private void BuildCamera()
    {
        cameraRig = new GameObject("Title Camera Rig").transform;
        cameraRig.SetParent(sceneRoot.transform, false);
        cameraRestPosition = new Vector3(-0.35f, 2.8f, -12.8f);
        cameraRestRotation = Quaternion.LookRotation((new Vector3(3.1f, 1.55f, 3.2f) - cameraRestPosition).normalized);
        cameraRig.localPosition = cameraRestPosition;
        cameraRig.localRotation = cameraRestRotation;

        GameObject cameraObject = new GameObject("Title Camera");
        cameraObject.transform.SetParent(cameraRig, false);
        cameraObject.tag = "MainCamera";
        titleCamera = cameraObject.AddComponent<Camera>();
        titleCamera.fieldOfView = 51f;
        titleCamera.nearClipPlane = 0.15f;
        titleCamera.farClipPlane = 120f;
        titleCamera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();
        BattlePostProcessing.Apply(titleCamera, cameraObject.transform);
    }

    private void BuildCamp(PresentationCatalog catalog)
    {
        BuildTitleGround();

        // Camp props rest on the (near-flat) camp floor; Ground() samples the terrain
        // so any gentle undulation beneath them is respected.
        Place(catalog?.tent, "Captain Tent", Ground(5.4f, 6.6f), Vector3.one * 1.65f, -12f, new Color(0.43f, 0.35f, 0.24f));
        Place(catalog?.tent, "Warband Tent", Ground(8.4f, 8.2f), Vector3.one * 1.35f, 18f, new Color(0.34f, 0.30f, 0.22f));
        Place(catalog?.campFence, "Camp Fence", Ground(9.8f, 11.2f), Vector3.one * 1.25f, 78f, new Color(0.29f, 0.16f, 0.07f));
        Place(catalog?.campFence, "Camp Fence", Ground(2.6f, 11.8f), Vector3.one * 1.25f, -72f, new Color(0.29f, 0.16f, 0.07f));
        Place(catalog?.bedroll, "Captain Bedroll", Ground(5.9f, 3.8f, 0.02f), Vector3.one * 1.15f, 85f, new Color(0.37f, 0.24f, 0.14f));
        Place(catalog?.villageWagon, "Supply Wagon", Ground(10.2f, 6.9f), Vector3.one * 0.9f, -25f, new Color(0.40f, 0.28f, 0.15f));
        Place(catalog?.barrel, "Supply Barrel", Ground(8.2f, 4.3f), Vector3.one * 1.05f, 0f, new Color(0.34f, 0.20f, 0.09f));
        Place(catalog?.propCrate, "Supply Crate", Ground(8.8f, 4.7f), Vector3.one * 0.86f, 20f, new Color(0.34f, 0.20f, 0.09f));
        Place(catalog?.weaponStand, "Weapon Stand", Ground(6.7f, 4.5f), Vector3.one * 0.92f, -18f, new Color(0.34f, 0.22f, 0.11f));

        banner = Place(catalog?.banner, "Warband Banner", Ground(3.6f, 7.6f), Vector3.one * 1.25f, 0f, new Color(0.16f, 0.42f, 0.80f));
        Place(catalog?.captainPrefab, "Title Captain", Ground(4.4f, 3.1f), Vector3.one * 1.18f, 153f, null);
        Place(catalog?.militiaPrefab, "Title Militia", Ground(6.1f, 3.8f), Vector3.one, 166f, null);
        Place(catalog?.militiaPrefab, "Title Militia", Ground(7.4f, 4.9f), Vector3.one * 0.96f, 174f, null);
        Place(catalog?.commonTree, "Title Tree", Ground(12.7f, 9f), Vector3.one * 0.58f, 22f, new Color(0.17f, 0.34f, 0.12f));
        Place(catalog?.pineTree, "Title Pine", Ground(0.8f, 13f), Vector3.one * 0.66f, -18f, new Color(0.14f, 0.30f, 0.12f));
        Place(catalog?.rock, "Title Boulder", Ground(11.5f, 3.4f, 0.1f), Vector3.one * 1.15f, 15f, new Color(0.36f, 0.37f, 0.35f));

        ScatterTreeline(catalog);
        ScatterGroundDetail(catalog);
        BuildGrassCarpet();

        Transform fire = Place(catalog?.campfire, "Campfire", Ground(5.45f, 4.9f), Vector3.one * 1.25f, 0f, null);
        if (fire == null)
            CreatePrimitive("Campfire Pit", PrimitiveType.Cylinder, Ground(5.45f, 4.9f, 0.06f), new Vector3(1.2f, 0.12f, 1.2f), new Color(0.16f, 0.12f, 0.09f));
        BuildFlame(Ground(5.45f, 4.9f, 0.55f));
        BuildEmbers(Ground(5.45f, 4.9f, 0.7f));
    }

    // Height field for the title ground: flat across the camp and the corridor toward
    // the camera (so the hand-placed props and figures read cleanly), then rolling hills
    // rise behind the camp and along the far flanks to fill the horizon with land.
    private static float TitleGroundHeight(float x, float z)
    {
        float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 42f, z));
        float side = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 60f, Mathf.Abs(x - CampCenterX)));
        float rise = Mathf.Max(back, side);
        float n = (Mathf.PerlinNoise(x * 0.045f + 100f, z * 0.045f + 100f) - 0.5f) * 2f
                + (Mathf.PerlinNoise(x * 0.13f + 40f, z * 0.13f + 40f) - 0.5f) * 0.6f;
        return n * rise * 9f + back * 2.5f;
    }

    private static Vector3 Ground(float x, float z, float y = 0f) =>
        new Vector3(x, TitleGroundHeight(x, z) + y, z);

    // One undulating ground mesh (built the way the arenas build theirs) that stays flat
    // under the camp and swells into hills beyond — this replaces the old flat slab and
    // the box "ridges" with continuous terrain that simply fades into the mist.
    private void BuildTitleGround()
    {
        const int seg = 96;
        const float width = 200f;
        const float depth = 210f;
        Vector2 origin = new Vector2(CampCenterX, 30f);
        int vpr = seg + 1;
        Vector3[] vertices = new Vector3[vpr * vpr];
        Vector2[] uvs = new Vector2[vpr * vpr];
        for (int z = 0; z < vpr; z++)
            for (int x = 0; x < vpr; x++)
            {
                float fx = origin.x + (x / (float)seg - 0.5f) * width;
                float fz = origin.y + (z / (float)seg - 0.5f) * depth;
                int i = z * vpr + x;
                vertices[i] = new Vector3(fx, TitleGroundHeight(fx, fz), fz);
                uvs[i] = new Vector2(x / (float)seg, z / (float)seg);
            }
        int[] triangles = new int[seg * seg * 6];
        int t = 0;
        for (int z = 0; z < seg; z++)
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
        Mesh mesh = new Mesh { name = "Title Terrain" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject ground = new GameObject("Title Ground");
        ground.transform.SetParent(sceneRoot.transform, false);
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial = RuntimeAssets.GroundMaterial(
            new Color(0.20f, 0.21f, 0.14f), new Color(0.11f, 0.13f, 0.08f), width / 4f);
    }

    // A naturally clumped pine treeline cresting the hills behind the camp, with varied
    // spacing and size so it reads as a forest edge rather than a fence of clones. Seeded
    // so the silhouette is stable across title visits.
    private void ScatterTreeline(PresentationCatalog catalog)
    {
        GameObject pine = catalog?.pineTree;
        if (pine == null)
            return;
        using var seeded = new SeededRandom(20240617);
        const int clumps = 6;
        for (int c = 0; c < clumps; c++)
        {
            float cx = Mathf.Lerp(-26f, 34f, (c + Random.Range(-0.18f, 0.18f)) / (clumps - 1f));
            float cz = Random.Range(20f, 33f);
            int inClump = Random.Range(2, 5);
            for (int k = 0; k < inClump; k++)
            {
                float x = cx + Random.Range(-4f, 4f);
                float z = cz + Random.Range(-3f, 3f);
                Place(pine, "Distant Pine", Ground(x, z), Vector3.one * Random.Range(0.55f, 0.95f),
                    Random.Range(0f, 360f), new Color(0.10f, 0.15f, 0.12f));
            }
        }
    }

    // Loose clumps of bushes and rocks around the camp give the ground mid-size detail
    // between the grass carpet and the buildings. Tinted (rather than using the authored
    // materials) to stay on-palette and to sidestep any non-URP source shaders.
    private void ScatterGroundDetail(PresentationCatalog catalog)
    {
        if (catalog == null)
            return;
        using var seeded = new SeededRandom(99114);
        for (int i = 0; i < 14; i++)
        {
            float x = CampCenterX + Random.Range(-20f, 20f);
            float z = 6f + Random.Range(-9f, 16f);
            if (Mathf.Abs(x - CampHearth.x) < 4f && Mathf.Abs(z - CampHearth.y) < 4f)
                continue; // leave the fire and the figures their breathing room
            bool rock = Random.value < 0.45f;
            GameObject model = rock ? (catalog.RandomRock() ?? catalog.rock) : (catalog.bush ?? catalog.RandomClutter());
            Color tint = rock
                ? new Color(0.30f, 0.31f, 0.30f) * Random.Range(0.85f, 1.1f)
                : new Color(0.13f, 0.20f, 0.11f) * Random.Range(0.85f, 1.15f);
            Place(model, rock ? "Camp Rock" : "Camp Bush", Ground(x, z),
                Vector3.one * Random.Range(0.5f, 1.0f), Random.Range(0f, 360f), tint);
        }
    }

    // A GPU-instanced grass carpet over the camp floor — thousands of blades in a few
    // batched draw calls (the same field the arenas use), so the flat near-ground reads
    // as a living meadow instead of a painted plane.
    private void BuildGrassCarpet()
    {
        int count = Mathf.RoundToInt(4200f * Mathf.Max(0.25f, GraphicsQuality.ScatterScale));
        List<Matrix4x4> instances = new(count);
        using (new SeededRandom(73312))
        {
            for (int i = 0; i < count; i++)
            {
                // Keep grass out of the immediate foreground under the low camera (where
                // a single blade would loom huge) and carpet the camp and its surrounds.
                float x = CampCenterX + Random.Range(-24f, 24f);
                float z = Random.Range(-1f, 28f);
                // Thin the carpet over the trodden centre and skip right around the fire.
                if (Mathf.Abs(x - CampHearth.x) < 3f && Mathf.Abs(z - CampHearth.y) < 3.5f && Random.value < 0.85f)
                    continue;
                Vector3 pos = Ground(x, z);
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float s = Random.Range(0.32f, 0.6f);
                instances.Add(Matrix4x4.TRS(pos, rot, new Vector3(s, s * Random.Range(0.85f, 1.3f), s)));
            }
        }

        GameObject field = new GameObject("Title Grass");
        field.transform.SetParent(sceneRoot.transform, false);
        field.AddComponent<GrassField>().Build(RuntimeAssets.GrassMesh(), RuntimeAssets.GrassMaterial(), instances);
    }

    // A lazy column of glowing embers drifting up from the campfire. Subtle and
    // additive — it gives the otherwise-static camp a flicker of life. The system
    // self-animates, so it keeps running even under reduced motion.
    private void BuildEmbers(Vector3 position)
    {
        GameObject embersObject = new GameObject("Campfire Embers");
        embersObject.transform.SetParent(sceneRoot.transform, false);
        embersObject.transform.position = position;

        ParticleSystem system = embersObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = 2.6f;
        main.startSpeed = 0.55f;
        main.startSize = 0.05f;
        main.startColor = new Color(1f, 0.55f, 0.18f, 1f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.22f;

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.62f, 0.22f), 0f), new GradientColorKey(new Color(0.7f, 0.18f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.4f;

        ParticleSystemRenderer renderer = embersObject.GetComponent<ParticleSystemRenderer>();
        renderer.material = RuntimeAssets.SoftParticleMaterial();
        renderer.sortingFudge = -2f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void BuildFlame(Vector3 position)
    {
        GameObject flameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flameObject.name = "Campfire Flame";
        flameObject.transform.SetParent(sceneRoot.transform, false);
        flameObject.transform.position = position;
        flameObject.transform.localScale = new Vector3(0.48f, 0.72f, 0.48f);
        RemovePhysics(flameObject);
        flameObject.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(1f, 0.34f, 0.06f), true);
        flame = flameObject.transform;
        campfireLight = flameObject.AddComponent<Light>();
        campfireLight.type = LightType.Point;
        campfireLight.color = new Color(1f, 0.42f, 0.14f);
        campfireLight.range = 8.5f;
        campfireLight.intensity = 3.1f;
        campfireLight.shadows = LightShadows.None;
    }

    private Transform Place(GameObject prefab, string name, Vector3 position, Vector3 scale, float yaw, Color? tint)
    {
        if (prefab == null)
            return null;
        GameObject instance = Instantiate(prefab, sceneRoot.transform);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.localScale = scale;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        RemovePhysics(instance);
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>())
            behaviour.enabled = false;
        if (tint.HasValue)
            RuntimeAssets.TintModel(instance, tint.Value);
        return instance.transform;
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(sceneRoot.transform, false);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        RemovePhysics(primitive);
        return primitive;
    }

    private static void RemovePhysics(GameObject instance)
    {
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0)
            instance.layer = ignoreRaycast;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            Object.Destroy(body);
        }
    }

    // Seeds Unity's global RNG for a deterministic block of scatter, then restores the
    // prior state on dispose so the rest of the game's random stream is undisturbed.
    private readonly struct SeededRandom : System.IDisposable
    {
        private readonly Random.State prior;
        public SeededRandom(int seed)
        {
            prior = Random.state;
            Random.InitState(seed);
        }
        public void Dispose() => Random.state = prior;
    }

    private void OnDestroy()
    {
        if (titleSkybox != null)
            Destroy(titleSkybox);
    }
}
