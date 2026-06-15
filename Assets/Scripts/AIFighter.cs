using UnityEngine;

public sealed class AIFighter : BattleFighter
{
    public BattleFighter CurrentTarget => target;
    public bool HasAttackPermission => hasAttackPermission;
    public bool IsRetreating => retreating;

    private BattleFighter target;
    private float decisionTimer;
    private float targetLockTimer;
    private float blockTimer;
    private float attackDelay;
    private float permissionTimer;
    private float orbitDirection;
    private float preferredRange;
    private float aggression;
    private bool hasAttackPermission;
    private bool committedAttack;
    private bool retreating;
    private float retreatTimer;
    private CombatDirection plannedBlockDirection = CombatDirection.Right;

    private void Start()
    {
        preferredRange = IsRanged ? Random.Range(8.5f, 11f)
            : Weapon == WeaponType.TwoHandedSword ? Random.Range(2.15f, 2.55f) : Random.Range(1.65f, 1.95f);
        aggression = Random.Range(0.76f, 1.08f);
        orbitDirection = Random.value < 0.5f ? -1f : 1f;
        attackDelay = Random.Range(0.65f, 1.8f);
        targetLockTimer = Random.Range(0.8f, 1.35f);
    }

    protected override void Update()
    {
        base.Update();
        if (!IsAlive || IsInHitStop || battle == null || !battle.IsBattleRunning)
            return;

        decisionTimer -= Time.deltaTime;
        targetLockTimer -= Time.deltaTime;
        blockTimer -= Time.deltaTime;
        attackDelay -= Time.deltaTime;
        permissionTimer -= Time.deltaTime;

        if (!retreating && battle.ShouldRetreat(this))
        {
            retreating = true;
            battle.ReleaseAttackPermission(this);
            hasAttackPermission = false;
            committedAttack = false;
            SetBlock(false, plannedBlockDirection);
            CancelCombatForRetreat();
            retreatTimer = 2f;
            battle.NotifyMoraleBreak(Team);
        }
        if (retreating)
        {
            UpdateRetreat();
            return;
        }

        if (hasAttackPermission && (permissionTimer <= 0f || committedAttack && !IsAttacking))
        {
            battle.ReleaseAttackPermission(this);
            hasAttackPermission = false;
            committedAttack = false;
        }

        if (target == null || !target.IsAlive || targetLockTimer <= 0f && decisionTimer <= 0f)
        {
            BattleFighter next = battle.SelectTacticalTarget(this, target);
            if (next != target)
            {
                battle.ReleaseAttackPermission(this);
                hasAttackPermission = false;
                target = next;
            }
            targetLockTimer = Random.Range(0.85f, 1.45f);
        }

        if (decisionTimer <= 0f)
        {
            decisionTimer = Random.Range(0.12f, 0.22f);
            if (Random.value < 0.18f)
                orbitDirection *= -1f;
            EvaluateDefense();
        }

        if (target == null)
            return;

        if (battle.TryGetCommandPosition(this, target, out Vector3 commandPosition))
        {
            battle.ReleaseAttackPermission(this);
            hasAttackPermission = false;
            committedAttack = false;
            SetBlock(false, plannedBlockDirection);
            Vector3 toCommand = commandPosition - transform.position;
            toCommand.y = 0f;
            Vector3 commandSeparation = battle.GetSeparation(this) * 0.55f;
            if (toCommand.sqrMagnitude > 0.2f)
            {
                FaceDirection(toCommand, 10f);
                float formationSpeed = Mathf.Clamp(toCommand.magnitude * 1.8f, 0.65f, 3.5f);
                Move((toCommand.normalized + commandSeparation).normalized * formationSpeed);
            }
            else if (target != null)
                FaceDirection(target.transform.position - transform.position, 8f);
            return;
        }

        if (IsRanged)
        {
            UpdateRangedFighter();
            return;
        }

        SetBlock(blockTimer > 0f, plannedBlockDirection);
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        FaceDirection(toTarget, IsBlocking ? 13f : 10f);

        Vector3 desiredPosition = battle.GetEngagementPosition(this, target, hasAttackPermission, preferredRange);
        Vector3 toPosition = desiredPosition - transform.position;
        toPosition.y = 0f;
        Vector3 separation = battle.GetSeparation(this) * 1.35f;

        if (hasAttackPermission)
            UpdateActiveAttacker(distance, toTarget, toPosition, separation);
        else
            UpdateSupportingFighter(distance, toTarget, toPosition, separation);
    }

    private void UpdateActiveAttacker(float distance, Vector3 toTarget, Vector3 toPosition, Vector3 separation)
    {
        if (target.IsAttackThreatening && distance < 2.45f)
        {
            Vector3 evade = -toTarget.normalized + Vector3.Cross(Vector3.up, toTarget.normalized) * orbitDirection * 0.28f;
            TacticalMove((evade + separation).normalized * 2.4f);
            return;
        }

        if (distance > preferredRange + 0.3f)
        {
            TacticalMove((toPosition.normalized + separation).normalized * 3.25f);
            return;
        }

        if (distance < preferredRange - 0.28f)
        {
            TacticalMove((-toTarget.normalized + separation).normalized * 1.9f);
            return;
        }

        Vector3 circle = Vector3.Cross(Vector3.up, toTarget.normalized) * orbitDirection;
        TacticalMove((circle * 0.55f + separation).normalized * 1.15f);
        if (attackDelay <= 0f && blockTimer <= 0f && !IsBlocking && !IsAttacking)
        {
            CombatDirection direction = ChooseAttackDirection();
            if (PrepareAttack(direction))
            {
                ReleasePreparedAttack();
                attackDelay = Random.Range(1.45f, 2.2f) / aggression;
                committedAttack = true;
            }
        }
    }

