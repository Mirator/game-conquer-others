using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws a dense carpet of grass via GPU instancing — thousands of blades in a handful
// of draw calls, far cheaper than one GameObject per tuft. Instances are static (built
// once), so the per-frame cost is just the batched DrawMeshInstanced calls.
public sealed class GrassField : MonoBehaviour
{
    private const int BatchSize = 1023; // Graphics.DrawMeshInstanced hard limit

    private Mesh mesh;
    private Material material;
    private readonly List<Matrix4x4[]> batches = new();

    public void Build(Mesh mesh, Material material, List<Matrix4x4> instances)
    {
        this.mesh = mesh;
        this.material = material;
        batches.Clear();
        for (int i = 0; i < instances.Count; i += BatchSize)
        {
            int n = Mathf.Min(BatchSize, instances.Count - i);
            Matrix4x4[] batch = new Matrix4x4[n];
            instances.CopyTo(i, batch, 0, n);
            batches.Add(batch);
        }
    }

    private void Update()
    {
        if (mesh == null || material == null)
            return;
        foreach (Matrix4x4[] batch in batches)
            Graphics.DrawMeshInstanced(mesh, 0, material, batch, batch.Length, null, ShadowCastingMode.Off, false);
    }
}
