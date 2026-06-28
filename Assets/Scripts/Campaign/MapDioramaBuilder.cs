using System.Collections.Generic;
using UnityEngine;

// Builds the campaign's miniature world: deterministic regional terrain, roads,
// settlement silhouettes, and non-interactive dressing beneath the map markers.
public sealed class MapDioramaBuilder
{
    private readonly Transform root;
    private readonly PresentationCatalog presentation;

    // The table's footprint (centred on z = 2 to match the map layout) and the level
    // areas the rolling terrain must respect: a flat pad under every hold and the
    // Training Arena, and a flat corridor along every road, so settlements, markers,
    // and roads never float over or sink into a slope.
    private const float MinX = -46f, MaxX = 46f, MinZ = -36f, MaxZ = 40f;
    private readonly List<Vector2> flatPads = new();
    private readonly List<(Vector2 a, Vector2 b)> flatRoads = new();

    public MapDioramaBuilder(Transform parent)
    {
        root = new GameObject("Campaign Diorama").transform;
        root.SetParent(parent, false);
        presentation = PresentationCatalog.Load();
    }

    public void Build(CampaignState campaign)
    {
        CollectFlatAreas(campaign);
        BuildGround();
        foreach (Territory territory in campaign.Territories)
            BuildDistrict(territory, campaign.Seed);
        foreach (Territory territory in campaign.Territories)
            foreach (int adjacent in territory.AdjacentIds)
                if (adjacent > territory.Id)
                    BuildRoad(territory, campaign.GetById(adjacent));
        BuildAmbientLandscape(campaign);
    }

    // Records the level pads (holds + the Training Arena) and road corridors the
    // terrain must flatten around. The Training Arena pad mirrors the fixed node
    // position in CampaignMapController so the two stay aligned.
    private void CollectFlatAreas(CampaignState campaign)
    {
        flatPads.Clear();
        flatRoads.Clear();
        foreach (Territory t in campaign.Territories)
            flatPads.Add(WorldXZ(t.MapPosition));
        flatPads.Add(new Vector2(14f, -9f)); // Training Arena node
        foreach (Territory t in campaign.Territories)
            foreach (int adjacent in t.AdjacentIds)
                if (adjacent > t.Id)
                    flatRoads.Add((WorldXZ(t.MapPosition), WorldXZ(campaign.GetById(adjacent).MapPosition)));
    }

    private static Vector2 WorldXZ(Vector2 mapPosition) => new Vector2(mapPosition.x * 1.4f, mapPosition.y * 1.4f);

    // Dresses the rolling ground with scattered woods and the odd pond, each seated on
    // the terrain surface and kept clear of the holds and roads. Relief now comes from
    // the ground mesh itself, so there are no prop "hills" here. Deterministic by seed.
    private void BuildAmbientLandscape(CampaignState campaign)
    {
        System.Random random = new System.Random(campaign.Seed * 9176 + 4242);
        const float step = 6.5f;
        for (float gx = MinX + 4f; gx <= MaxX - 4f; gx += step)
            for (float gz = MinZ + 4f; gz <= MaxZ - 4f; gz += step)
            {
                if (random.NextDouble() < 0.32)
                    continue; // leave natural gaps
                Vector2 p = new(gx + Range(random, -2.5f, 2.5f), gz + Range(random, -2.5f, 2.5f));

                float padDist = float.MaxValue;
                foreach (Vector2 pad in flatPads)
                    padDist = Mathf.Min(padDist, Vector2.Distance(p, pad));
                if (padDist < 9f) // a clearing around every hold so it breathes
                    continue;
                float roadDist = float.MaxValue;
                foreach ((Vector2 a, Vector2 b) in flatRoads)
                    roadDist = Mathf.Min(roadDist, DistanceToSegment(p, a, b));
                if (roadDist < 3f)
                    continue;

                float y = TerrainHeight(p.x, p.y);
                // Ponds settle into the low flats; woods carpet the rest of the ground.
                if (y < 0.2f && random.NextDouble() < 0.16)
                    BuildPond(new Vector3(p.x, y, p.y), random);
                else
                    BuildWood(new Vector3(p.x, y, p.y), random);
            }
    }

