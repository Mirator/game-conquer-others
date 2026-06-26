using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds the cinematic post-processing stack at runtime — URP needs a Volume in
// the scene and per-camera post-processing enabled, neither of which exists in a
// from-primitives world. A global Volume drives ACES tonemapping, bloom, a light
// colour grade, and a vignette; heavy effects scale with the quality tier. SSAO is
// already a renderer feature and activates once post-processing is on.
public static class BattlePostProcessing
{
    // Enables post-processing on the camera and ensures one global Volume exists
    // under the supplied parent (so it is torn down with the rest of the scene).
    public static void Apply(Camera camera, Transform parent)
    {
        if (camera == null)
            return;

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderPostProcessing = true;
            data.antialiasing = GraphicsQuality.IsLow
                ? AntialiasingMode.None
                : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
        }

        BuildGlobalVolume(parent);
    }

    private static void BuildGlobalVolume(Transform parent)
    {
        GameObject go = new GameObject("Post Processing Volume");
        if (parent != null)
            go.transform.SetParent(parent, false);

        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;

        Tonemapping tonemapping = profile.Add<Tonemapping>();
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = profile.Add<ColorAdjustments>();
        color.postExposure.Override(0.1f);
        color.contrast.Override(12f);
        color.saturation.Override(8f);

        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(GraphicsQuality.IsLow ? 0.18f : 0.28f);
        vignette.smoothness.Override(0.4f);

        if (!GraphicsQuality.IsLow)
        {
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(GraphicsQuality.IsHigh ? 0.9f : 0.5f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));
        }
    }
}
