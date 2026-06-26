using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Persistent battlefield ground decals — blood splats, trample scuffs, and dropped-
// gear debris — as flat quads laid on the terrain. Capped and recycled (oldest first)
// so a long battle never accumulates unbounded geometry.
public sealed class BattleDecals : MonoBehaviour
{
    private const int MaxDecals = 64;

    private readonly Queue<GameObject> decals = new();

    public void AddBlood(Vector3 position, float scale) => Add(RuntimeAssets.BloodMaterial(), position, scale);
    public void AddScuff(Vector3 position, float scale) => Add(RuntimeAssets.ScuffMaterial(), position, scale);
    public void AddDebris(Vector3 position) => Add(RuntimeAssets.DebrisMaterial(), position, Random.Range(0.7f, 1.1f));

    // Trampled ground where the lines are about to clash, around the centre of the field.
    public void SeedClashZone()
    {
        for (int i = 0; i < 8; i++)
            AddScuff(new Vector3(Random.Range(-11f, 11f), 0f, Random.Range(-7f, 7f)), Random.Range(2.5f, 4.5f));
    }

    private void Add(Material material, Vector3 position, float scale)
    {
        if (material == null)
            return;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Decal";
        quad.transform.SetParent(transform);
        Destroy(quad.GetComponent<Collider>());
        float y = GroundHeight(position) + Random.Range(0.02f, 0.05f);
        quad.transform.position = new Vector3(position.x, y, position.z);
        quad.transform.rotation = Quaternion.Euler(-90f, Random.Range(0f, 360f), 0f);
        quad.transform.localScale = Vector3.one * scale;
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        decals.Enqueue(quad);
        if (decals.Count > MaxDecals)
        {
            GameObject oldest = decals.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    // Drops the decal onto the terrain surface (raycast ignores fighters, which live on
    // the Ignore Raycast layer), falling back to the supplied height.
    private static float GroundHeight(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f))
            return hit.point.y;
        return position.y;
    }
}
