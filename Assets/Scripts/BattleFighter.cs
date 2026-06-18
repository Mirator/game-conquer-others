using UnityEngine;

public abstract class BattleFighter : MonoBehaviour
{
    public Team Team { get; private set; }
    public UnitType UnitType { get; private set; }
    public WeaponType Weapon { get; private set; }
    public bool IsPlayer { get; private set; }
    public bool IsAlive => health > 0f && !withdrawn;
    public bool SurvivedBattle => health > 0f;
    public bool IsBlocking { get; private set; }
    public bool IsAttacking => Phase == CombatPhase.AttackWindup || Phase == CombatPhase.AttackHold || Phase == CombatPhase.AttackRelease || Phase == CombatPhase.AttackRecovery;
    public bool IsAttackThreatening => Phase == CombatPhase.AttackWindup || Phase == CombatPhase.AttackHold || Phase == CombatPhase.AttackRelease;
    public CombatDirection AttackDirection { get; private set; } = CombatDirection.Right;
    public CombatDirection BlockDirection { get; private set; } = CombatDirection.Right;
    public CombatPhase Phase { get; private set; } = CombatPhase.Idle;
    public float HealthNormalized => Mathf.Clamp01(health / maxHealth);
    public float StaminaNormalized => Mathf.Clamp01(stamina / MaxStamina);
    public float CurrentHealth => health;
    public bool IsInHitStop => hitStopTimer > 0f;
    public bool IsChargingAttack => Phase == CombatPhase.AttackWindup || Phase == CombatPhase.AttackHold;
    public bool IsCounterReady => counterWindowTimer > 0f;
    public bool IsCounterAttack => counterAttack;
    public bool IsRanged => Weapon == WeaponType.Bow;
    public bool CanBlock => Weapon != WeaponType.Bow;
    public float PreferredCombatRange => IsRanged ? 10f : Weapon == WeaponType.TwoHandedSword ? 2.35f : 1.8f;
    public float BowDrawNormalized => IsRanged ? Mathf.Clamp01(bowDrawTimer / BowFullPrecisionTime) : 0f;
    public float BowPrecisionNormalized => IsRanged
        ? Mathf.InverseLerp(BowPrecisionThreshold, BowFullPrecisionTime, bowDrawTimer) : 0f;
    public bool BowPrecisionReady => IsRanged && bowDrawTimer >= BowPrecisionThreshold;
    public float BowPrecisionThresholdNormalized => BowPrecisionThreshold / BowFullPrecisionTime;
    public float BowCurrentSpreadDegrees => IsRanged
        ? Mathf.Lerp(BowLooseSpreadDegrees, BowPreciseSpreadDegrees,
            Mathf.SmoothStep(0f, 1f, BowPrecisionNormalized)) : 0f;
    public float CurrentAttackDamageMultiplier => counterAttack ? CombatBalance.CounterDamageMultiplier : 1f;
    public bool ShouldShowHealthBar => damageDisplayTimer > 0f;
    public float AttackChargeNormalized => Phase == CombatPhase.AttackWindup
        ? Mathf.Clamp01(1f - phaseTimer / Mathf.Max(phaseDuration, 0.0001f))
        : Phase == CombatPhase.AttackHold ? 1f : 0f;
    public float AttackTelegraphProgress => Phase == CombatPhase.AttackWindup
        ? Mathf.Clamp01(1f - phaseTimer / Mathf.Max(phaseDuration, 0.0001f))
        : Phase == CombatPhase.AttackHold || Phase == CombatPhase.AttackRelease ? 1f : 0f;

    protected CharacterController controller;
    protected BattleManager battle;

    private const float AttackRange = 2.2f;
    private const float MaxStamina = 100f;
    private const float PerfectBlockWindow = 0.2f;
    private const float CounterWindow = 0.65f;
    private const float BowPrecisionThreshold = 0.7f;
    private const float BowFullPrecisionTime = 1.4f;
    private const float BowLooseSpreadDegrees = 7.5f;
    private const float BowPreciseSpreadDegrees = 0.25f;