    private void UpdateSupportingFighter(float distance, Vector3 toTarget, Vector3 toPosition, Vector3 separation)
    {
        if (distance < 2.35f)
        {
            Vector3 withdraw = -toTarget.normalized + Vector3.Cross(Vector3.up, toTarget.normalized) * orbitDirection * 0.55f;
            TacticalMove((withdraw + separation).normalized * 2.6f);
        }
        else if (toPosition.sqrMagnitude > 0.35f)
            TacticalMove((toPosition.normalized + separation).normalized * 2.7f);
        else
        {
            Vector3 circle = Vector3.Cross(Vector3.up, toTarget.normalized) * orbitDirection;
            TacticalMove((circle * 0.65f + separation).normalized * 1.2f);
        }

        if (attackDelay <= 0f && blockTimer <= 0f && battle.TryClaimAttackPermission(this, target))
        {
            hasAttackPermission = true;
            permissionTimer = 2.5f;
            committedAttack = false;
            attackDelay = Random.Range(0.18f, 0.42f);
        }
    }

    private void EvaluateDefense()
    {
        if (IsRanged)
        {
            blockTimer = 0f;
            return;
        }
        BattleFighter threat = battle.FindIncomingThreat(this) ?? target;
        if (threat == null || !threat.IsAttackThreatening || IsAttacking)
            return;

        float distance = DistanceTo(threat);
        if (distance > 2.7f)
            return;

        float correctChance = threat.IsPlayer ? 0.62f : 0.52f;
        plannedBlockDirection = Random.value < correctChance
            ? threat.AttackDirection
            : RandomWrongDirection(threat.AttackDirection);
        blockTimer = Random.Range(0.42f, 0.72f);
    }

    private void TacticalMove(Vector3 velocity)
    {
        Vector3 planar = velocity;
        planar.y = 0f;
        if (planar.sqrMagnitude > 0.01f)
        {
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            int mask = ignoreRaycast >= 0 ? ~(1 << ignoreRaycast) : ~0;
            Vector3 origin = transform.position + Vector3.up * 0.75f;
            if (Physics.SphereCast(origin, 0.32f, planar.normalized, out RaycastHit hit, 1.15f, mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, hit.normal) * orbitDirection;
                planar = Vector3.ProjectOnPlane(planar, hit.normal) + tangent * planar.magnitude * 0.7f;
            }
        }
        Move(planar);
    }

    private CombatDirection ChooseAttackDirection()
    {
        if (target.IsBlocking && Random.value < 0.72f)
            return RandomWrongDirection(target.BlockDirection);
        if (target.Phase == CombatPhase.AttackRecovery && Random.value < 0.55f)
            return CombatDirection.Up;
        return RandomDirection();
    }

    private void UpdateRangedFighter()
    {
        SetBlock(false, CombatDirection.Thrust);
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        // Aim above torso center to compensate for arrow drop across the
        // archer's preferred engagement distance.
        Vector3 aim = target.transform.position + Vector3.up * 1.48f
            - (transform.position + Vector3.up * 1.45f);
        SetAimDirection(aim);
        FaceDirection(toTarget, 12f);

        Vector3 separation = battle.GetSeparation(this) * 1.6f;
        if (distance < 6.5f)
        {
            Vector3 retreat = -toTarget.normalized + Vector3.Cross(Vector3.up, toTarget.normalized) * orbitDirection * 0.35f;
            TacticalMove((retreat + separation).normalized * 3.4f);
        }
        else if (distance > preferredRange + 2f)
            TacticalMove((toTarget.normalized + separation).normalized * 2.8f);
        else
        {
            Vector3 strafe = Vector3.Cross(Vector3.up, toTarget.normalized) * orbitDirection;
            TacticalMove((strafe * 0.45f + separation).normalized * 1.2f);
        }

        if (attackDelay <= 0f && distance <= 24f && !IsAttacking && PrepareAttack(CombatDirection.Thrust))
        {
            attackDelay = Random.Range(1.65f, 2.45f) / aggression;
        }
        else if (Phase == CombatPhase.AttackHold && BowPrecisionNormalized >= 0.72f)
            ReleasePreparedAttack();
    }

    private void UpdateRetreat()
    {
        retreatTimer -= Time.deltaTime;
        float edge = Team == Team.Allies ? -16f : 16f;
        Vector3 destination = new Vector3(Mathf.Clamp(transform.position.x, -14f, 14f), transform.position.y, edge);
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.45f || retreatTimer <= 0f)
        {
            battle.NotifyRetreat(this);
            return;
        }
        FaceDirection(direction, 15f);
        Move(direction.normalized * 5.2f);
    }

    private static CombatDirection RandomDirection() => (CombatDirection)Random.Range(0, 4);

    private static CombatDirection RandomWrongDirection(CombatDirection incoming)
    {
        CombatDirection result;
        do result = RandomDirection();
        while (result == incoming);
        return result;
    }

    private void OnDestroy()
    {
        if (battle != null)
            battle.ReleaseAttackPermission(this);
    }
}
