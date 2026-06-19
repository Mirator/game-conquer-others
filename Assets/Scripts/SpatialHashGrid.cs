using System.Collections.Generic;
using UnityEngine;

// A uniform spatial hash over the XZ plane for near-O(1) neighbour queries, so
// per-frame proximity work (separation, telemetry) scales to large battles
// instead of comparing every fighter against every other. The cell size equals
// the query radius, so the 3x3 block around a point contains every neighbour
// within that radius. Cell lists are pooled and reused across rebuilds to stay
// allocation-free after warm-up.
public sealed class SpatialHashGrid
{
    private readonly float inverseCell;
    private readonly Dictionary<long, List<BattleFighter>> cells = new();
    private readonly Stack<List<BattleFighter>> pool = new();

    public SpatialHashGrid(float cellSize)
    {
        inverseCell = 1f / cellSize;
    }

    // Reinserts every alive fighter into its cell. O(n); reuses cell lists.
    public void Rebuild(IReadOnlyList<BattleFighter> fighters)
    {
        foreach (List<BattleFighter> list in cells.Values)
        {
            list.Clear();
            pool.Push(list);
        }
        cells.Clear();
        for (int i = 0; i < fighters.Count; i++)
        {
            BattleFighter fighter = fighters[i];
            if (!fighter.IsAlive)
                continue;
            long key = KeyFor(fighter.transform.position);
            if (!cells.TryGetValue(key, out List<BattleFighter> list))
            {
                list = pool.Count > 0 ? pool.Pop() : new List<BattleFighter>();
                cells[key] = list;
            }
            list.Add(fighter);
        }
    }

    // Fills a caller-owned list with every fighter in the 3x3 cell block around
    // position (the querying fighter itself is included; callers skip it).
    public void QueryNeighbors(Vector3 position, List<BattleFighter> results)
    {
        results.Clear();
        int cx = Mathf.FloorToInt(position.x * inverseCell);
        int cz = Mathf.FloorToInt(position.z * inverseCell);
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (cells.TryGetValue(Pack(cx + dx, cz + dz), out List<BattleFighter> list))
                    results.AddRange(list);
    }

    private long KeyFor(Vector3 position)
        => Pack(Mathf.FloorToInt(position.x * inverseCell), Mathf.FloorToInt(position.z * inverseCell));

    // Packs two cell coordinates into one key: x in the high 32 bits, z in the
    // low 32 (cast through uint to preserve negative bit patterns), so distinct
    // cells never collide.
    private static long Pack(int x, int z) => ((long)x << 32) | (uint)z;
}
