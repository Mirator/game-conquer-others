// A tiny, allocation-light deterministic pseudo-random generator (xorshift32) used
// to make AI decisions reproducible: seed it and the same sequence always follows,
// so AI behaviour can be unit-tested and a seeded battle replays identically. Kept
// separate from UnityEngine.Random (which is global, shared, and unseedable per
// fighter). Not cryptographic — only stable and well-distributed enough for gameplay.
public sealed class DeterministicRng
{
    private uint state;

    public DeterministicRng(int seed)
    {
        // 0 is a fixed point of xorshift, so fold it to a non-zero constant.
        state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
    }

    private uint NextUInt()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    // Uniform float in [0, 1).
    public float Value => (NextUInt() & 0xFFFFFFu) / (float)0x1000000;

    // Uniform float in [min, max).
    public float Range(float min, float max) => min + (max - min) * Value;

    // Uniform int in [minInclusive, maxExclusive); returns minInclusive if the range
    // is empty, mirroring UnityEngine.Random.Range(int, int).
    public int Range(int minInclusive, int maxExclusive)
        => maxExclusive <= minInclusive
            ? minInclusive
            : minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
}
