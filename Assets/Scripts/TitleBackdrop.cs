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
        sunObject.transform.rotation = Quaternion.Euler(18f, -38f, 0f);
        sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.66f, 0.42f);
        sun.intensity = 0.92f;
        sun.shadows = LightShadows.Soft;

        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader != null)
        {
            titleSkybox = new Material(shader) { name = "Title Dawn Skybox" };
            titleSkybox.SetColor("_SkyTint", new Color(0.43f, 0.49f, 0.62f));
            titleSkybox.SetColor("_GroundColor", new Color(0.08f, 0.07f, 0.06f));
            titleSkybox.SetFloat("_AtmosphereThickness", 1.18f);
            titleSkybox.SetFloat("_Exposure", 0.78f);
        }
    }

    private void ApplyDawnLighting()
    {
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.25f, 0.28f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.24f, 0.25f, 0.31f);
        RenderSettings.fogDensity = 0.014f;
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
    }

    private void BuildCamp(PresentationCatalog catalog)
    {
        CreatePrimitive("Title Ground", PrimitiveType.Cube, new Vector3(2.5f, -0.4f, 5.5f), new Vector3(38f, 0.8f, 34f),
            new Color(0.13f, 0.16f, 0.10f));
        CreatePrimitive("Title Road", PrimitiveType.Cube, new Vector3(3.7f, 0.015f, 5.6f), new Vector3(5.8f, 0.035f, 25f),
            new Color(0.24f, 0.16f, 0.08f));
        CreatePrimitive("Title Ridge", PrimitiveType.Cube, new Vector3(8.5f, 1.3f, 16f), new Vector3(17f, 2.6f, 3f),
            new Color(0.19f, 0.20f, 0.19f));

        Place(catalog?.tent, "Captain Tent", new Vector3(5.4f, 0f, 6.6f), Vector3.one * 1.65f, -12f, new Color(0.43f, 0.35f, 0.24f));
        Place(catalog?.tent, "Warband Tent", new Vector3(8.4f, 0f, 8.2f), Vector3.one * 1.35f, 18f, new Color(0.34f, 0.30f, 0.22f));
        Place(catalog?.campFence, "Camp Fence", new Vector3(9.8f, 0f, 11.2f), Vector3.one * 1.25f, 78f, new Color(0.29f, 0.16f, 0.07f));
        Place(catalog?.campFence, "Camp Fence", new Vector3(2.6f, 0f, 11.8f), Vector3.one * 1.25f, -72f, new Color(0.29f, 0.16f, 0.07f));
        Place(catalog?.bedroll, "Captain Bedroll", new Vector3(5.9f, 0.02f, 3.8f), Vector3.one * 1.15f, 85f, new Color(0.37f, 0.24f, 0.14f));
        Place(catalog?.villageWagon, "Supply Wagon", new Vector3(10.2f, 0f, 6.9f), Vector3.one * 0.9f, -25f, new Color(0.40f, 0.28f, 0.15f));
        Place(catalog?.barrel, "Supply Barrel", new Vector3(8.2f, 0f, 4.3f), Vector3.one * 1.05f, 0f, new Color(0.34f, 0.20f, 0.09f));
        Place(catalog?.propCrate, "Supply Crate", new Vector3(8.8f, 0f, 4.7f), Vector3.one * 0.86f, 20f, new Color(0.34f, 0.20f, 0.09f));
        Place(catalog?.weaponStand, "Weapon Stand", new Vector3(6.7f, 0f, 4.5f), Vector3.one * 0.92f, -18f, new Color(0.34f, 0.22f, 0.11f));

        banner = Place(catalog?.banner, "Warband Banner", new Vector3(3.6f, 0f, 7.6f), Vector3.one * 1.25f, 0f, new Color(0.16f, 0.42f, 0.80f));
        Place(catalog?.captainPrefab, "Title Captain", new Vector3(4.4f, 0f, 3.1f), Vector3.one * 1.18f, 153f, null);
        Place(catalog?.militiaPrefab, "Title Militia", new Vector3(6.1f, 0f, 3.8f), Vector3.one, 166f, null);
        Place(catalog?.militiaPrefab, "Title Militia", new Vector3(7.4f, 0f, 4.9f), Vector3.one * 0.96f, 174f, null);
        Place(catalog?.commonTree, "Title Tree", new Vector3(12.7f, 0f, 9f), Vector3.one * 0.58f, 22f, new Color(0.17f, 0.34f, 0.12f));
        Place(catalog?.pineTree, "Title Pine", new Vector3(0.8f, 0f, 13f), Vector3.one * 0.66f, -18f, new Color(0.14f, 0.30f, 0.12f));
        Place(catalog?.rock, "Title Boulder", new Vector3(11.5f, 0.1f, 3.4f), Vector3.one * 1.15f, 15f, new Color(0.36f, 0.37f, 0.35f));

        Transform fire = Place(catalog?.campfire, "Campfire", new Vector3(5.45f, 0f, 4.9f), Vector3.one * 1.25f, 0f, null);
        if (fire == null)
            CreatePrimitive("Campfire Pit", PrimitiveType.Cylinder, new Vector3(5.45f, 0.06f, 4.9f), new Vector3(1.2f, 0.12f, 1.2f), new Color(0.16f, 0.12f, 0.09f));
        BuildFlame(new Vector3(5.45f, 0.55f, 4.9f));
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
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = RuntimeAssets.Material(tint.Value);
        return instance.transform;
    }

    private void CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(sceneRoot.transform, false);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        RemovePhysics(primitive);
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

    private void OnDestroy()
    {
        if (titleSkybox != null)
            Destroy(titleSkybox);
    }
}
