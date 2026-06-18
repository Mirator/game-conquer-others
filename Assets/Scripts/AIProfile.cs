// Behavioral parameters that give an AI fighter a distinct fighting personality,
// decoupled from its unit stats and weapon. The Default preset reproduces the
// original baseline behavior, so an unassigned fighter is unchanged. Archetype
// presets (shieldbearer, berserker, archer, captain) are layered on top.
public sealed class AIProfile
{
    public float aggression = 0.92f;          // center value; higher shortens attack cooldowns
    public float aggressionJitter = 0.16f;    // +/- organic variation rolled per fighter
    public float rangeScale = 1f;             // multiplies the weapon-based preferred range
    public float blockChance = 1f;            // chance to raise a guard when threatened
    public float blockCorrectChanceVsPlayer = 0.62f;
    public float blockCorrectChanceVsAi = 0.52f;
    public float feintChance = 0.72f;         // strike a blocking target's open side
    public float recoveryPunishChance = 0.55f; // overhead a recovering target
    public float retreatBravery = 1f;         // >1 holds longer; very high never retreats

    // Baseline behavior, identical to the pre-archetype AI.
    public static AIProfile Default() => new AIProfile();

    // Balanced line infantry; the baseline personality.
    public static AIProfile Soldier() => new AIProfile();

    // Patient turtle: guards constantly and skillfully, attacks slowly, holds
    // the line, and punishes mistakes rather than pressing.
    public static AIProfile Shieldbearer() => new AIProfile
    {
        aggression = 0.7f,
        rangeScale = 0.95f,
        blockChance = 1f,
        blockCorrectChanceVsPlayer = 0.82f,
        blockCorrectChanceVsAi = 0.74f,
        feintChance = 0.5f,
        recoveryPunishChance = 0.45f,
        retreatBravery = 1.5f
    };

    // Relentless aggressor: rarely guards, attacks fast, closes in, punishes
    // recovery hard, and fights almost to the death.
    public static AIProfile Berserker() => new AIProfile
    {
        aggression = 1.4f,
        aggressionJitter = 0.2f,
        rangeScale = 0.9f,
        blockChance = 0.2f,
        blockCorrectChanceVsPlayer = 0.4f,
        blockCorrectChanceVsAi = 0.35f,
        feintChance = 0.6f,
        recoveryPunishChance = 0.8f,
        retreatBravery = 6f
    };

    // Ranged skirmisher: never melee-guards (handled by ranged logic) and is
    // quicker to fall back when pressured.
    public static AIProfile Archer() => new AIProfile
    {
        aggression = 0.95f,
        blockChance = 0f,
        retreatBravery = 0.7f
    };

    // Elite duelist and morale anchor: skilled defense, tricky offense, and
    // holds the line long after lesser fighters would break.
    public static AIProfile Captain() => new AIProfile
    {
        aggression = 1.05f,
        blockChance = 1f,
        blockCorrectChanceVsPlayer = 0.85f,
        blockCorrectChanceVsAi = 0.78f,
        feintChance = 0.8f,
        recoveryPunishChance = 0.7f,
        retreatBravery = 8f
    };
}
