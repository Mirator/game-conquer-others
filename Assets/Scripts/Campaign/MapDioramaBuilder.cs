using UnityEngine;

// Builds the campaign's miniature world: deterministic regional terrain, roads,
// settlement silhouettes, and non-interactive dressing beneath the map markers.
public sealed class MapDioramaBuilder
{
    private readonly Transform root;
    private readonly PresentationCatalog presentation;

    public MapDioramaBuilder(Transform parent)
    {
        root = new GameObject("Campaign Diorama").transform;
        root.SetParent(parent, false);
        presentation = PresentationCatalog.Load();
    }

    public void Build(CampaignState campaign)
    {
        BuildTable();
        foreach (Territory territory in campaign.Territories)
            BuildDistrict(territory, campaign.Seed);
        foreach (Territory territory in campaign.Territories)
            foreach (int adjacent in territory.AdjacentIds)
                if (adjacent > territory.Id)
                    BuildRoad(territory, campaign.GetById(adjacent));
    }

    private void BuildTable()
    {
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Map Table";
        table.transform.SetParent(root, false);
        table.transform.position = new Vector3(0f, -0.5f, 2f);
        table.transform.localScale = new Vector3(92f, 1f, 76f);
        table.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.11f, 0.14f, 0.11f));
    }

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
                CreatePrefab(prefab, "Forest Tree", position, Vector3.one * Range(random, 0.34f, 0.52f), random.Next(0, 360));
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
        for (int i = 0; i < 7; i++)
        {
            Vector3 position = Scatter(center, random, 1.7f, 4.8f);
            Vector3 scale = new Vector3(Range(random, 0.9f, 1.7f), Range(random, 0.65f, 1.35f), Range(random, 0.9f, 1.7f));
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
            CreatePrimitive("Town Hall", PrimitiveType.Cube, center + Vector3.up * 1.25f,
                new Vector3(1.8f, 2.5f, 1.5f), new Color(0.42f, 0.36f, 0.27f));
            CreatePrimitive("Town Hall Roof", PrimitiveType.Cylinder, center + Vector3.up * 2.7f,
                new Vector3(2.1f, 0.45f, 2.1f), new Color(0.36f, 0.16f, 0.07f));
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
        float scale = settlement == SettlementType.Town ? 0.9f : 0.7f;
        CreatePrimitive("Settlement House", PrimitiveType.Cube, position + Vector3.up * (0.65f * scale),
            new Vector3(1.2f, 1.3f, 1.05f) * scale, new Color(0.46f, 0.36f, 0.24f));
        GameObject roof = CreatePrimitive("Settlement Roof", PrimitiveType.Cube, position + Vector3.up * (1.42f * scale),
            new Vector3(1.35f, 0.28f, 1.2f) * scale, new Color(0.34f, 0.15f, 0.07f));
        roof.transform.rotation = Quaternion.Euler(0f, index * 37f, 32f);
    }

    private void BuildCastle(Vector3 center, Color bannerColor)
    {
        CreatePrimitive("Castle Keep", PrimitiveType.Cube, center + Vector3.up * 2f,
            new Vector3(2.7f, 4f, 2.5f), new Color(0.34f, 0.35f, 0.33f));
        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -2.1f : 2.1f;
            float z = i % 2 == 0 ? -1.8f : 1.8f;
            CreatePrimitive("Castle Tower", PrimitiveType.Cylinder, center + new Vector3(x, 1.6f, z),
                new Vector3(1.05f, 3.2f, 1.05f), new Color(0.36f, 0.37f, 0.35f));
        }
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

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
}
