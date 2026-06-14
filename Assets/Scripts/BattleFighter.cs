using UnityEngine;

public abstract class BattleFighter : MonoBehaviour
{
    public Team Team { get; private set; }
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
    public bool IsChargingAttack => Phase == CombatPhase.AttackWindup || Phase == CombatPhase.AttackHold;
    public float AttackChargeNormalized => Phase == CombatPhase.AttackWindup
        ? Mathf.Clamp01(1f - phaseTimer / Mathf.Max(phaseDuration, 0.0001f))
        : Phase == CombatPhase.AttackHold ? 1f : 0f;

    protected CharacterController controller;
    protected BattleManager battle;

    private const float AttackRange = 2.2f;
    private const float MaxStamina = 100f;

    private float maxHealth;
    private float health;
    private float stamina = MaxStamina;
    private float phaseTimer;
    private float phaseDuration;
    private float hitFlashTimer;
    private float staggerTimer;
    private float walkCycle;
    private float previousWalkCycle;
    private bool releaseQueued;
    private bool dealtAttackDamage;
    private Transform modelRoot;
    private Transform swordPivot;
    private Transform shieldPivot;
    private Transform leftLeg;
    private Transform rightLeg;
    private Transform leftArm;
    private Transform rightArm;
    private Renderer[] renderers;
    private Color[] baseColors;
    private Vector3 previousPosition;
    private Vector3 swordBasePosition;

    public void Configure(BattleManager owner, Team team, bool player)
    {
        battle = owner;
        Team = team;
        IsPlayer = player;
        maxHealth = player ? 125f : team == Team.Allies ? 110f : 100f;
        health = maxHealth;
        name = player ? "Player" : team == Team.Allies ? "Allied Soldier" : "Enemy Soldier";

        controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.85f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 0.93f, 0f);
        controller.stepOffset = 0.32f;
        controller.skinWidth = 0.04f;

