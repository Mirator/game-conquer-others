using UnityEngine;

// Serialized combat tuning. Defaults below are the authoritative values; create
// an instance in a Resources folder (Conquer Others > Create Combat Balance
// Asset, or Assets > Create > Conquer Others > Combat Balance named
// "CombatBalance") to override them and tune live in the inspector during play.
[CreateAssetMenu(menuName = "Conquer Others/Combat Balance", fileName = "CombatBalance")]
public sealed class CombatBalanceData : ScriptableObject
{
    public const string ResourceName = "CombatBalance";

    [Header("Stamina")]
    public float staminaRegenBlocking = 12f;
    public float staminaRegenIdle = 25f;
    public float attackCostRanged = 12f;
    public float attackCostCounter = 10f;
    public float attackCostTwoHanded = 24f;
    public float attackCostOneHanded = 18f;
    // Stamina spent per point of damage absorbed by a (non-perfect) block.
    public float blockStaminaDamageFactor = 0.55f;

    [Header("Rhythm")]
    // Brief settle after a melee swing's recovery before the next swing may begin,
    // so a high-stamina fighter has weight instead of machine-gunning attacks.
    // Counters and ranged shots are exempt.
    public float meleeAttackCooldown = 0.16f;

    [Header("Counter")]
    public float counterDamageMultiplier = 1.45f;
    // A guard lands a perfect block only within this window after raising/redirecting
    // it; a perfect block then opens the counter window for this long.
    public float perfectBlockWindow = 0.2f;
    public float counterWindow = 0.65f;

    [Header("Reach (metres)")]
    // Swept-strike sample reach (the thrust strike-path endpoint) and per-weapon
    // preferred standoff used by movement/AI.
    public float sweptStrikeReach = 2.2f;
    public float rangeRanged = 10f;
    public float rangeTwoHanded = 2.35f;
    public float rangeOneHanded = 1.8f;

    [Header("Bow precision")]
    // Draw seconds to reach the precision threshold (spread starts tightening) and to
    // reach full precision, plus the loose/precise aim cone in degrees.
    public float bowPrecisionThreshold = 0.7f;
    public float bowFullPrecisionTime = 1.4f;
    public float bowLooseSpreadDegrees = 7.5f;
    public float bowPreciseSpreadDegrees = 0.25f;

    [Header("Movement penalties")]
    // Movement speed multipliers while attacking (release vs the rest of the swing)
    // and while blocking, so committing to a strike or a guard slows the fighter.
    public float moveScaleAttackRelease = 0.45f;
    public float moveScaleAttacking = 0.62f;
    public float moveScaleBlocking = 0.58f;

    [Header("Windup (seconds)")]
    public float windupRanged = 0.62f;
    public float windupUp = 0.5f;
    public float windupThrust = 0.3f;
    public float windupDefault = 0.35f;
    public float windupTwoHandedScale = 1.22f;

    [Header("Release (seconds)")]
    public float releaseRanged = 0.08f;
    public float releaseThrust = 0.2f;
    public float releaseDefault = 0.25f;
    public float releaseTwoHandedScale = 1.08f;

    [Header("Recovery (seconds)")]
    public float recoveryRanged = 0.42f;
    public float recoveryUp = 0.6f;
    public float recoveryThrust = 0.4f;
    public float recoveryDefault = 0.45f;
    public float recoveryTwoHandedScale = 1.25f;

    [Header("Damage")]
    public float damageRanged = 32f;
    public float damageUp = 35f;
    public float damageThrust = 20f;
    public float damageDefault = 25f;
    public float damageTwoHandedScale = 1.38f;

    [Header("Coordination")]
    // Active-attacker limits: at most this many AI may actively attack the player /
    // any other single target at once; the rest circle or seek another opponent.
    public int maxPlayerAttackers = 1;
    public int maxTargetAttackers = 2;
    // Non-active supporters hold between these distances from their target, fanned
    // across the support angles below (the spread offsets each overflow ring so a
    // crowd does not stack on one arc).
    public float supportEngagementNear = 2.8f;
    public float supportEngagementFar = 3.5f;
    public float supportSlotSpreadDegrees = 18f;
    public float[] supportAngles = { -72f, 72f, -138f, 138f, 180f, -105f, 105f };
    // Separation-force shaping (the 2.5m onset radius is fixed to the spatial-hash
    // cell size in BattleTactics and is intentionally not tunable here).
    public float separationFalloff = 1.8f;
    public float separationMaxForce = 1.4f;
}
