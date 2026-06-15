using UnityEngine;

// Central combat tuning numbers extracted verbatim from BattleFighter. Values
// are unchanged; this is the single place to adjust melee/bow feel. Promoting
// this to a ScriptableObject later enables in-editor tuning without a rebuild.
public static class CombatBalance
{
    // Stamina
    public const float StaminaRegenBlocking = 12f;
    public const float StaminaRegenIdle = 25f;
    public const float AttackCostRanged = 12f;
    public const float AttackCostCounter = 10f;
    public const float AttackCostTwoHanded = 24f;
    public const float AttackCostOneHanded = 18f;

    // Counter
    public const float CounterDamageMultiplier = 1.45f;

    // Windup (seconds)
    public const float WindupRanged = 0.62f;
    public const float WindupUp = 0.5f;
    public const float WindupThrust = 0.3f;
    public const float WindupDefault = 0.35f;
    public const float WindupTwoHandedScale = 1.22f;

    // Release (seconds)
    public const float ReleaseRanged = 0.08f;
    public const float ReleaseThrust = 0.2f;
    public const float ReleaseDefault = 0.25f;
    public const float ReleaseTwoHandedScale = 1.08f;

    // Recovery (seconds)
    public const float RecoveryRanged = 0.42f;
    public const float RecoveryUp = 0.6f;
    public const float RecoveryThrust = 0.4f;
    public const float RecoveryDefault = 0.45f;
    public const float RecoveryTwoHandedScale = 1.25f;

    // Damage
    public const float DamageRanged = 32f;
    public const float DamageUp = 35f;
    public const float DamageThrust = 20f;
    public const float DamageDefault = 25f;
    public const float DamageTwoHandedScale = 1.38f;
}