    private void BuildWood(Vector3 center, System.Random random)
    {
        int count = random.Next(3, 6);
        for (int i = 0; i < count; i++)
        {
            float tx = center.x + Range(random, -2.4f, 2.4f);
            float tz = center.z + Range(random, -2.4f, 2.4f);
            Vector3 p = new Vector3(tx, TerrainHeight(tx, tz), tz); // sit on the slope
            GameObject prefab = i % 2 == 0 ? presentation?.commonTree : presentation?.pineTree;
            Color green = i % 2 == 0 ? new Color(0.20f, 0.42f, 0.18f) : new Color(0.13f, 0.30f, 0.16f);
            if (prefab != null)
                CreatePrefab(prefab, "Wildwood Tree", p, Vector3.one * Range(random, 0.34f, 0.56f), random.Next(0, 360), green);
            else
            {
                CreatePrimitive("Wildwood Trunk", PrimitiveType.Cylinder, p + Vector3.up * 0.7f,
                    new Vector3(0.24f, 1.4f, 0.24f), new Color(0.21f, 0.12f, 0.05f));
                CreatePrimitive("Wildwood Crown", PrimitiveType.Sphere, p + Vector3.up * 1.8f,
                    Vector3.one * 1.05f, green);
            }
        }
    }

    private void BuildPond(Vector3 pos, System.Random random)
        => CreatePrimitive("Pond", PrimitiveType.Cylinder, pos + Vector3.up * 0.06f,
            new Vector3(Range(random, 3f, 5f), 0.04f, Range(random, 2.4f, 4.4f)), new Color(0.12f, 0.32f, 0.36f));

