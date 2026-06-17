using System;
using UnityEngine;

[Serializable]
public sealed class FighterViewAnchors
{
    public Transform rightHand;
    public Transform leftHand;
    public Transform projectile;
    public Transform hit;
    public Transform footsteps;
}

public sealed class FighterView : MonoBehaviour
{
    public Animator animator;
    public FighterViewAnchors anchors = new();
    public Renderer[] teamTintRenderers;
    public TrailRenderer weaponTrail;

    private MaterialPropertyBlock properties;
    private string currentState;
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;
    private WeaponType poseWeapon;
    private CombatDirection poseAttackDirection;
    private CombatDirection poseBlockDirection;
    private CombatPhase posePhase;
    private bool poseBlocking;
    private bool warnedMissingPoseBones;
    private HumanPoseHandler poseHandler;
    private HumanPose humanPose;
    private int leftArmDownUp = -1;
    private int leftArmFrontBack = -1;
    private int leftForearmStretch = -1;
    private int rightArmDownUp = -1;
    private int rightArmFrontBack = -1;
    private int rightForearmStretch = -1;

    private const string ControllerResource = "Presentation/Fighter";

    private void Awake()
    {
        EnsureAnimatorController();
        CacheBones();
        CreateHead();
    }

    // Quaternius outfit FBXs ship without a base head mesh, so the hood/neck reads
    // as empty. Attach a simple skin-toned head to the validated Head bone so every
    // fighter has a visible head that follows the animation.
    private void CreateHead()
    {
        if (animator == null || !animator.isHuman)
            return;
        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone == null)
            return;
        // Remove any stray/duplicate heads (including one accidentally baked into
        // the prefab) so we always end with exactly one head that has a material.
        for (int i = headBone.childCount - 1; i >= 0; i--)
            if (headBone.GetChild(i).name == "Generated Head")
                Destroy(headBone.GetChild(i).gameObject);
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Generated Head";
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(headBone, false);
        head.transform.localPosition = new Vector3(0f, 0.09f, 0.01f);
        head.transform.localRotation = Quaternion.identity;
        head.transform.localScale = new Vector3(0.16f, 0.19f, 0.17f);
        head.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(new Color(0.78f, 0.6f, 0.47f));
    }

    // The authored prefab can lose its serialized controller reference when the
    // catalog is rebuilt; guarantee playback by binding it from Resources here.
    private void EnsureAnimatorController()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            return;
        if (animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(ControllerResource);
        // Gameplay owns the fighter's position; root motion in the locomotion clips
        // would slide the model away from its logical transform during movement.
        animator.applyRootMotion = false;
    }

    public void ApplyTeam(Team team)
    {
        properties ??= new MaterialPropertyBlock();
        Color color = team == Team.Allies ? new Color(0.08f, 0.34f, 0.9f) : new Color(0.78f, 0.08f, 0.045f);
        if (teamTintRenderers == null)
            return;
        foreach (Renderer target in teamTintRenderers)
        {
            if (target == null)
                continue;
            target.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            target.SetPropertyBlock(properties);
        }
    }

    public void UpdateState(float movement, bool blocking, CombatPhase phase, float staggerTimer, bool alive,
        CombatDirection attackDirection = CombatDirection.Right, bool formation = false)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        bool attacking = phase == CombatPhase.AttackWindup || phase == CombatPhase.AttackHold
            || phase == CombatPhase.AttackRelease || phase == CombatPhase.AttackRecovery;
        string state = !alive ? "Death"
            : staggerTimer > 0f ? "Hit"
            : attacking ? AttackState(attackDirection)
            : movement > 0.65f ? "Jog"
            : movement > 0.05f ? formation ? "FormationWalk" : "Walk" : "Idle";
        if (state == currentState)
            return;
        currentState = state;
        bool fast = state == "Hit" || state.StartsWith("Attack");
        animator.CrossFade(state, fast ? 0.05f : 0.12f);
    }

    // Each attack direction maps to a distinct authored swing; fall back to the
    // generic "Attack" if a directional clip is unavailable in the controller.
    private string AttackState(CombatDirection direction)
    {
        string state = direction switch
        {
            CombatDirection.Up => "Attack_Up",
            CombatDirection.Left => "Attack_Left",
            CombatDirection.Thrust => "Attack_Thrust",
            _ => "Attack_Right"
        };
        return HasState(state) ? state : "Attack";
    }

    private bool HasState(string state) => animator.HasState(0, Animator.StringToHash(state));

    public void SetCombatPose(WeaponType weapon, bool blocking, CombatDirection attackDirection,
        CombatDirection blockDirection, CombatPhase phase)
    {
        poseWeapon = weapon;
        poseBlocking = blocking;
        poseAttackDirection = attackDirection;
        poseBlockDirection = blockDirection;
        posePhase = phase;
    }

    private void LateUpdate()
    {
        // The AnimatorController drives the full body (idle/walk/attack/block/hit
        // /death). The additive arm-pose override below is kept but disabled; the
        // authored clips already convey combat, and the override fought retargeting.
        if (!poseOverrideEnabled)
            return;
        if (leftUpperArm == null || rightUpperArm == null)
            CacheBones();
        ApplyArmPose();
    }

    private bool poseOverrideEnabled;

    private void CacheBones()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            if (!warnedMissingPoseBones && Application.isEditor)
            {
                warnedMissingPoseBones = true;
                Debug.LogWarning($"FighterView '{name}' cannot apply authored arm pose: animator is "
                    + (animator == null ? "missing" : "not humanoid"));
            }
            return;
        }
        poseHandler ??= new HumanPoseHandler(animator.avatar, animator.transform);
        CacheMuscleIndices();
        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void ApplyArmPose()
    {
        if (ApplyHumanoidMusclePose())
            return;

        if (leftUpperArm == null || leftLowerArm == null || leftHand == null
            || rightUpperArm == null || rightLowerArm == null || rightHand == null)
            return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        bool attacking = posePhase == CombatPhase.AttackWindup || posePhase == CombatPhase.AttackHold
            || posePhase == CombatPhase.AttackRelease || posePhase == CombatPhase.AttackRecovery;

        Vector3 leftUpper = (-right * 0.35f - up * 0.72f + forward * 0.42f).normalized;
        Vector3 leftLower = (forward * 0.82f - up * 0.16f + right * 0.05f).normalized;
        Vector3 rightUpper = (right * 0.35f - up * 0.72f + forward * 0.42f).normalized;
        Vector3 rightLower = (forward * 0.82f - up * 0.12f - right * 0.05f).normalized;

        if (poseWeapon == WeaponType.Bow)
        {
            leftUpper = (-right * 0.18f - up * 0.24f + forward * 0.95f).normalized;
            leftLower = (forward * 0.98f - up * 0.04f).normalized;
            rightUpper = (right * 0.22f - up * 0.2f + forward * 0.74f).normalized;
            rightLower = (-right * 0.35f - up * 0.08f - forward * 0.78f).normalized;
        }
        else if (poseWeapon == WeaponType.TwoHandedSword)
        {
            float side = poseAttackDirection == CombatDirection.Left ? -0.18f
                : poseAttackDirection == CombatDirection.Right ? 0.18f : 0f;
            leftUpper = (-right * 0.18f - up * 0.45f + forward * 0.85f + right * side).normalized;
            leftLower = (forward * 0.88f - up * 0.08f + right * 0.18f).normalized;
            rightUpper = (right * 0.18f - up * 0.45f + forward * 0.85f + right * side).normalized;
            rightLower = (forward * 0.88f - up * 0.08f - right * 0.18f).normalized;
        }
        else if (poseBlocking)
        {
            float shieldSide = poseBlockDirection == CombatDirection.Left ? -0.35f
                : poseBlockDirection == CombatDirection.Right ? 0.2f : -0.08f;
            leftUpper = (-right * 0.28f - up * 0.28f + forward * 0.92f + right * shieldSide).normalized;
            leftLower = (forward * 0.98f + right * shieldSide * 0.35f).normalized;
        }
        else if (attacking)
        {
            float side = poseAttackDirection == CombatDirection.Left ? -0.35f
                : poseAttackDirection == CombatDirection.Right ? 0.35f : 0f;
            rightUpper = (right * 0.22f - up * 0.35f + forward * 0.86f + right * side).normalized;
            rightLower = (forward * 0.9f + right * side * 0.45f - up * 0.08f).normalized;
        }

        AimSegment(leftUpperArm, leftLowerArm, leftUpper);
        AimSegment(leftLowerArm, leftHand, leftLower);
        AimSegment(rightUpperArm, rightLowerArm, rightUpper);
        AimSegment(rightLowerArm, rightHand, rightLower);
    }

    private static void AimSegment(Transform bone, Transform child, Vector3 desiredWorldDirection)
    {
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.0001f || desiredWorldDirection.sqrMagnitude < 0.0001f)
            return;
        Quaternion correction = Quaternion.FromToRotation(current.normalized, desiredWorldDirection.normalized);
        bone.rotation = correction * bone.rotation;
    }

    private bool ApplyHumanoidMusclePose()
    {
        if (poseHandler == null || leftArmDownUp < 0 || rightArmDownUp < 0)
            return false;

        poseHandler.GetHumanPose(ref humanPose);
        float leftFront = 0.28f;
        float rightFront = 0.28f;
        float leftBend = 0.55f;
        float rightBend = 0.45f;

        if (poseWeapon == WeaponType.Bow)
        {
            leftFront = 0.78f;
            rightFront = -0.3f;
            leftBend = 0.1f;
            rightBend = 0.82f;
        }
        else if (poseWeapon == WeaponType.TwoHandedSword)
        {
            leftFront = 0.68f;
            rightFront = 0.68f;
            leftBend = 0.5f;
            rightBend = 0.5f;
        }
        else if (poseBlocking)
        {
            leftFront = 0.82f;
            leftBend = 0.25f;
        }

        SetMuscle(leftArmDownUp, -0.78f);
        SetMuscle(rightArmDownUp, -0.78f);
        SetMuscle(leftArmFrontBack, leftFront);
        SetMuscle(rightArmFrontBack, rightFront);
        SetMuscle(leftForearmStretch, leftBend);
        SetMuscle(rightForearmStretch, rightBend);
        poseHandler.SetHumanPose(ref humanPose);
        return true;
    }

    private void SetMuscle(int index, float value)
    {
        if (index < 0 || index >= humanPose.muscles.Length)
            return;
        humanPose.muscles[index] = Mathf.Clamp(value, -1f, 1f);
    }

    private void CacheMuscleIndices()
    {
        if (leftArmDownUp >= 0)
            return;
        leftArmDownUp = FindMuscle("Left Arm Down-Up");
        leftArmFrontBack = FindMuscle("Left Arm Front-Back");
        leftForearmStretch = FindMuscle("Left Forearm Stretch");
        rightArmDownUp = FindMuscle("Right Arm Down-Up");
        rightArmFrontBack = FindMuscle("Right Arm Front-Back");
        rightForearmStretch = FindMuscle("Right Forearm Stretch");
    }

    private static int FindMuscle(string muscleName)
    {
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            if (HumanTrait.MuscleName[i] == muscleName)
                return i;
        return -1;
    }
}
