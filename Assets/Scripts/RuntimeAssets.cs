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
        private readonly bool emissive;

        public MaterialKey(Color value, bool glow)
        {
            color = value;
            emissive = glow;
        }

        public bool Equals(MaterialKey other) => color.Equals(other.color) && emissive == other.emissive;
        public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);
        public override int GetHashCode() => color.GetHashCode() * 397 ^ emissive.GetHashCode();
    }

    public static Material Material(Color color, bool emissive = false)
    {
        MaterialKey key = new(color, emissive);
        if (materials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material material = new(FindLitShader()) { color = color };
        material.SetFloat("_Smoothness", 0.18f);
        material.enableInstancing = true;
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }
        materials[key] = material;
        return material;
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
