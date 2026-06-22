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
}
