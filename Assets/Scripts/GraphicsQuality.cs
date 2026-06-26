using UnityEngine;

// Maps the active Unity quality level to coarse tiers so heavy presentation
// (post-processing intensity, scatter density, shadow distance) can scale down on
// weak hardware while the gameplay-critical arena layout stays identical.
public enum GraphicsTier
{
    Low,
    Medium,
    High
}

public static class GraphicsQuality
{
    public static GraphicsTier Tier
    {
        get
        {
            int count = Mathf.Max(1, QualitySettings.names.Length);
            if (count <= 1)
                return GraphicsTier.High;
            int level = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, count - 1);
            float t = level / (float)(count - 1);
            if (t < 0.34f)
                return GraphicsTier.Low;
            return t < 0.67f ? GraphicsTier.Medium : GraphicsTier.High;
        }
    }

    public static bool IsLow => Tier == GraphicsTier.Low;
    public static bool IsHigh => Tier == GraphicsTier.High;

    // Density multiplier for scatter / ground-clutter counts.
    public static float ScatterScale => Tier switch
    {
        GraphicsTier.Low => 0.35f,
        GraphicsTier.Medium => 0.7f,
        _ => 1f
    };

    // Shadow draw distance scaled to fit the larger field without crushing the
    // shadow map resolution on low presets.
    public static float ShadowDistance => Tier switch
    {
        GraphicsTier.Low => 45f,
        GraphicsTier.Medium => 70f,
        _ => 95f
    };
}
