using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the battlefield itself — the biome (terrain mesh, ground, scatter, grass,
// water, boundary) and the kind-specific dressing (hold / bandit camp / training
// yard, plus torches, tents, banners and the distant silhouette ring) — extracted
// from BattleBootstrap. A component on the battle root so it shares its lifetime;
// BattleBootstrap keeps lighting, camera, spawning and the day-clock weather choice.
public sealed class ArenaBuilder : MonoBehaviour
{
    // Terrain extends well past the playable footprint so the rolling rim and the
    // distant silhouette ring sit on continuous ground, not floating over a void.
    private const float TerrainWorldSize = ArenaMetrics.GroundSize + 96f;
    // Keep scatter off the central road/causeway/track so the clash lane stays clear.
    private const float PathClearHalfWidth = 3.75f;

    private GameObject battleRoot;
    private PresentationCatalog presentation;
    private float timeOfDay = 0.5f;
    private Weather weather = Weather.Clear;
    private WindSway windSway;

    // Entry point: build the chosen biome plus the dressing for the encounter kind.
    public void Build(ArenaType arena, BattleKind kind, PresentationCatalog presentation, float timeOfDay, Weather weather)
    {
        battleRoot = gameObject;
        this.presentation = presentation;
        this.timeOfDay = timeOfDay;
        this.weather = weather;
        BuildArena(arena, kind);
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
    // Public+static so EditMode tests can assert the flat-core / bounded-rim contract.
    public static float TerrainHeight(float x, float z)
    {
        float r = Mathf.Max(Mathf.Abs(x) / ArenaMetrics.HalfWidth, Mathf.Abs(z) / ArenaMetrics.HalfDepth);
        float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 1.9f, r));
        float n = (Mathf.PerlinNoise(x * 0.045f + 100f, z * 0.045f + 100f) - 0.5f)
            + (Mathf.PerlinNoise(x * 0.12f + 40f, z * 0.12f + 40f) - 0.5f) * 0.4f;
        return n * outside * 7f;
    }

    public static float GroundHeightAt(float x, float z) => TerrainHeight(x, z);

    public static List<Vector2> ClusterCenters(int count, float halfWidth, float halfDepth)
    {
        List<Vector2> centers = new();
        for (int i = 0; i < count; i++)
            centers.Add(new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfDepth, halfDepth)));
        return centers;
    }

    // A point scattered within `radius` of a random cluster centre (uniform over the
    // disc), clamped to the field. y is left at 0 for the caller to sample onto terrain.
    public static Vector3 ClusteredPoint(List<Vector2> centers, float radius, float halfWidth, float halfDepth)
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
