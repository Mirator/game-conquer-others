using System;
using System.Collections.Generic;
using UnityEngine;

// Owns generated native assets for the application lifetime so map and battle
// rebuilds reuse a bounded set of materials and procedural audio clips.
public static class RuntimeAssets
{
    private static readonly Dictionary<MaterialKey, Material> materials = new();
    private static readonly Dictionary<string, AudioClip> audioClips = new();
    private static readonly Dictionary<ArenaType, Material> skyboxes = new();
    private static readonly Dictionary<int, Material> groundMaterials = new();
    private static Shader litShader;

    // A per-region procedural skybox, cached for the app lifetime. Tint/ground are
    // baked per arena; the bootstrap modulates _Exposure by time of day each build.
    public static Material Skybox(ArenaType arena)
    {
        if (skyboxes.TryGetValue(arena, out Material cached) && cached != null)
            return cached;
        Shader shader = Shader.Find("Skybox/Procedural");
        Material sky = shader != null ? new Material(shader) : null;
        if (sky != null)
        {
            (Color tint, Color ground) = arena switch
            {
                ArenaType.Forest => (new Color(0.46f, 0.55f, 0.45f), new Color(0.12f, 0.14f, 0.08f)),
                ArenaType.Marsh => (new Color(0.50f, 0.56f, 0.58f), new Color(0.15f, 0.17f, 0.16f)),
                ArenaType.Highlands => (new Color(0.50f, 0.58f, 0.72f), new Color(0.20f, 0.20f, 0.18f)),
                _ => (new Color(0.60f, 0.62f, 0.66f), new Color(0.20f, 0.18f, 0.14f))
            };
            sky.SetColor("_SkyTint", tint);
            sky.SetColor("_GroundColor", ground);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetFloat("_Exposure", 1f);
        }
        skyboxes[arena] = sky;
        return sky;
    }