    // Planar distance from p to segment a-b, for hold/road keep-out and flattening.
    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    private void BuildGround()
    {
        // A solid base slab gives the table its thickness and dark underside. Its top
        // (y = -0.9) sits safely below the terrain's clamped minimum (-0.5) so the two
        // surfaces never coincide and never z-fight. No collider — map clicks land on
        // the terrain surface above it.
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Map Table Base";
        slab.transform.SetParent(root, false);
        slab.transform.position = new Vector3(0f, -1.6f, 2f);
        slab.transform.localScale = new Vector3(94f, 1.4f, 80f);
        Object.Destroy(slab.GetComponent<Collider>());
        slab.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.12f, 0.08f, 0.05f));

        BuildTerrainMesh();

        // The rim spans -1.0..1.0, covering the slab top and the terrain's cut edge.
        Color rimColor = new Color(0.16f, 0.10f, 0.05f);
        BuildRim(new Vector3(0f, 0f, MaxZ), new Vector3(96f, 2.0f, 2.6f), rimColor);
        BuildRim(new Vector3(0f, 0f, MinZ), new Vector3(96f, 2.0f, 2.6f), rimColor);
        BuildRim(new Vector3(MaxX, 0f, 2f), new Vector3(2.6f, 2.0f, 80f), rimColor);
        BuildRim(new Vector3(MinX, 0f, 2f), new Vector3(2.6f, 2.0f, 80f), rimColor);
    }

    // A procedurally height-mapped, flat-shaded ground mesh: gentle rolling relief that
    // rises into hills toward the table edges and lies level over the hold pads and
    // road corridors. The mottled parchment material drapes over it so the map reads as
    // sculpted terrain rather than a flat board; a MeshCollider catches map clicks.
    private void BuildTerrainMesh()
    {
        const float cell = 2.5f;
        int cols = Mathf.CeilToInt((MaxX - MinX) / cell);
        int rows = Mathf.CeilToInt((MaxZ - MinZ) / cell);

        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                float x0 = MinX + i * cell, x1 = Mathf.Min(MinX + (i + 1) * cell, MaxX);
                float z0 = MinZ + j * cell, z1 = Mathf.Min(MinZ + (j + 1) * cell, MaxZ);
                Vector3 a = Corner(x0, z0), b = Corner(x1, z0), c = Corner(x1, z1), d = Corner(x0, z1);
                AddFlatTri(a, c, b, verts, tris, uvs); // CCW from above → upward normals
                AddFlatTri(a, d, c, verts, tris, uvs);
            }

        Mesh mesh = new() { name = "Campaign Terrain" };
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        GameObject ground = new GameObject("Campaign Terrain");
        ground.transform.SetParent(root, false);
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial =
            RuntimeAssets.GroundMaterial(new Color(0.30f, 0.25f, 0.17f), new Color(0.41f, 0.35f, 0.24f), 1f);
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private Vector3 Corner(float x, float z) => new Vector3(x, TerrainHeight(x, z), z);

    // Each triangle owns its three vertices, so RecalculateNormals gives crisp per-face
    // shading (the low-poly faceted look). UVs map world XZ for the tiling ground texture.
    private static void AddFlatTri(Vector3 a, Vector3 b, Vector3 c, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        int n = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        uvs.Add(new Vector2(a.x, a.z) * 0.1f);
        uvs.Add(new Vector2(b.x, b.z) * 0.1f);
        uvs.Add(new Vector2(c.x, c.z) * 0.1f);
        tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
    }

    // Rolling relief: low layered noise, calm in the central basin and rising toward the
    // table edges, then forced to zero over the hold pads and road corridors so the
    // playable surface stays level.
    private float TerrainHeight(float x, float z)
    {
        float n = Mathf.PerlinNoise(x * 0.045f + 11.3f, z * 0.045f + 7.7f) * 0.65f
                + Mathf.PerlinNoise(x * 0.11f + 3.1f, z * 0.11f + 19.4f) * 0.35f;
        float hills = n - 0.5f;
        float edge = Mathf.Max(Mathf.Abs(x) / 46f, Mathf.Abs(z - 2f) / 38f);
        float amp = Mathf.Lerp(0.8f, 3.4f, Mathf.InverseLerp(0.45f, 0.98f, edge));
        // Clamp the valley depth so the ground never reaches the base slab beneath it
        // (the source of camera-movement z-fighting) and stays above the rim's footing.
        return Mathf.Max(-0.5f, hills * amp * FlatMask(x, z));
    }

    // 0 over a level pad/road (and its margin), ramping to 1 across the open ground.
    private float FlatMask(float x, float z)
    {
        Vector2 p = new(x, z);
        float mask = 1f;
        foreach (Vector2 pad in flatPads)
            mask *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 11f, Vector2.Distance(p, pad)));
        foreach ((Vector2 a, Vector2 b) in flatRoads)
            mask *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 6f, DistanceToSegment(p, a, b)));
        return mask;
    }

    private void BuildRim(Vector3 position, Vector3 scale, Color color)
        => CreatePrimitive("Table Rim", PrimitiveType.Cube, position, scale, color);

    private void BuildDistrict(Territory territory, int seed)
    {
        System.Random random = new System.Random(seed * 486187739 + territory.Id * 92821);
        Vector3 center = WorldOf(territory.MapPosition);
        Color ground = GroundColor(territory.Arena);
        CreatePrimitive($"Diorama District {territory.Id}", PrimitiveType.Cylinder,
            center + Vector3.up * 0.025f, new Vector3(9.6f, 0.05f, 9.6f), ground);

        switch (territory.Arena)
        {
            case ArenaType.Forest:
                BuildForestDistrict(center, random);
                break;
            case ArenaType.Marsh:
                BuildMarshDistrict(center, random);
                break;
            case ArenaType.Highlands:
                BuildHighlandDistrict(center, random);
                break;
            default:
                BuildCourtyardDistrict(center, random);
                break;
        }

        BuildSettlement(center, territory, random);
    }

    private void BuildRoad(Territory a, Territory b)
    {
        Vector3 from = WorldOf(a.MapPosition);
        Vector3 to = WorldOf(b.MapPosition);
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f)
            return;
        GameObject road = CreatePrimitive($"Road {a.Id}-{b.Id}", PrimitiveType.Cube,
            (from + to) * 0.5f + Vector3.up * 0.07f, new Vector3(1.15f, 0.05f, length),
            new Color(0.34f, 0.25f, 0.13f));
        road.transform.rotation = Quaternion.LookRotation(delta.normalized);

        int markers = Mathf.Clamp(Mathf.FloorToInt(length / 6f), 1, 7);
        for (int i = 1; i <= markers; i++)
        {
            Vector3 marker = Vector3.Lerp(from, to, i / (float)(markers + 1));
            CreatePrimitive("Roadside Stone", PrimitiveType.Cylinder, marker + Vector3.up * 0.22f,
                new Vector3(0.28f, 0.36f, 0.28f), new Color(0.36f, 0.34f, 0.29f));
        }
    }

    private void BuildForestDistrict(Vector3 center, System.Random random)
    {
        for (int i = 0; i < 9; i++)
        {
            Vector3 position = Scatter(center, random, 3.2f, 5.1f);
            GameObject prefab = i % 3 == 0 ? presentation?.pineTree : presentation?.commonTree;
            if (prefab != null)
                CreatePrefab(prefab, "Forest Tree", position, Vector3.one * Range(random, 0.34f, 0.52f), random.Next(0, 360),
                    i % 3 == 0 ? new Color(0.13f, 0.30f, 0.16f) : new Color(0.20f, 0.42f, 0.18f));
            else
            {
                CreatePrimitive("Forest Trunk", PrimitiveType.Cylinder, position + Vector3.up * 0.8f,
                    new Vector3(0.28f, 1.6f, 0.28f), new Color(0.21f, 0.12f, 0.05f));
                CreatePrimitive("Forest Crown", PrimitiveType.Sphere, position + Vector3.up * 2.1f,
                    Vector3.one * 1.15f, new Color(0.10f, 0.31f, 0.10f));
            }
        }
        CreatePrimitive("Forest Clearing", PrimitiveType.Cylinder, center + new Vector3(2.4f, 0.06f, -2.2f),
            new Vector3(2.5f, 0.035f, 2.5f), new Color(0.28f, 0.22f, 0.12f));
    }

    private void BuildMarshDistrict(Vector3 center, System.Random random)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 pool = Scatter(center, random, 1.6f, 4.5f);
            CreatePrimitive("Marsh Pool", PrimitiveType.Cylinder, pool + Vector3.up * 0.06f,
                new Vector3(Range(random, 2.1f, 3.6f), 0.025f, Range(random, 1.4f, 2.8f)),
                new Color(0.10f, 0.31f, 0.34f));
            for (int reed = 0; reed < 3; reed++)
            {
                Vector3 position = pool + new Vector3(Range(random, -1f, 1f), 0f, Range(random, -0.8f, 0.8f));
                CreatePrimitive("Marsh Reeds", PrimitiveType.Cube, position + Vector3.up * 0.35f,
                    new Vector3(0.08f, 0.7f, 0.08f), new Color(0.35f, 0.43f, 0.12f));
            }
        }
        CreatePrimitive("Marsh Causeway", PrimitiveType.Cube, center + new Vector3(0f, 0.08f, -2.7f),
            new Vector3(1.5f, 0.08f, 6f), new Color(0.33f, 0.27f, 0.17f));
    }

    private void BuildHighlandDistrict(Vector3 center, System.Random random)
    {
        for (int i = 0; i < 6; i++)
        {
            // Boulders sit behind and beside the settlement (never in the front, toward
            // the fixed southward camera) so the hold's buildings stay readable.
            Vector3 position = ScatterBehind(center, random, 2.6f, 5f);
            Vector3 scale = new Vector3(Range(random, 0.8f, 1.3f), Range(random, 0.6f, 1.15f), Range(random, 0.8f, 1.3f));
            if (presentation?.rock != null)
                CreatePrefab(presentation.rock, "Highland Boulder", position, scale, random.Next(0, 360));
            else
                CreatePrimitive("Highland Boulder", PrimitiveType.Cube, position + Vector3.up * scale.y * 0.45f,
                    scale, new Color(0.37f, 0.36f, 0.33f));
        }
        CreatePrimitive("Highland Ridge", PrimitiveType.Cube, center + new Vector3(-2.8f, 0.7f, 1.9f),
            new Vector3(4.5f, 1.4f, 1.2f), new Color(0.34f, 0.33f, 0.30f));
    }

    private void BuildCourtyardDistrict(Vector3 center, System.Random random)
    {
        CreatePrimitive("Courtyard Square", PrimitiveType.Cube, center + Vector3.up * 0.06f,
            new Vector3(7.5f, 0.05f, 7.5f), new Color(0.40f, 0.32f, 0.22f));
        for (int i = 0; i < 4; i++)
        {
            Vector3 position = Scatter(center, random, 2.8f, 4.2f);
            if (presentation?.villageFence != null)
                CreatePrefab(presentation.villageFence, "Courtyard Fence", position, Vector3.one * 0.7f, random.Next(0, 360));
            else
                CreatePrimitive("Courtyard Fence", PrimitiveType.Cube, position + Vector3.up * 0.35f,
                    new Vector3(1.2f, 0.7f, 0.12f), new Color(0.28f, 0.14f, 0.05f));
        }
    }

    private void BuildSettlement(Vector3 center, Territory territory, System.Random random)
    {
        Color bannerColor = territory.Owner == TerritoryOwner.Player
            ? new Color(0.18f, 0.48f, 0.95f) : new Color(0.84f, 0.16f, 0.10f);
        int houses = territory.Settlement == SettlementType.Castle ? 0
            : territory.Settlement == SettlementType.Town ? 5 : 3;
        for (int i = 0; i < houses; i++)
            BuildHouse(center + new Vector3((i % 3 - 1) * 1.2f, 0f, (i / 3 - 0.5f) * 1.8f), i, territory.Settlement);

        if (territory.Settlement == SettlementType.Castle)
            BuildCastle(center, bannerColor);
        else if (territory.Settlement == SettlementType.Town)
        {
            if (presentation?.townHall != null)
                CreatePrefab(presentation.townHall, "Town Hall", center, Vector3.one, 0f, new Color(0.60f, 0.50f, 0.37f));
            else
            {
                CreatePrimitive("Town Hall", PrimitiveType.Cube, center + Vector3.up * 1.25f,
                    new Vector3(1.8f, 2.5f, 1.5f), new Color(0.42f, 0.36f, 0.27f));
                CreatePrimitive("Town Hall Roof", PrimitiveType.Cylinder, center + Vector3.up * 2.7f,
                    new Vector3(2.1f, 0.45f, 2.1f), new Color(0.36f, 0.16f, 0.07f));
            }
        }
        else if (presentation?.villageWagon != null)
            CreatePrefab(presentation.villageWagon, "Village Wagon", center + new Vector3(1.7f, 0f, -1.4f), Vector3.one * 0.75f, random.Next(0, 360));

        if (presentation?.banner != null)
            CreatePrefab(presentation.banner, "Settlement Banner", center + new Vector3(0.5f, 2.2f, 0f),
                Vector3.one * 0.62f, territory.Owner == TerritoryOwner.Enemy ? 180f : 0f, bannerColor);
        else
            CreatePrimitive("Settlement Banner", PrimitiveType.Cube, center + new Vector3(0.5f, 2.2f, 0f),
                new Vector3(0.8f, 1.1f, 0.08f), bannerColor);
    }

    private void BuildHouse(Vector3 position, int index, SettlementType settlement)
    {
        bool large = settlement == SettlementType.Town;
        GameObject prefab = presentation?.House(large);
        if (prefab != null)
        {
            // The authored house models ship near-white; tint to warm timber so they
            // read as buildings on the map rather than blown-out blocks.
            Color timber = large ? new Color(0.62f, 0.50f, 0.36f) : new Color(0.55f, 0.43f, 0.30f);
            CreatePrefab(prefab, "Settlement House", position, Vector3.one, index * 37f, timber);
            return;
        }
        float scale = large ? 0.9f : 0.7f;
        CreatePrimitive("Settlement House", PrimitiveType.Cube, position + Vector3.up * (0.65f * scale),
            new Vector3(1.2f, 1.3f, 1.05f) * scale, new Color(0.46f, 0.36f, 0.24f));
        GameObject roof = CreatePrimitive("Settlement Roof", PrimitiveType.Cube, position + Vector3.up * (1.42f * scale),
            new Vector3(1.35f, 0.28f, 1.2f) * scale, new Color(0.34f, 0.15f, 0.07f));
        roof.transform.rotation = Quaternion.Euler(0f, index * 37f, 32f);
    }

    private void BuildCastle(Vector3 center, Color bannerColor)
    {
        bool authoredKeep = presentation?.castleKeep != null;
        Color stone = new Color(0.52f, 0.52f, 0.50f);
        if (authoredKeep)
            CreatePrefab(presentation.castleKeep, "Castle Keep", center, Vector3.one, 0f, stone);
        else
            CreatePrimitive("Castle Keep", PrimitiveType.Cube, center + Vector3.up * 2f,
                new Vector3(2.7f, 4f, 2.5f), new Color(0.34f, 0.35f, 0.33f));
        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -2.1f : 2.1f;
            float z = i % 2 == 0 ? -1.8f : 1.8f;
            if (presentation?.castleTower != null)
                CreatePrefab(presentation.castleTower, "Castle Tower", center + new Vector3(x, 0f, z), Vector3.one, 0f, stone);
            else
                CreatePrimitive("Castle Tower", PrimitiveType.Cylinder, center + new Vector3(x, 1.6f, z),
                    new Vector3(1.05f, 3.2f, 1.05f), new Color(0.36f, 0.37f, 0.35f));
        }
        // The authored keep ships with its own door; only add the primitive gate for the
        // primitive keep.
        if (!authoredKeep)
            CreatePrimitive("Castle Gate", PrimitiveType.Cube, center + new Vector3(0f, 1f, -2.3f),
                new Vector3(1.5f, 2f, 0.3f), new Color(0.25f, 0.13f, 0.05f));
        CreatePrimitive("Castle Standard", PrimitiveType.Cube, center + new Vector3(0f, 4.5f, 0f),
            new Vector3(0.15f, 3.2f, 0.15f), new Color(0.24f, 0.15f, 0.07f));
        CreatePrimitive("Castle Flag", PrimitiveType.Cube, center + new Vector3(0.5f, 5.5f, 0f),
            new Vector3(1f, 0.8f, 0.08f), bannerColor);
    }

    private GameObject CreatePrefab(GameObject prefab, string name, Vector3 position, Vector3 scale, float yaw, Color? tint = null)
    {
        GameObject instance = Object.Instantiate(prefab, root);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.localScale = scale;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            Object.Destroy(collider);
        if (tint.HasValue)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", tint.Value);
                properties.SetColor("_Color", tint.Value);
                renderer.SetPropertyBlock(properties);
            }
        }
        return instance;
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(root, false);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        Object.Destroy(primitive.GetComponent<Collider>());
        primitive.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        return primitive;
    }

    private static Vector3 WorldOf(Vector2 mapPosition) => new Vector3(mapPosition.x * 1.4f, 0f, mapPosition.y * 1.4f);

    private static Color GroundColor(ArenaType arena) => arena switch
    {
        ArenaType.Forest => new Color(0.13f, 0.31f, 0.12f),
        ArenaType.Marsh => new Color(0.19f, 0.29f, 0.24f),
        ArenaType.Highlands => new Color(0.33f, 0.34f, 0.30f),
        _ => new Color(0.35f, 0.28f, 0.18f)
    };

    private static Vector3 Scatter(Vector3 center, System.Random random, float minRadius, float maxRadius)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float radius = Range(random, minRadius, maxRadius);
        return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    // Scatter confined to the back (+Z) hemisphere, so tall dressing lands behind the
    // settlement and clear of the fixed, southward-looking map camera's sightline.
    private static Vector3 ScatterBehind(Vector3 center, System.Random random, float minRadius, float maxRadius)
    {
        float angle = Range(random, 0.16f, 0.84f) * Mathf.PI; // upper hemisphere → sin > 0
        float radius = Range(random, minRadius, maxRadius);
        return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
}