    private float maxHealth;
    private float damageScale = 1f;
    private float health;
    private float stamina = MaxStamina;
    private float phaseTimer;
    private float phaseDuration;
    private float hitFlashTimer;
    private float hitStopTimer;
    private float staggerTimer;
    private float blockAge;
    private float counterWindowTimer;
    private float damageDisplayTimer;
    private float bowDrawTimer;
    private bool releaseQueued;
    private bool dealtAttackDamage;
    private bool whiffRecovery;
    private bool counterAttack;
    private bool withdrawn;
    private Vector3 aimDirection;
    private CombatDirection reactionDirection = CombatDirection.Right;
    private Vector3 previousStrikePoint;
    private BattleFighterPresentation presentation;

    public void Configure(BattleManager owner, Team team, bool player, float healthScale = 1f,
        UnitType unitType = UnitType.Militia, WeaponType weapon = WeaponType.SwordAndShield)
    {
        battle = owner;
        Team = team;
        IsPlayer = player;
        UnitType = unitType;
        Weapon = weapon;
        maxHealth = player ? 125f : team == Team.Allies ? 110f : 100f;
        if (!player)
        {
            maxHealth *= healthScale * UnitCatalog.HealthScale(unitType);
            damageScale = UnitCatalog.DamageScale(unitType);
        }
        health = maxHealth;
        name = player ? $"Player - {WeaponCatalog.ShortLabel(weapon)}"
            : $"{team} {UnitCatalog.Label(unitType)} - {WeaponCatalog.ShortLabel(weapon)}";
        aimDirection = transform.forward;

        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.85f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 0.93f, 0f);
        controller.stepOffset = 0.32f;
        controller.skinWidth = 0.04f;