        BuildModel();
        previousPosition = transform.position;
    }

    protected virtual void Update()
    {
        if (!IsAlive || battle == null)
            return;

        if (battle.IsBattleRunning)
        {
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
        if (!CanAct || IsAttacking || IsBlocking || stamina < 18f)
            return false;

        stamina -= 18f;
        AttackDirection = direction;
        Phase = CombatPhase.AttackWindup;
        phaseDuration = GetWindup(direction);
        phaseTimer = phaseDuration;
        releaseQueued = false;
        dealtAttackDamage = false;
        battle.PlayAttackSound(transform.position, IsPlayer);
        return true;
    }

    protected void ReleasePreparedAttack()
    {
        if (Phase == CombatPhase.AttackWindup)
            releaseQueued = true;
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

    protected void SetBlock(bool active, CombatDirection direction)
    {
        bool canBlock = CanAct && !IsAttacking;
        IsBlocking = active && canBlock;
        if (active)
            BlockDirection = direction;
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

        if (guarded)
        {
            staggerTimer = 0.08f;
            attacker.OnAttackBlocked();
        }
        else
        {
            health = Mathf.Max(0f, health - damage);
            staggerTimer = 0.24f;
            Phase = CombatPhase.HitReaction;
            IsBlocking = false;
            if (controller.enabled && toAttacker.sqrMagnitude > 0.01f)
                controller.Move(-toAttacker.normalized * 0.32f);
        }

        hitFlashTimer = guarded ? 0.1f : 0.2f;
        battle.ReportImpact(this, guarded, damage);

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
            controller.Move(transform.forward * 1.7f * Time.deltaTime);
            if (!dealtAttackDamage && phaseTimer <= phaseDuration * 0.55f)
            {
                dealtAttackDamage = true;
                BattleFighter target = battle.FindBestTarget(this, AttackRange, 100f);
                if (target != null)
                    target.ReceiveHit(GetDamage(AttackDirection), this, AttackDirection);
            }
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
            EnterRecovery();
        else if (Phase == CombatPhase.AttackRecovery)
            Phase = CombatPhase.Idle;
    }

    private void UpdateVisuals()
    {
        Vector3 planarDelta = transform.position - previousPosition;
        planarDelta.y = 0f;
        float movement = Mathf.Clamp01(planarDelta.magnitude / Mathf.Max(Time.deltaTime, 0.001f) / 4f);
        previousPosition = transform.position;
        walkCycle += movement * Time.deltaTime * 11f;
        if (battle.IsBattleRunning && Mathf.FloorToInt(walkCycle / Mathf.PI) > Mathf.FloorToInt(previousWalkCycle / Mathf.PI))
            battle.PlayFootstep(transform.position, IsPlayer);
        previousWalkCycle = walkCycle;

        ApplyWeaponPose();

        float legSwing = Mathf.Sin(walkCycle) * 28f * movement;
        leftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
        modelRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkCycle)) * 0.035f * movement);
        modelRoot.localRotation = Quaternion.Euler(staggerTimer > 0f ? -7f : 0f, 0f, IsBlocking ? 5f : 0f);

        hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = hitFlashTimer > 0f ? Color.white : baseColors[i];
    }

    private void Die()
    {
        IsBlocking = false;
        Phase = CombatPhase.Dead;
        if (controller != null)
            controller.enabled = false;

        transform.rotation = Quaternion.Euler(78f, transform.eulerAngles.y, Random.Range(-16f, 16f));
        transform.position += Vector3.down * 0.36f;
        battle.NotifyDeath(this);
    }

    private void BuildModel()
    {
        Color teamColor = Team == Team.Allies ? new Color(0.12f, 0.39f, 0.82f) : new Color(0.72f, 0.12f, 0.08f);
        Color cloth = Team == Team.Allies ? new Color(0.08f, 0.17f, 0.32f) : new Color(0.32f, 0.07f, 0.05f);
        Color metal = new Color(0.55f, 0.6f, 0.65f);
        Color leather = new Color(0.2f, 0.1f, 0.04f);

        modelRoot = new GameObject("Animated Model").transform;
        modelRoot.SetParent(transform, false);
        CreatePart("Torso", PrimitiveType.Capsule, modelRoot, new Vector3(0f, 1.16f, 0f), new Vector3(0.68f, 0.65f, 0.48f), cloth);
        CreatePart("Tabard", PrimitiveType.Cube, modelRoot, new Vector3(0f, 0.95f, 0.22f), new Vector3(0.5f, 0.72f, 0.08f), teamColor);
        CreatePart("Head", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.76f, 0f), Vector3.one * 0.4f, new Color(0.72f, 0.5f, 0.32f));
        CreatePart("Helmet", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.88f, 0f), new Vector3(0.47f, 0.27f, 0.47f), metal);
        CreatePart("Helmet Ridge", PrimitiveType.Cube, modelRoot, new Vector3(0f, 2.02f, 0f), new Vector3(0.1f, 0.2f, 0.55f), teamColor);
        CreatePart("Belt", PrimitiveType.Cube, modelRoot, new Vector3(0f, 0.91f, 0f), new Vector3(0.7f, 0.11f, 0.52f), leather);

        leftLeg = NewPivot("Left Leg", modelRoot, new Vector3(-0.2f, 0.78f, 0f));
        rightLeg = NewPivot("Right Leg", modelRoot, new Vector3(0.2f, 0.78f, 0f));
        CreatePart("Left Leg Mesh", PrimitiveType.Capsule, leftLeg, new Vector3(0f, -0.36f, 0f), new Vector3(0.22f, 0.42f, 0.22f), leather);
        CreatePart("Right Leg Mesh", PrimitiveType.Capsule, rightLeg, new Vector3(0f, -0.36f, 0f), new Vector3(0.22f, 0.42f, 0.22f), leather);

        leftArm = NewPivot("Left Arm", modelRoot, new Vector3(-0.42f, 1.48f, 0f));
        rightArm = NewPivot("Right Arm", modelRoot, new Vector3(0.42f, 1.48f, 0f));
        CreatePart("Left Arm Mesh", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.2f, 0.34f, 0.2f), metal);
        CreatePart("Right Arm Mesh", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.2f, 0.34f, 0.2f), metal);

        swordPivot = NewPivot("Sword Pivot", rightArm, new Vector3(0f, -0.52f, 0.08f));
        swordBasePosition = swordPivot.localPosition;
        CreatePart("Sword", PrimitiveType.Cube, swordPivot, new Vector3(0f, 0f, 0.72f), new Vector3(0.09f, 0.07f, 1.42f), metal);
        CreatePart("Sword Guard", PrimitiveType.Cube, swordPivot, new Vector3(0f, 0f, 0.06f), new Vector3(0.4f, 0.09f, 0.09f), leather);

        shieldPivot = NewPivot("Shield Pivot", leftArm, new Vector3(0f, -0.3f, 0.25f));
        CreatePart("Shield", PrimitiveType.Cylinder, shieldPivot, Vector3.zero, new Vector3(0.68f, 0.12f, 0.78f), teamColor, new Vector3(90f, 0f, 0f));
        CreatePart("Shield Boss", PrimitiveType.Sphere, shieldPivot, new Vector3(0f, 0f, -0.08f), Vector3.one * 0.22f, metal);

        renderers = modelRoot.GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].material.color;
    }

    private static Transform NewPivot(string pivotName, Transform parent, Vector3 position)
    {
        Transform pivot = new GameObject(pivotName).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = position;
        return pivot;
    }

    private void EnterRelease()
    {
        Phase = CombatPhase.AttackRelease;
        phaseDuration = GetRelease(AttackDirection);
        phaseTimer = phaseDuration;
        releaseQueued = false;
    }

    private void EnterRecovery()
    {
        Phase = CombatPhase.AttackRecovery;
        phaseDuration = GetRecovery(AttackDirection);
        phaseTimer = phaseDuration;
    }

    private void OnAttackBlocked()
    {
        if (!IsAttacking)
            return;
        Phase = CombatPhase.AttackRecovery;
        phaseDuration = GetRecovery(AttackDirection) + 0.22f;
        phaseTimer = phaseDuration;
    }

    private void ApplyWeaponPose()
    {
        float progress = phaseDuration > 0f ? 1f - phaseTimer / phaseDuration : 0f;
        Vector3 prepared = AttackDirection switch
        {
            CombatDirection.Left => new Vector3(-25f, -80f, -45f),
            CombatDirection.Right => new Vector3(-25f, 80f, 45f),
            CombatDirection.Up => new Vector3(-145f, 0f, -12f),
            _ => new Vector3(-5f, 0f, -8f)
        };
        Vector3 released = AttackDirection switch
        {
            CombatDirection.Left => new Vector3(25f, 95f, 50f),
            CombatDirection.Right => new Vector3(25f, -95f, -50f),
            CombatDirection.Up => new Vector3(95f, 0f, -8f),
            _ => new Vector3(-5f, 0f, -8f)
        };

        Vector3 swordEuler = new Vector3(-18f, 0f, 0f);
        swordPivot.localPosition = swordBasePosition;
        if (Phase == CombatPhase.AttackWindup)
            swordEuler = Vector3.Lerp(new Vector3(-18f, 0f, 0f), prepared, progress);
        else if (Phase == CombatPhase.AttackHold)
            swordEuler = prepared;
        else if (Phase == CombatPhase.AttackRelease)
        {
            swordEuler = Vector3.Lerp(prepared, released, progress);
            if (AttackDirection == CombatDirection.Thrust)
                swordPivot.localPosition = swordBasePosition + Vector3.forward * Mathf.Sin(progress * Mathf.PI) * 0.7f;
        }
        else if (Phase == CombatPhase.AttackRecovery)
            swordEuler = Vector3.Lerp(released, new Vector3(-18f, 0f, 0f), progress);

        swordPivot.localRotation = Quaternion.Euler(swordEuler);
        Vector3 shieldEuler = IsBlocking ? GetBlockPose(BlockDirection) : new Vector3(8f, 0f, 0f);
        shieldPivot.localRotation = Quaternion.Euler(shieldEuler);
        leftArm.localRotation = Quaternion.Euler(IsBlocking ? -55f : 0f, 0f, IsBlocking ? -20f : 0f);
        rightArm.localRotation = Quaternion.Euler(IsAttacking ? swordEuler.x * 0.25f : 0f, 0f, -8f);
    }

    private static Vector3 GetBlockPose(CombatDirection direction) => direction switch
    {
        CombatDirection.Left => new Vector3(-15f, -65f, -25f),
        CombatDirection.Right => new Vector3(-15f, 65f, 25f),
        CombatDirection.Up => new Vector3(-80f, 0f, 0f),
        _ => new Vector3(20f, 0f, 0f)
    };

    private static float GetWindup(CombatDirection direction) => direction == CombatDirection.Up ? 0.5f : direction == CombatDirection.Thrust ? 0.3f : 0.35f;
    private static float GetRelease(CombatDirection direction) => direction == CombatDirection.Thrust ? 0.2f : 0.25f;
    private static float GetRecovery(CombatDirection direction) => direction == CombatDirection.Up ? 0.6f : direction == CombatDirection.Thrust ? 0.4f : 0.45f;
    private static float GetDamage(CombatDirection direction) => direction == CombatDirection.Up ? 35f : direction == CombatDirection.Thrust ? 20f : 25f;

    public void DebugSetBlock(bool active, CombatDirection direction) => SetBlock(active, direction);

    public void DebugRestoreHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
        staggerTimer = 0f;
        if (health > 0f && Phase == CombatPhase.HitReaction)
            Phase = CombatPhase.Idle;
    }

    private static GameObject CreatePart(string partName, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, Vector3? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localEulerAngles = rotation ?? Vector3.zero;
        Object.Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().material = BattleBootstrap.CreateMaterial(color);
        return part;
    }
}
