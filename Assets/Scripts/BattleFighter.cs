using UnityEngine;

public abstract class BattleFighter : MonoBehaviour
{
    public Team Team { get; private set; }
    public UnitType UnitType { get; private set; }
    public bool IsPlayer { get; private set; }
    public bool IsAlive => health > 0f;
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
    public float CurrentAttackDamageMultiplier => counterAttack ? 1.45f : 1f;
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
    private bool releaseQueued;
    private bool dealtAttackDamage;
    private bool whiffRecovery;
    private bool counterAttack;
    private Vector3 previousStrikePoint;
    private BattleFighterPresentation presentation;

    public void Configure(BattleManager owner, Team team, bool player, float healthScale = 1f, UnitType unitType = UnitType.Militia)
    {
        battle = owner;
        Team = team;
        IsPlayer = player;
        UnitType = unitType;
        maxHealth = player ? 125f : team == Team.Allies ? 110f : 100f;
        if (!player)
        {
            maxHealth *= healthScale * UnitCatalog.HealthScale(unitType);
            damageScale = UnitCatalog.DamageScale(unitType);
        }
        health = maxHealth;
        name = player ? "Player" : $"{team} {UnitCatalog.Label(unitType)}";

        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.85f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 0.93f, 0f);
        controller.stepOffset = 0.32f;
        controller.skinWidth = 0.04f;

        presentation = new BattleFighterPresentation(transform, Team, UnitType);
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
            stamina = Mathf.Min(MaxStamina, stamina + (IsBlocking ? 12f : 25f) * Time.deltaTime);
            staggerTimer = Mathf.Max(0f, staggerTimer - Time.deltaTime);
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
        float staminaCost = useCounter ? 10f : 18f;
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
        if (active && IsChargingAttack)
            CancelPreparedAttack();
        bool canBlock = CanAct && !IsAttacking;
        bool wasBlocking = IsBlocking;
        bool changedDirection = direction != BlockDirection;
        IsBlocking = active && canBlock;
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

    public void ReceiveHit(float damage, BattleFighter attacker, CombatDirection incomingDirection = CombatDirection.Right)
    {
        if (!IsAlive)
            return;

        Vector3 toAttacker = attacker.transform.position - transform.position;
        toAttacker.y = 0f;
        bool facingAttack = toAttacker.sqrMagnitude < 0.01f || Vector3.Dot(transform.forward, toAttacker.normalized) >= 0.707f;
        bool guarded = IsBlocking && facingAttack && BlockDirection == incomingDirection;
        bool perfectBlock = guarded && blockAge <= PerfectBlockWindow;

        float appliedDamage = 0f;
        if (guarded)
        {
            staggerTimer = perfectBlock ? 0.025f : 0.08f;
            if (perfectBlock)
                counterWindowTimer = CounterWindow;
            attacker.OnAttackBlocked(perfectBlock);
        }
        else
        {
            float before = health;
            health = Mathf.Max(0f, health - damage);
            appliedDamage = before - health;
            staggerTimer = 0.24f;
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

        if (Phase == CombatPhase.AttackRelease)
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

    private void UpdateVisuals()
    {
        hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);
        presentation.Update(battle, IsPlayer, IsBlocking, AttackDirection, BlockDirection, Phase,
            phaseTimer, phaseDuration, staggerTimer, whiffRecovery, hitFlashTimer);
    }

    private void Die()
    {
        IsBlocking = false;
        Phase = CombatPhase.Dead;
        if (controller != null)
            controller.enabled = false;

        presentation.Fall();
        battle.NotifyDeath(this);
    }

    private void EnterRelease()
    {
        Phase = CombatPhase.AttackRelease;
        phaseDuration = GetRelease(AttackDirection);
        phaseTimer = phaseDuration;
        releaseQueued = false;
        previousStrikePoint = GetStrikePoint(AttackDirection, 0f);
        battle.PlayAttackSound(transform.position, IsPlayer);
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
        return transform.TransformPoint(Vector3.Lerp(start, end, eased));
    }

    private static float GetWindup(CombatDirection direction) => direction == CombatDirection.Up ? 0.5f : direction == CombatDirection.Thrust ? 0.3f : 0.35f;
    private static float GetRelease(CombatDirection direction) => direction == CombatDirection.Thrust ? 0.2f : 0.25f;
    private static float GetRecovery(CombatDirection direction) => direction == CombatDirection.Up ? 0.6f : direction == CombatDirection.Thrust ? 0.4f : 0.45f;
    private static float GetDamage(CombatDirection direction) => direction == CombatDirection.Up ? 35f : direction == CombatDirection.Thrust ? 20f : 25f;

    public void DebugSetBlock(bool active, CombatDirection direction) => SetBlock(active, direction);

    public void DebugExpirePerfectBlock() => blockAge = PerfectBlockWindow + 0.1f;

    public bool DebugPrepareAttack(CombatDirection direction) => PrepareAttack(direction);

    public bool DebugForceAttackTelegraph(CombatDirection direction)
    {
        DebugClearCombatReaction();
        Phase = CombatPhase.Idle;
        IsBlocking = false;
        counterAttack = false;
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

}
