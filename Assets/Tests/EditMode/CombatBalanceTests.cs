using NUnit.Framework;
using UnityEngine;

// Pins the baked combat-tuning defaults and the CombatBalance facade passthrough.
// These are a regression net for tuning: a stray edit to a windup, damage, stamina
// cost, block/counter window, reach, bow-precision, or movement-penalty value fails
// here instead of silently shipping. Defaults are read from a fresh instance so the
// test is independent of any optional Resources/CombatBalance override.
public sealed class CombatBalanceTests
{
    private CombatBalanceData defaults;

    [SetUp]
    public void SetUp() => defaults = ScriptableObject.CreateInstance<CombatBalanceData>();

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(defaults);
        CombatBalance.Override(null); // ensure later tests see the real defaults/asset
    }

    [Test]
    public void StaminaDefaults_MatchSpec()
    {
        Assert.AreEqual(12f, defaults.staminaRegenBlocking, 1e-4f);
        Assert.AreEqual(25f, defaults.staminaRegenIdle, 1e-4f);
        Assert.AreEqual(12f, defaults.attackCostRanged, 1e-4f);
        Assert.AreEqual(10f, defaults.attackCostCounter, 1e-4f);
        Assert.AreEqual(24f, defaults.attackCostTwoHanded, 1e-4f);
        Assert.AreEqual(18f, defaults.attackCostOneHanded, 1e-4f);
        Assert.AreEqual(0.55f, defaults.blockStaminaDamageFactor, 1e-4f);
    }

    [Test]
    public void RhythmAndCounterDefaults_MatchSpec()
    {
        Assert.AreEqual(0.16f, defaults.meleeAttackCooldown, 1e-4f);
        Assert.AreEqual(1.45f, defaults.counterDamageMultiplier, 1e-4f);
        Assert.AreEqual(0.2f, defaults.perfectBlockWindow, 1e-4f);
        Assert.AreEqual(0.65f, defaults.counterWindow, 1e-4f);
    }

    [Test]
    public void WindupDefaults_MatchSpec()
    {
        Assert.AreEqual(0.62f, defaults.windupRanged, 1e-4f);
        Assert.AreEqual(0.5f, defaults.windupUp, 1e-4f);
        Assert.AreEqual(0.3f, defaults.windupThrust, 1e-4f);
        Assert.AreEqual(0.35f, defaults.windupDefault, 1e-4f);
        Assert.AreEqual(1.22f, defaults.windupTwoHandedScale, 1e-4f);
    }

    [Test]
    public void ReleaseDefaults_MatchSpec()
    {
        Assert.AreEqual(0.08f, defaults.releaseRanged, 1e-4f);
        Assert.AreEqual(0.2f, defaults.releaseThrust, 1e-4f);
        Assert.AreEqual(0.25f, defaults.releaseDefault, 1e-4f);
        Assert.AreEqual(1.08f, defaults.releaseTwoHandedScale, 1e-4f);
    }

    [Test]
    public void RecoveryDefaults_MatchSpec()
    {
        Assert.AreEqual(0.42f, defaults.recoveryRanged, 1e-4f);
        Assert.AreEqual(0.6f, defaults.recoveryUp, 1e-4f);
        Assert.AreEqual(0.4f, defaults.recoveryThrust, 1e-4f);
        Assert.AreEqual(0.45f, defaults.recoveryDefault, 1e-4f);
        Assert.AreEqual(1.25f, defaults.recoveryTwoHandedScale, 1e-4f);
    }

    [Test]
    public void DamageDefaults_MatchSpec()
    {
        Assert.AreEqual(32f, defaults.damageRanged, 1e-4f);
        Assert.AreEqual(35f, defaults.damageUp, 1e-4f);
        Assert.AreEqual(20f, defaults.damageThrust, 1e-4f);
        Assert.AreEqual(25f, defaults.damageDefault, 1e-4f);
        Assert.AreEqual(1.38f, defaults.damageTwoHandedScale, 1e-4f);
    }

    [Test]
    public void ReachAndBowDefaults_MatchSpec()
    {
        Assert.AreEqual(2.2f, defaults.sweptStrikeReach, 1e-4f);
        Assert.AreEqual(10f, defaults.rangeRanged, 1e-4f);
        Assert.AreEqual(2.35f, defaults.rangeTwoHanded, 1e-4f);
        Assert.AreEqual(1.8f, defaults.rangeOneHanded, 1e-4f);
        Assert.AreEqual(0.7f, defaults.bowPrecisionThreshold, 1e-4f);
        Assert.AreEqual(1.4f, defaults.bowFullPrecisionTime, 1e-4f);
        Assert.AreEqual(7.5f, defaults.bowLooseSpreadDegrees, 1e-4f);
        Assert.AreEqual(0.25f, defaults.bowPreciseSpreadDegrees, 1e-4f);
    }

    [Test]
    public void MovementPenaltyDefaults_MatchSpec()
    {
        Assert.AreEqual(0.45f, defaults.moveScaleAttackRelease, 1e-4f);
        Assert.AreEqual(0.62f, defaults.moveScaleAttacking, 1e-4f);
        Assert.AreEqual(0.58f, defaults.moveScaleBlocking, 1e-4f);
    }

    [Test]
    public void CoordinationDefaults_MatchSpec()
    {
        Assert.AreEqual(1, defaults.maxPlayerAttackers);
        Assert.AreEqual(2, defaults.maxTargetAttackers);
        Assert.AreEqual(2.8f, defaults.supportEngagementNear, 1e-4f);
        Assert.AreEqual(3.5f, defaults.supportEngagementFar, 1e-4f);
        Assert.AreEqual(18f, defaults.supportSlotSpreadDegrees, 1e-4f);
        Assert.AreEqual(1.8f, defaults.separationFalloff, 1e-4f);
        Assert.AreEqual(1.4f, defaults.separationMaxForce, 1e-4f);
        Assert.AreEqual(7, defaults.supportAngles.Length);
    }

    // Invariants beyond exact values: relationships that must hold for the combat to
    // read correctly (heavy overhead hits harder/slower than a quick thrust; the
    // two-handed weapon trades speed for damage; a perfect block is a tight window
    // that opens a longer counter; support standoff sits outside contact range).
    [Test]
    public void TuningInvariants_Hold()
    {
        Assert.Greater(defaults.damageUp, defaults.damageDefault, "overhead should out-damage a level swing");
        Assert.Greater(defaults.damageDefault, defaults.damageThrust, "a level swing should out-damage a thrust");
        Assert.Greater(defaults.windupUp, defaults.windupThrust, "overhead should be slower to wind up than a thrust");
        Assert.Greater(defaults.damageTwoHandedScale, 1f, "two-handed should hit harder");
        Assert.Greater(defaults.windupTwoHandedScale, 1f, "two-handed should be slower");
        Assert.Greater(defaults.attackCostTwoHanded, defaults.attackCostOneHanded, "two-handed swings should cost more stamina");
        Assert.Greater(defaults.counterWindow, defaults.perfectBlockWindow, "the counter window should outlast the perfect-block window");
        Assert.Greater(defaults.counterDamageMultiplier, 1f, "counters should be rewarded");
        Assert.Greater(defaults.staminaRegenIdle, defaults.staminaRegenBlocking, "blocking should slow stamina regen");
        Assert.Greater(defaults.supportEngagementFar, defaults.supportEngagementNear);
        Assert.Greater(defaults.rangeRanged, defaults.rangeTwoHanded, "bows should prefer a longer standoff than melee");
        Assert.Greater(defaults.rangeTwoHanded, defaults.rangeOneHanded, "two-handed reach should exceed one-handed");
        Assert.Less(defaults.bowPreciseSpreadDegrees, defaults.bowLooseSpreadDegrees, "a full draw should tighten the cone");
    }

    [Test]
    public void Facade_ReadsTheOverriddenSet()
    {
        CombatBalanceData custom = ScriptableObject.CreateInstance<CombatBalanceData>();
        custom.perfectBlockWindow = 0.33f;
        custom.counterWindow = 0.99f;
        custom.sweptStrikeReach = 3.14f;
        custom.moveScaleBlocking = 0.4f;
        custom.damageUp = 77f;
        try
        {
            CombatBalance.Override(custom);
            Assert.AreEqual(0.33f, CombatBalance.PerfectBlockWindow, 1e-4f);
            Assert.AreEqual(0.99f, CombatBalance.CounterWindow, 1e-4f);
            Assert.AreEqual(3.14f, CombatBalance.SweptStrikeReach, 1e-4f);
            Assert.AreEqual(0.4f, CombatBalance.MoveScaleBlocking, 1e-4f);
            Assert.AreEqual(77f, CombatBalance.DamageUp, 1e-4f);
        }
        finally
        {
            CombatBalance.Override(null);
            Object.DestroyImmediate(custom);
        }
    }
}
