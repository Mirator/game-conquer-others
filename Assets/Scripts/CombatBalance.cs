using UnityEngine;

// Central combat tuning access. Values come from a CombatBalanceData asset in
// Resources when present (enabling live in-editor tuning without a rebuild) and
// otherwise fall back to CombatBalanceData's baked defaults. Call sites read
// CombatBalance.X exactly as before.
public static class CombatBalance
{
    private static CombatBalanceData active;

    public static CombatBalanceData Active => active != null
        ? active
        : (active = Resources.Load<CombatBalanceData>(CombatBalanceData.ResourceName)
            ?? ScriptableObject.CreateInstance<CombatBalanceData>());

    // Lets tests or tuning tools swap (or, with null, reset) the active set.
    public static void Override(CombatBalanceData data) => active = data;

    // Stamina
    public static float StaminaRegenBlocking => Active.staminaRegenBlocking;
    public static float StaminaRegenIdle => Active.staminaRegenIdle;
    public static float AttackCostRanged => Active.attackCostRanged;
    public static float AttackCostCounter => Active.attackCostCounter;
    public static float AttackCostTwoHanded => Active.attackCostTwoHanded;
    public static float AttackCostOneHanded => Active.attackCostOneHanded;
    public static float BlockStaminaDamageFactor => Active.blockStaminaDamageFactor;

    // Rhythm
    public static float MeleeAttackCooldown => Active.meleeAttackCooldown;

    // Counter
    public static float CounterDamageMultiplier => Active.counterDamageMultiplier;
    public static float PerfectBlockWindow => Active.perfectBlockWindow;
    public static float CounterWindow => Active.counterWindow;

    // Reach
    public static float SweptStrikeReach => Active.sweptStrikeReach;
    public static float RangeRanged => Active.rangeRanged;
    public static float RangeTwoHanded => Active.rangeTwoHanded;
    public static float RangeOneHanded => Active.rangeOneHanded;

    // Bow precision
    public static float BowPrecisionThreshold => Active.bowPrecisionThreshold;
    public static float BowFullPrecisionTime => Active.bowFullPrecisionTime;
    public static float BowLooseSpreadDegrees => Active.bowLooseSpreadDegrees;
    public static float BowPreciseSpreadDegrees => Active.bowPreciseSpreadDegrees;

    // Movement penalties
    public static float MoveScaleAttackRelease => Active.moveScaleAttackRelease;
    public static float MoveScaleAttacking => Active.moveScaleAttacking;
    public static float MoveScaleBlocking => Active.moveScaleBlocking;

    // Windup (seconds)
    public static float WindupRanged => Active.windupRanged;
    public static float WindupUp => Active.windupUp;
    public static float WindupThrust => Active.windupThrust;
    public static float WindupDefault => Active.windupDefault;
    public static float WindupTwoHandedScale => Active.windupTwoHandedScale;

    // Release (seconds)
    public static float ReleaseRanged => Active.releaseRanged;
    public static float ReleaseThrust => Active.releaseThrust;
    public static float ReleaseDefault => Active.releaseDefault;
    public static float ReleaseTwoHandedScale => Active.releaseTwoHandedScale;

    // Recovery (seconds)
    public static float RecoveryRanged => Active.recoveryRanged;
    public static float RecoveryUp => Active.recoveryUp;
    public static float RecoveryThrust => Active.recoveryThrust;
    public static float RecoveryDefault => Active.recoveryDefault;
    public static float RecoveryTwoHandedScale => Active.recoveryTwoHandedScale;

    // Damage
    public static float DamageRanged => Active.damageRanged;
    public static float DamageUp => Active.damageUp;
    public static float DamageThrust => Active.damageThrust;
    public static float DamageDefault => Active.damageDefault;
    public static float DamageTwoHandedScale => Active.damageTwoHandedScale;

    // Coordination
    public static int MaxPlayerAttackers => Active.maxPlayerAttackers;
    public static int MaxTargetAttackers => Active.maxTargetAttackers;
    public static float SupportEngagementNear => Active.supportEngagementNear;
    public static float SupportEngagementFar => Active.supportEngagementFar;
    public static float SupportSlotSpreadDegrees => Active.supportSlotSpreadDegrees;
    public static float[] SupportAngles => Active.supportAngles;
    public static float SeparationFalloff => Active.separationFalloff;
    public static float SeparationMaxForce => Active.separationMaxForce;
}
