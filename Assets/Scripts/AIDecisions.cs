// Pure, stochastic AI offense decisions extracted from AIFighter so they can be unit
// tested with a seeded DeterministicRng instead of the global UnityEngine.Random.
// These pick *which* attack line to throw; the surrounding movement, timing, and
// permission logic stays in AIFighter. All randomness flows through the supplied rng.
public static class AIDecisions
{
    // A uniformly random one of the four attack lines.
    public static CombatDirection RandomDirection(DeterministicRng rng) => (CombatDirection)rng.Range(0, 4);

    // A random line that is NOT the given one — used to feint around a guard or to
    // deliberately mis-guard.
    public static CombatDirection RandomWrongDirection(CombatDirection incoming, DeterministicRng rng)
    {
        CombatDirection result;
        do result = RandomDirection(rng);
        while (result == incoming);
        return result;
    }

    // Punishing a recovering target favours the heavy overhead but mixes in other
    // lines so a sharp opponent can't pre-guard a single predictable answer.
    public static CombatDirection RandomPunishDirection(DeterministicRng rng)
    {
        float roll = rng.Value;
        if (roll < 0.5f)
            return CombatDirection.Up;
        if (roll < 0.75f)
            return CombatDirection.Thrust;
        return rng.Value < 0.5f ? CombatDirection.Left : CombatDirection.Right;
    }

    // The attack line to throw at a target: feint around its guard if it is blocking,
    // punish it if it is recovering, otherwise a fresh random line. Mirrors the prior
    // AIFighter.ChooseAttackDirection exactly, with state passed in for testability.
    public static CombatDirection ChooseAttackDirection(AIProfile profile, bool targetBlocking,
        CombatDirection targetBlockDirection, bool targetRecovering, DeterministicRng rng)
    {
        if (targetBlocking && rng.Value < profile.feintChance)
            return RandomWrongDirection(targetBlockDirection, rng);
        if (targetRecovering && rng.Value < profile.recoveryPunishChance)
            return RandomPunishDirection(rng);
        return RandomDirection(rng);
    }
}
