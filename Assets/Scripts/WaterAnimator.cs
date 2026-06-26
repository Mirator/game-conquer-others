using UnityEngine;

// Scrolls a water material's normal-map offset so its glossy highlights drift like
// ripples — animated water without a custom shader. One animator drives the shared
// water material for the whole arena.
public sealed class WaterAnimator : MonoBehaviour
{
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

    private Material material;
    private Vector2 velocity;

    public void Configure(Material material, Vector2 velocity)
    {
        this.velocity = velocity;
        // Resolve the bump-map property once; if the material lacks it, stay disabled.
        this.material = material != null && material.HasProperty(BumpMap) ? material : null;
        enabled = this.material != null;
    }

    private void Update()
    {
        Vector2 offset = new Vector2(Time.time * velocity.x % 1f, Time.time * velocity.y % 1f);
        material.SetTextureOffset(BumpMap, offset);
    }
}
