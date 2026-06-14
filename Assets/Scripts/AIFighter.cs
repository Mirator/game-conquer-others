using UnityEngine;

public sealed class AIFighter : BattleFighter
{
    private BattleFighter target;
    private float decisionTimer;
    private float blockTimer;
    private float attackDelay;
    private Vector3 strafeBias;
    private float preferredRange;
    private float aggression;
    private CombatDirection plannedBlockDirection = CombatDirection.Right;

    private void Start()
    {
        preferredRange = Random.Range(1.45f, 1.85f);
        aggression = Random.Range(0.7f, 1.15f);
    }

    protected override void Update()
    {
        base.Update();
        if (!IsAlive || battle == null || !battle.IsBattleRunning)
            return;

        decisionTimer -= Time.deltaTime;
        blockTimer -= Time.deltaTime;
        attackDelay -= Time.deltaTime;
        SetBlock(blockTimer > 0f, plannedBlockDirection);

        if (decisionTimer <= 0f || target == null || !target.IsAlive)
        {
            target = battle.FindNearestOpponent(this);
            decisionTimer = Random.Range(0.2f, 0.42f);
            strafeBias = Vector3.Cross(Vector3.up, transform.forward) * Random.Range(-0.55f, 0.55f);
        }

        if (target == null)
            return;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        FaceDirection(toTarget, 9f);

        if (distance > preferredRange + 0.25f)
        {
            Vector3 separation = battle.GetSeparation(this);
            Vector3 approach = toTarget.normalized + strafeBias * Mathf.Clamp01(3f / distance) + separation;
            Move(approach.normalized * Random.Range(3.05f, 3.55f));
        }
        else if (distance < preferredRange - 0.35f && target.IsAttacking)
        {
            Move(-toTarget.normalized * 1.8f);
        }
        else
        {
            Move(strafeBias * 1.15f);
            if (attackDelay <= 0f && !IsBlocking)
            {
                if (target.IsAttackThreatening && Random.value < 0.3f)
                {
                    blockTimer = Random.Range(0.4f, 0.8f);
                    plannedBlockDirection = Random.value < 0.4f ? target.AttackDirection : RandomWrongDirection(target.AttackDirection);
                    SetBlock(true, plannedBlockDirection);
                }
                else if (Random.value < aggression && PrepareAttack(RandomDirection()))
                {
                    ReleasePreparedAttack();
                    attackDelay = Random.Range(1.5f, 2.5f);
                }
            }
        }
    }

    private static CombatDirection RandomDirection() => (CombatDirection)Random.Range(0, 4);

    private static CombatDirection RandomWrongDirection(CombatDirection incoming)
    {
        CombatDirection result;
        do result = RandomDirection();
        while (result == incoming);
        return result;
    }
}