        presentation = new BattleFighterPresentation(transform, Team, UnitType, Weapon);
    }

    protected virtual void Update()
    {
        if (!IsAlive || battle == null)
            return;

        hitStopTimer = Mathf.Max(0f, hitStopTimer - Time.unscaledDeltaTime);
        if (hitStopTimer > 0f)
        {
            UpdateVisuals();
            return;
        }

        if (battle.IsBattleRunning)
        {
            counterWindowTimer = Mathf.Max(0f, counterWindowTimer - Time.unscaledDeltaTime);
            damageDisplayTimer = Mathf.Max(0f, damageDisplayTimer - Time.unscaledDeltaTime);
            if (IsBlocking)
                blockAge += Time.unscaledDeltaTime;
            stamina = Mathf.Min(MaxStamina, stamina + (IsBlocking ? CombatBalance.StaminaRegenBlocking : CombatBalance.StaminaRegenIdle) * Time.deltaTime);
            staggerTimer = Mathf.Max(0f, staggerTimer - Time.deltaTime);
            if (IsRanged && IsChargingAttack)
                bowDrawTimer = Mathf.Min(BowFullPrecisionTime, bowDrawTimer + Time.deltaTime);
            UpdateAttack();
            if (Phase == CombatPhase.HitReaction && staggerTimer <= 0f)
                Phase = CombatPhase.Idle;
        }

        UpdateVisuals();
    }

    protected bool CanAct => IsAlive && staggerTimer <= 0f;

    protected void Move(Vector3 velocity)
    {
        if (!CanAct || controller == null || !controller.enabled)
            return;

        if (IsAttacking)
            velocity *= Phase == CombatPhase.AttackRelease ? 0.45f : 0.62f;
        if (IsBlocking)
            velocity *= 0.58f;
        velocity.y = controller.isGrounded ? -1f : velocity.y;
        controller.Move(velocity * Time.deltaTime);
    }

    protected void FaceDirection(Vector3 direction, float turnSpeed = 12f)
    {
        direction.y = 0f;
        if (!CanAct || direction.sqrMagnitude < 0.01f)
            return;

        Quaternion target = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    protected bool PrepareAttack(CombatDirection direction)
    {
        bool useCounter = counterWindowTimer > 0f;
        float staminaCost = IsRanged ? CombatBalance.AttackCostRanged
            : useCounter ? CombatBalance.AttackCostCounter
            : Weapon == WeaponType.TwoHandedSword ? CombatBalance.AttackCostTwoHanded : CombatBalance.AttackCostOneHanded;
        if (!CanAct || IsAttacking || IsBlocking || stamina < staminaCost)
            return false;

        stamina -= staminaCost;
        counterAttack = useCounter;
        counterWindowTimer = 0f;
        AttackDirection = direction;
        Phase = CombatPhase.AttackWindup;
        phaseDuration = GetWindup(direction) * (counterAttack ? 0.62f : 1f);
        phaseTimer = phaseDuration;
        releaseQueued = false;
        dealtAttackDamage = false;
        whiffRecovery = false;
        bowDrawTimer = 0f;
        return true;
    }

    protected void ReleasePreparedAttack(bool expediteQueuedRelease = false)
    {
        if (Phase == CombatPhase.AttackWindup)
        {
            releaseQueued = true;
            if (expediteQueuedRelease)
                phaseTimer = Mathf.Min(phaseTimer, 0.12f);
        }
        else if (Phase == CombatPhase.AttackHold)
            EnterRelease();
    }

    // Mount & Blade style re-aim: while the swing is still being held back,
    // the chosen direction tracks the player's current input.
    protected void AimHeldAttack(CombatDirection direction)
    {
        if (Phase == CombatPhase.AttackWindup || Phase == CombatPhase.AttackHold)
            AttackDirection = direction;
    }

    protected bool SetBlock(bool active, CombatDirection direction)
    {
        if (!CanBlock)
            active = false;
        if (active && IsChargingAttack)
            CancelPreparedAttack();
        bool canBlock = CanAct && !IsAttacking;
        bool wasBlocking = IsBlocking;
        bool changedDirection = direction != BlockDirection;
        IsBlocking = active && canBlock;
        // Raising the guard or re-aiming it to a new direction re-arms the perfect-block
        // window (re-aiming to meet an incoming attack is an intended defensive read).
        if (IsBlocking && (!wasBlocking || changedDirection))
            blockAge = 0f;
        if (active)
            BlockDirection = direction;
        return IsBlocking;
    }

    protected bool TrySpendStamina(float amount)
    {
        if (stamina < amount)
            return false;
        stamina -= amount;
        return true;
    }

    protected void CancelCombatForRetreat()
    {
        IsBlocking = false;
        Phase = CombatPhase.Idle;
        phaseTimer = 0f;
        phaseDuration = 0f;
        releaseQueued = false;
        counterAttack = false;
    }

    public void ReceiveHit(float damage, BattleFighter attacker, CombatDirection incomingDirection = CombatDirection.Right)
        => ReceiveHitInternal(damage, attacker, incomingDirection, false);

    public void ReceiveProjectileHit(float damage, BattleFighter attacker)
        => ReceiveHitInternal(damage, attacker, CombatDirection.Thrust, true);

    private void ReceiveHitInternal(float damage, BattleFighter attacker, CombatDirection incomingDirection, bool projectile)
    {
        if (!IsAlive)
            return;

        Vector3 toAttacker = attacker != null ? attacker.transform.position - transform.position : Vector3.zero;
        toAttacker.y = 0f;
        bool facingAttack = toAttacker.sqrMagnitude < 0.01f || Vector3.Dot(transform.forward, toAttacker.normalized) >= 0.707f;
        bool guarded = IsBlocking && facingAttack
            && (projectile ? Weapon == WeaponType.SwordAndShield : BlockDirection == incomingDirection);
        bool perfectBlock = guarded && blockAge <= PerfectBlockWindow;

        float appliedDamage = 0f;
        if (guarded)
        {
            staggerTimer = perfectBlock ? 0.025f : 0.08f;
            if (perfectBlock)
                counterWindowTimer = CounterWindow;
            attacker?.OnAttackBlocked(perfectBlock);
        }
        else
        {
            float before = health;
            health = Mathf.Max(0f, health - damage);
            appliedDamage = before - health;
            staggerTimer = 0.24f;
            reactionDirection = incomingDirection;
            Phase = CombatPhase.HitReaction;
            IsBlocking = false;
            counterWindowTimer = 0f;
            if (controller.enabled && toAttacker.sqrMagnitude > 0.01f)
                controller.Move(-toAttacker.normalized * 0.32f);
        }

        damageDisplayTimer = 4f;
        hitFlashTimer = perfectBlock ? 0.18f : guarded ? 0.1f : 0.2f;
        battle.ReportImpact(this, attacker, guarded, perfectBlock, attacker != null && attacker.IsCounterAttack);
        if (!guarded)
            battle.RecordDamage(attacker, this, appliedDamage, health <= 0f);

        if (health <= 0f)
            Die();
    }

    public float DistanceTo(BattleFighter other)
    {
        return Vector3.Distance(transform.position, other.transform.position);
    }

    private void UpdateAttack()
    {
        if (!IsAttacking)
            return;

        phaseTimer = Mathf.Max(0f, phaseTimer - Time.deltaTime);

        if (Phase == CombatPhase.AttackRelease && !IsRanged)
        {
            float progress = 1f - phaseTimer / Mathf.Max(phaseDuration, 0.0001f);
            Vector3 strikePoint = GetStrikePoint(AttackDirection, progress);
            if (!dealtAttackDamage)
            {
                BattleFighter target = battle.FindSweptStrikeTarget(this, previousStrikePoint, strikePoint, 0.72f);
                if (target != null)
                {
                    dealtAttackDamage = true;
                    target.ReceiveHit(GetDamage(AttackDirection) * damageScale * CurrentAttackDamageMultiplier, this, AttackDirection);
                }
            }
            previousStrikePoint = strikePoint;
        }

        if (phaseTimer > 0f)
            return;

        if (Phase == CombatPhase.AttackWindup)
        {
            if (releaseQueued)
                EnterRelease();
            else
                Phase = CombatPhase.AttackHold;
        }
        else if (Phase == CombatPhase.AttackRelease)
        {
            if (!dealtAttackDamage)
            {
                whiffRecovery = true;
                battle.ReportWhiff(this);
            }
            EnterRecovery();
        }
        else if (Phase == CombatPhase.AttackRecovery)
        {
            Phase = CombatPhase.Idle;
            whiffRecovery = false;
            counterAttack = false;
        }
    }

    private static bool loggedVisualFault;
    private void UpdateVisuals()
    {
        hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);
        if (presentation == null)
            return;
        // Presentation is purely visual, so a transient fault here must never spam
        // the console or interrupt the simulation. Swallow it, but record the first
        // occurrence (with full stack) so the root cause stays diagnosable.
        try
        {
            presentation.Update(battle, IsPlayer, IsBlocking, AttackDirection, BlockDirection, Phase,
                phaseTimer, phaseDuration, staggerTimer, whiffRecovery, hitFlashTimer, reactionDirection);
        }
        catch (System.Exception e)
        {
            if (loggedVisualFault)
                return;
            loggedVisualFault = true;
            Debug.LogError($"Presentation update fault on {name} (weapon={Weapon}, phase={Phase}): {e}");
            try { System.IO.File.WriteAllText(System.IO.Path.GetFullPath("PresentationCaptures/visual-fault.txt"),
                $"{name} weapon={Weapon} phase={Phase} alive={IsAlive}\n{e}"); }
            catch { }
        }
    }

    private void Die()
    {
        IsBlocking = false;
        Phase = CombatPhase.Dead;
        if (controller != null)
            controller.enabled = false;

        presentation.Fall(reactionDirection);
        battle.NotifyDeath(this);
    }

    private void EnterRelease()
    {
        Phase = CombatPhase.AttackRelease;
        phaseDuration = GetRelease(AttackDirection);
        phaseTimer = phaseDuration;
        releaseQueued = false;
        previousStrikePoint = GetStrikePoint(AttackDirection, 0f);
        battle.PlayAttackSound(transform.position, IsPlayer, Weapon);
        if (IsRanged)
        {
            dealtAttackDamage = true;
            Vector3 shotDirection = ApplyBowSpread(aimDirection, BowCurrentSpreadDegrees);
            battle.SpawnArrow(this, transform.position + Vector3.up * 1.45f + aimDirection * 0.5f,
                shotDirection, GetDamage(AttackDirection) * damageScale * CurrentAttackDamageMultiplier);
        }
    }

    private void EnterRecovery()
    {
        Phase = CombatPhase.AttackRecovery;
        phaseDuration = GetRecovery(AttackDirection);
        phaseTimer = phaseDuration;
    }

    private void OnAttackBlocked(bool perfect)
    {
        if (!IsAttacking)
            return;
        Phase = CombatPhase.AttackRecovery;
        phaseDuration = GetRecovery(AttackDirection) + (perfect ? 0.48f : 0.22f);
        phaseTimer = phaseDuration;
    }

    private void CancelPreparedAttack()
    {
        Phase = CombatPhase.Idle;
        phaseTimer = 0f;
        phaseDuration = 0f;
        releaseQueued = false;
        counterAttack = false;
    }

    private Vector3 GetStrikePoint(CombatDirection direction, float progress)
    {
        progress = Mathf.Clamp01(progress);
        Vector3 start = direction switch
        {
            CombatDirection.Left => new Vector3(-1.35f, 1.25f, 0.75f),
            CombatDirection.Right => new Vector3(1.35f, 1.25f, 0.75f),
            CombatDirection.Up => new Vector3(0f, 2.25f, 0.65f),
            _ => new Vector3(0.35f, 1.2f, 0.7f)
        };
        Vector3 end = direction switch
        {
            CombatDirection.Left => new Vector3(1.35f, 1.2f, 1.2f),
            CombatDirection.Right => new Vector3(-1.35f, 1.2f, 1.2f),
            CombatDirection.Up => new Vector3(0f, 0.8f, 1.45f),
            _ => new Vector3(0.35f, 1.2f, AttackRange)
        };
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        float reach = Weapon == WeaponType.TwoHandedSword ? 1.25f : 1f;
        Vector3 point = Vector3.Lerp(start, end, eased);
        point.z *= reach;
        point.x *= Weapon == WeaponType.TwoHandedSword ? 1.12f : 1f;
        return transform.TransformPoint(point);
    }

    private float GetWindup(CombatDirection direction)
    {
        if (IsRanged) return CombatBalance.WindupRanged;
        float value = direction == CombatDirection.Up ? CombatBalance.WindupUp
            : direction == CombatDirection.Thrust ? CombatBalance.WindupThrust : CombatBalance.WindupDefault;
        return value * (Weapon == WeaponType.TwoHandedSword ? CombatBalance.WindupTwoHandedScale : 1f);
    }

    private float GetRelease(CombatDirection direction)
    {
        if (IsRanged) return CombatBalance.ReleaseRanged;
        float value = direction == CombatDirection.Thrust ? CombatBalance.ReleaseThrust : CombatBalance.ReleaseDefault;
        return value * (Weapon == WeaponType.TwoHandedSword ? CombatBalance.ReleaseTwoHandedScale : 1f);
    }

    private float GetRecovery(CombatDirection direction)
    {
        if (IsRanged) return CombatBalance.RecoveryRanged;
        float value = direction == CombatDirection.Up ? CombatBalance.RecoveryUp
            : direction == CombatDirection.Thrust ? CombatBalance.RecoveryThrust : CombatBalance.RecoveryDefault;
        return value * (Weapon == WeaponType.TwoHandedSword ? CombatBalance.RecoveryTwoHandedScale : 1f);
    }

    private float GetDamage(CombatDirection direction)
    {
        if (IsRanged) return CombatBalance.DamageRanged;
        float value = direction == CombatDirection.Up ? CombatBalance.DamageUp
            : direction == CombatDirection.Thrust ? CombatBalance.DamageThrust : CombatBalance.DamageDefault;
        return value * (Weapon == WeaponType.TwoHandedSword ? CombatBalance.DamageTwoHandedScale : 1f);
    }

    protected void SetAimDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            aimDirection = direction.normalized;
    }

    private static Vector3 ApplyBowSpread(Vector3 direction, float spread)
    {
        Vector2 offset = Random.insideUnitCircle * spread;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        if (right.sqrMagnitude < 0.01f)
            right = Vector3.right;
        Vector3 up = Vector3.Cross(direction, right).normalized;
        return (Quaternion.AngleAxis(offset.x, up) * Quaternion.AngleAxis(offset.y, right) * direction).normalized;
    }

    public void DebugSetBlock(bool active, CombatDirection direction) => SetBlock(active, direction);

    public void DebugExpirePerfectBlock() => blockAge = PerfectBlockWindow + 0.1f;

    public bool DebugPrepareAttack(CombatDirection direction) => PrepareAttack(direction);

    public bool DebugForceAttackTelegraph(CombatDirection direction)
    {
        DebugClearCombatReaction();
        Phase = CombatPhase.Idle;
        IsBlocking = false;
        counterAttack = false;
        counterWindowTimer = 0f;
        stamina = MaxStamina;
        bool prepared = PrepareAttack(direction);
        if (prepared)
            ReleasePreparedAttack();
        return prepared;
    }

    public void DebugApplyHitStop(float duration) => ApplyHitStop(duration);

    public void ApplyHitStop(float duration)
    {
        hitStopTimer = Mathf.Max(hitStopTimer, duration);
    }

    public void DebugClearCombatReaction()
    {
        hitStopTimer = 0f;
        staggerTimer = 0f;
        if (Phase == CombatPhase.HitReaction)
            Phase = CombatPhase.Idle;
    }

    public void DebugResetCombatFeedback()
    {
        DebugClearCombatReaction();
        IsBlocking = false;
        counterWindowTimer = 0f;
        counterAttack = false;
        damageDisplayTimer = 0f;
        hitFlashTimer = 0f;
    }

    public void DebugRestoreHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
        staggerTimer = 0f;
        if (health > 0f && Phase == CombatPhase.HitReaction)
            Phase = CombatPhase.Idle;
    }

    public void DebugRestoreStamina() => stamina = MaxStamina;

    public void DebugTeleport(Vector3 position)
    {
        bool enabled = controller != null && controller.enabled;
        if (enabled)
            controller.enabled = false;
        transform.position = position;
        if (enabled)
            controller.enabled = true;
    }

    public void WithdrawFromBattle()
    {
        if (!IsAlive)
            return;
        withdrawn = true;
        IsBlocking = false;
        Phase = CombatPhase.Idle;
        if (controller != null)
            controller.enabled = false;
        gameObject.SetActive(false);
    }

    public virtual void DebugAimAt(BattleFighter target)
    {
        if (target != null)
            SetAimDirection(target.transform.position + Vector3.up * 1.1f - (transform.position + Vector3.up * 1.45f));
    }

    public void DebugReleasePreparedAttack()
    {
        if (IsRanged)
            bowDrawTimer = BowFullPrecisionTime;
        ReleasePreparedAttack(true);
    }

    public bool DebugTrailEmitting => presentation != null && presentation.IsTrailEmitting;

}
