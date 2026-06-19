using UnityEngine;

// The shape allied soldiers arrange in when they are not in melee. Orthogonal to
// the order (stance) in AllyCommand: any shape can be used under Follow, Hold, or
// Advance.
public enum FormationShape { Line, ShieldWall, Skirmish }

// Pure, allocation-free formation geometry: maps a soldier's stable slot index to
// a captain-relative offset (local x = right, +z = ahead of the captain) for each
// shape. No Unity state, so it is trivially unit-testable. Spacing/width come from
// the live-tunable FormationBalance facade.
public static class Formation
{
    public static Vector3 SlotLocalOffset(int index, int count, FormationShape shape)
    {
        int width = Mathf.Max(1, FormationBalance.Width(shape));
        int rank = index / width;
        int column = index % width;
        // Centre each rank, including a partial final rank, so the line stays
        // symmetric about the captain rather than left-justified.
        int rankCount = Mathf.Clamp(count - rank * width, 1, width);
        float centered = column - (rankCount - 1) * 0.5f;
        float side = centered * FormationBalance.SideSpacing(shape);
        float depth = FormationBalance.FrontDepth(shape) - rank * FormationBalance.RankDepth(shape);
        if (shape == FormationShape.Skirmish)
        {
            float jitter = FormationBalance.SkirmishJitter;
            side += Jitter(index, 31) * jitter;
            depth += Jitter(index, 71) * jitter;
        }
        return new Vector3(side, 0f, depth);
    }

    // Deterministic hash -> [-1, 1]. Skirmish offsets must be stable every frame
    // (so soldiers don't vibrate) and reproducible (so tests can assert exact
    // positions), which rules out Random; a per-slot hash gives both.
    private static float Jitter(int index, int salt)
    {
        int hash = (index * 73856093) ^ (salt * 19349663);
        return (hash & 0x7fffffff) % 2001 / 1000f - 1f;
    }
}