    private readonly struct MaterialKey : IEquatable<MaterialKey>
    {
        private readonly Color32 color;
        private readonly byte smoothness;
        private readonly byte metallic;
        private readonly bool emissive;

        public MaterialKey(Color value, byte smooth, byte metal, bool glow)
        {
            color = value;
            smoothness = smooth;
            metallic = metal;
            emissive = glow;
        }

        public bool Equals(MaterialKey other) => color.Equals(other.color) && smoothness == other.smoothness
            && metallic == other.metallic && emissive == other.emissive;
        public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);
        public override int GetHashCode()
        {
            int hash = color.GetHashCode() * 397 ^ emissive.GetHashCode();
            return hash * 397 ^ (smoothness | (metallic << 8));
        }
    }

    // Matte default surface (used by the bulk of arena/UI geometry).
    public static Material Material(Color color, bool emissive = false) => Material(color, 0.18f, 0f, emissive);

    // Tunable PBR surface for the few materials that benefit from a metal/wet look.
    // Results are cached on quantized smoothness/metallic so instancing still batches.
    public static Material Material(Color color, float smoothness, float metallic, bool emissive = false)
    {
        byte s = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(smoothness) * 255f), 0, 255);
        byte m = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(metallic) * 255f), 0, 255);
        MaterialKey key = new(color, s, m, emissive);
        if (materials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material material = new(FindLitShader()) { color = color };
        material.SetFloat("_Smoothness", s / 255f);
        material.SetFloat("_Metallic", m / 255f);
        material.enableInstancing = true;
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }
        materials[key] = material;
        return material;
    }

    // A tiling, mottled ground material backed by a runtime-generated seamless noise
    // texture, so the large ground plane reads as textured terrain rather than a flat
    // colour. Cached for the app lifetime (the texture is generated once per palette).
    public static Material GroundMaterial(Color baseColor, Color speckle, float tiling, bool wet = false)
    {
        int key = baseColor.GetHashCode() * 397 ^ speckle.GetHashCode() ^ Mathf.RoundToInt(tiling) * 7919 ^ (wet ? 31 : 0);
        if (groundMaterials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        // Rain/mist darkens the ground and makes it glossy (a wet sheen).
        Color albedoBase = wet ? baseColor * 0.78f : baseColor;
        Color albedoSpeckle = wet ? speckle * 0.78f : speckle;
        Texture2D texture = GenerateGroundTexture(albedoBase, albedoSpeckle);
        Vector2 scale = new(tiling, tiling);
        Material material = new(FindLitShader()) { color = Color.white, mainTexture = texture };
        material.mainTextureScale = scale;
        material.SetFloat("_Smoothness", wet ? 0.55f : 0.08f);
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
        }
        if (material.HasProperty("_BumpMap"))
        {
            material.EnableKeyword("_NORMALMAP");
            Texture2D normal = GenerateNormalMap(GroundNoise, 2.2f);
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BumpMap", scale);
            material.SetFloat("_BumpScale", 0.7f);
        }
        material.enableInstancing = true;
        groundMaterials[key] = material;
        return material;
    }

    private static Material waterMaterial;

    // A glossy water material with an animated-friendly ripple normal map. Scrolling
    // its _BumpMap offset (see WaterAnimator) gives moving highlights without a shader.
    public static Material WaterMaterial()
    {
        if (waterMaterial != null)
            return waterMaterial;
        Material material = new(FindLitShader()) { color = new Color(0.14f, 0.33f, 0.39f) };
        material.SetFloat("_Smoothness", 0.92f);
        material.SetFloat("_Metallic", 0.1f);
        if (material.HasProperty("_BumpMap"))
        {
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_BumpMap", GenerateNormalMap(RippleNoise, 1.6f));
            material.SetTextureScale("_BumpMap", new Vector2(5f, 5f));
            material.SetFloat("_BumpScale", 0.5f);
        }
        material.enableInstancing = true;
        waterMaterial = material;
        return material;
    }

    private static Material particleSoftMaterial;

    // A transparent, soft-edged particle material (Sprites/Default is always included
    // and alpha-blends), tinted per-particle by the system's start colour. Used for
    // rain/snow/mist so they read as soft precipitation rather than hard dots.
    public static Material SoftParticleMaterial()
    {
        if (particleSoftMaterial != null)
            return particleSoftMaterial;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? FindLitShader();
        Material material = new(shader) { mainTexture = GenerateSoftDot() };
        material.enableInstancing = true;
        particleSoftMaterial = material;
        return material;
    }

    private static Texture2D GenerateSoftDot()
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    private static Mesh grassMesh;
    private static Material grassMaterial;

    // A tiny three-quad "blade cross" mesh drawn instanced thousands of times to carpet
    // the field with grass at negligible draw-call cost.
    public static Mesh GrassMesh()
    {
        if (grassMesh != null)
            return grassMesh;
        const float w = 0.18f;
        const float h = 0.55f;
        Vector3[] verts = new Vector3[12];
        Vector2[] uvs = new Vector2[12];
        int[] tris = new int[18];
        for (int q = 0; q < 3; q++)
        {
            float a = q * Mathf.PI / 3f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * w;
            int v = q * 4;
            verts[v + 0] = -dir;
            verts[v + 1] = dir;
            verts[v + 2] = dir + Vector3.up * h;
            verts[v + 3] = -dir + Vector3.up * h;
            uvs[v + 0] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(1f, 0f);
            uvs[v + 2] = new Vector2(1f, 1f);
            uvs[v + 3] = new Vector2(0f, 1f);
            int t = q * 6;
            tris[t + 0] = v;
            tris[t + 1] = v + 2;
            tris[t + 2] = v + 1;
            tris[t + 3] = v;
            tris[t + 4] = v + 3;
            tris[t + 5] = v + 2;
        }
        grassMesh = new Mesh { name = "Grass Blade" };
        grassMesh.vertices = verts;
        grassMesh.uv = uvs;
        grassMesh.triangles = tris;
        grassMesh.RecalculateNormals();
        grassMesh.RecalculateBounds();
        return grassMesh;
    }

    // Double-sided, instancing-enabled grass material with a root-to-tip gradient.
    public static Material GrassMaterial()
    {
        if (grassMaterial != null)
            return grassMaterial;
        const int h = 16;
        Texture2D gradient = new(1, h, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        Color root = new(0.10f, 0.22f, 0.07f);
        Color tip = new(0.32f, 0.52f, 0.18f);
        Color[] pixels = new Color[h];
        for (int y = 0; y < h; y++)
            pixels[y] = Color.Lerp(root, tip, y / (float)(h - 1));
        gradient.SetPixels(pixels);
        gradient.Apply(false);
        grassMaterial = new(FindLitShader()) { color = Color.white, mainTexture = gradient };
        grassMaterial.SetFloat("_Smoothness", 0.1f);
        if (grassMaterial.HasProperty("_BaseMap"))
            grassMaterial.SetTexture("_BaseMap", gradient);
        if (grassMaterial.HasProperty("_Cull"))
            grassMaterial.SetFloat("_Cull", 0f); // double-sided blades
        grassMaterial.enableInstancing = true;
        return grassMaterial;
    }

    private static Material bloodMaterial;
    private static Material scuffMaterial;
    private static Material debrisMaterial;

    // Flat ground decals (blood splats, trample scuffs, dropped-gear debris) on a
    // transparent unlit material, baked colour-and-alpha so a shared material batches.
    public static Material BloodMaterial() => bloodMaterial ??=
        DecalMaterial(new Color(0.34f, 0.02f, 0.01f), 0.85f, 0.55f, false);
    public static Material ScuffMaterial() => scuffMaterial ??=
        DecalMaterial(new Color(0.17f, 0.13f, 0.09f), 0.4f, 0.35f, false);
    public static Material DebrisMaterial() => debrisMaterial ??=
        DecalMaterial(new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.25f, true);

    private static Material DecalMaterial(Color color, float maxAlpha, float noise, bool ring)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? FindLitShader();
        return new Material(shader) { mainTexture = GenerateDecalTexture(color, maxAlpha, noise, ring) };
    }

    private static Texture2D GenerateDecalTexture(Color color, float maxAlpha, float noiseAmount, bool ring)
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 centre .. 1 edge
                float n = (Mathf.PerlinNoise(x * 0.2f, y * 0.2f) - 0.5f) * noiseAmount;
                float mask = ring ? 1f - Mathf.Abs(d - 0.7f) * 4f : 1f - d;
                mask = Mathf.Clamp01(mask + n);
                pixels[y * size + x] = new Color(color.r, color.g, color.b, mask * mask * maxAlpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    // The [0,1] surface field shared by the ground albedo and its normal map, so the
    // mottling and the per-texel bumps line up.
    private static float GroundNoise(float u, float v)
        => TileableNoise(u, v, 4f) * 0.6f + TileableNoise(u, v, 12f) * 0.4f;

    private static Texture2D GenerateGroundTexture(Color baseColor, Color speckle)
    {
        const int size = 256;
        Texture2D texture = new(size, size, TextureFormat.RGB24, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                pixels[y * size + x] = Color.Lerp(baseColor, speckle, Mathf.Clamp01(GroundNoise(u, v)));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    // Finer, higher-frequency field for water ripples.
    private static float RippleNoise(float u, float v)
        => TileableNoise(u, v, 8f) * 0.5f + TileableNoise(u, v, 18f) * 0.5f;

    // Tangent-space normal map derived from a [0,1] height field via finite
    // differences, encoded in the AG layout URP's UnpackNormal expects for runtime
    // textures. Shared by the ground and the water ripple surface.
    private static Texture2D GenerateNormalMap(Func<float, float, float> field, float strength)
    {
        const int size = 256;
        float step = 1f / size;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float dx = (field(u - step, v) - field(u + step, v)) * strength;
                float dy = (field(u, v - step) - field(u, v + step)) * strength;
                Vector3 n = new Vector3(dx, dy, 1f).normalized;
                // URP UnpackNormal reads x from A and y from G; reconstruct z from those.
                pixels[y * size + x] = new Color(1f, n.y * 0.5f + 0.5f, 1f, n.x * 0.5f + 0.5f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    // Seamlessly tiling value in [0,1] from Unity's non-periodic Perlin noise, via the
    // standard four-sample bilinear wrap blend over the unit square.
    private static float TileableNoise(float u, float v, float freq)
    {
        float x = u * freq;
        float y = v * freq;
        float n =
            Mathf.PerlinNoise(x, y) * (freq - x) * (freq - y) +
            Mathf.PerlinNoise(x - freq, y) * x * (freq - y) +
            Mathf.PerlinNoise(x, y - freq) * (freq - x) * y +
            Mathf.PerlinNoise(x - freq, y - freq) * x * y;
        return n / (freq * freq);
    }

    public static AudioClip Audio(string key, Func<AudioClip> factory)
    {
        if (audioClips.TryGetValue(key, out AudioClip cached) && cached != null)
            return cached;
        AudioClip clip = factory();
        clip.name = key;
        audioClips[key] = clip;
        return clip;
    }

    private static Shader FindLitShader()
    {
        if (litShader != null)
            return litShader;
        litShader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        litShader ??= Shader.Find("Legacy Shaders/Diffuse");
        litShader ??= Shader.Find("Sprites/Default");
        return litShader;
    }
}
