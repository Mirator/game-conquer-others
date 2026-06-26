using UnityEngine;

// Single source of battlefield dimensions. Every movement clamp, spawn lane, wall,
// and scatter range derives from these so the arena can be resized coherently.
public static class ArenaMetrics
{
    // Playable footprint: fighters are clamped inside this rectangle.
    public const float HalfWidth = 26f;
    public const float HalfDepth = 30f;

    // Visual ground plane and the containment boundary just outside the footprint.
    public const float GroundSize = 68f;
    public const float WallOffset = 33f;

    // Team spawn lanes (centre Z of each starting battle line) and the player slot.
    public const float AllySpawnZ = -11f;
    public const float EnemySpawnZ = 12f;
    public const float PlayerSpawnZ = -14f;

    // Spawn safety net so no fighter is ever placed inside the boundary wall.
    public const float SpawnSafeZ = HalfDepth - 0.4f;

    // AI withdrawal: retreat to the friendly edge, held inside the side walls.
    public const float RetreatEdge = 31f;
    public const float RetreatHalfWidth = 27f;

    // Defender structures (hold walls / bandit camp / dressing) sit behind the enemy
    // spawn lane so they never overlap the +z starting line.
    public const float StructureMinZ = 24f;
    public const float HoldWallOffset = 32f;

    // Clamps a position into the playable footprint.
    public static Vector3 Clamp(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, -HalfWidth, HalfWidth);
        position.z = Mathf.Clamp(position.z, -HalfDepth, HalfDepth);
        return position;
    }
}
