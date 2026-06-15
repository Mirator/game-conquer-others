using System;
using System.Collections.Generic;
using UnityEngine;

// Owns generated native assets for the application lifetime so map and battle
// rebuilds reuse a bounded set of materials and procedural audio clips.
public static class RuntimeAssets
{
    private static readonly Dictionary<MaterialKey, Material> materials = new();
    private static readonly Dictionary<string, AudioClip> audioClips = new();
    private static Shader litShader;

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
