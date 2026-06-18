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
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;
    private CombatDirection poseAttackDirection;
    private CombatPhase posePhase;
    private float poseProgress;
    private bool warnedMissingPoseBones;
    private Transform weaponPivot;

    // Calibration: rotates the weapon so its blade aligns with the forearm line.
    private static readonly Vector3 BladeAlign = new Vector3(-90f, 0f, 0f);

    public void SetWeaponPivot(Transform pivot) => weaponPivot = pivot;

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
        // Attacks are driven procedurally in LateUpdate, so the body stays in
        // locomotion/idle here and the arm swing is layered on top.
        string state = !alive ? "Death"
            : staggerTimer > 0f ? "Hit"
            : movement > 0.65f ? "Jog"
            : movement > 0.05f ? formation ? "FormationWalk" : "Walk" : "Idle";
        if (state == currentState)
            return;
        currentState = state;
        animator.CrossFade(state, state == "Hit" ? 0.05f : 0.12f);
    }

    // progress is the 0..1 progress WITHIN the current phase (not a global swing).
    public void SetCombatPose(WeaponType weapon, bool blocking, CombatDirection attackDirection,
        CombatDirection blockDirection, CombatPhase phase, float progress)
    {
        poseAttackDirection = attackDirection;
        posePhase = phase;
        poseProgress = progress;
    }

    private void LateUpdate()
    {
        bool attacking = posePhase == CombatPhase.AttackWindup || posePhase == CombatPhase.AttackHold
            || posePhase == CombatPhase.AttackRelease || posePhase == CombatPhase.AttackRecovery;
        if (!attacking)
            return;
        if (rightUpperArm == null)
            CacheBones();
        ApplyAttackSwing();
    }

    private void CacheBones()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            if (!warnedMissingPoseBones && Application.isEditor)
            {
                warnedMissingPoseBones = true;
                Debug.LogWarning($"FighterView '{name}' cannot drive procedural attacks: animator is "
                    + (animator == null ? "missing" : "not humanoid"));
            }
            return;
        }
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    // Procedurally swing the sword arm so the strike matches the aimed direction.
    // Interpolated PER PHASE (idle -> cocked -> follow-through -> idle) rather than
    // across one global parameter, so windup, hold, release and recovery each read
    // distinctly. Drives only the right (sword) arm; the body keeps its animated
    // locomotion pose. (See directional-attack design doc.)
    private void ApplyAttackSwing()
    {
        if (rightUpperArm == null || rightLowerArm == null || rightHand == null)
            return;

        Vector3 f = transform.forward;
        Vector3 r = transform.right;
        Vector3 u = transform.up;

        // Shared neutral "en garde" the swing starts from and returns to.
        Vector3 idleUpper = (f * 0.30f + u * 0.05f).normalized;
        Vector3 idleLower = (f * 0.65f + u * 0.05f).normalized;

        // cocked (windup end) and follow-through (release end) directions + blade roll.
        Vector3 cu, cl, eu, el;
        float roll;
        switch (poseAttackDirection)
        {
            case CombatDirection.Up: // raise overhead -> chop down forward
                cu = u * 0.55f + f * 0.10f;
                cl = u * 0.65f + f * 0.05f;
                eu = f * 0.55f - u * 0.30f;
                el = f * 0.40f - u * 0.80f;
                roll = 0f;
                break;
            case CombatDirection.Left: // diagonal slash upper-right -> lower-left
                cu = r * 0.40f + f * 0.25f + u * 0.10f;
                cl = r * 0.10f + f * 0.55f + u * 0.10f;
                eu = -r * 0.50f + f * 0.25f - u * 0.30f;
                el = -r * 0.20f + f * 0.55f - u * 0.35f;
                roll = 50f;
                break;
            case CombatDirection.Thrust: // stab: blade at center, push hand forward
                cu = -f * 0.10f + u * 0.10f - r * 0.05f;
                cl = f * 0.85f + u * 0.02f;
                eu = f * 0.65f + u * 0.03f;
                el = f * 0.96f + u * 0.06f;
                roll = 0f;
                break;
            default: // Right: diagonal slash upper-left -> lower-right
                cu = -r * 0.40f + f * 0.25f + u * 0.10f;
                cl = -r * 0.10f + f * 0.55f + u * 0.10f;
                eu = r * 0.50f + f * 0.25f - u * 0.30f;
                el = r * 0.20f + f * 0.55f - u * 0.35f;
                roll = -50f;
                break;
        }

        float t = Mathf.Clamp01(poseProgress);
        Vector3 upper, lower;
        float bladeRoll;
        switch (posePhase)
        {
            case CombatPhase.AttackWindup: // idle -> cocked
                float w = EaseOut(t);
                upper = Vector3.Slerp(idleUpper, cu.normalized, w);
                lower = Vector3.Slerp(idleLower, cl.normalized, w);
                bladeRoll = Mathf.Lerp(0f, roll, w);
                break;
            case CombatPhase.AttackHold: // hold cocked
                upper = cu.normalized;
                lower = cl.normalized;
                bladeRoll = roll;
                break;
            case CombatPhase.AttackRelease: // cocked -> follow-through (fast strike)
                float s = EaseOutCubic(t);
                upper = Vector3.Slerp(cu.normalized, eu.normalized, s);
                lower = Vector3.Slerp(cl.normalized, el.normalized, s);
                bladeRoll = roll;
                break;
            default: // AttackRecovery: follow-through -> idle
                float rec = EaseOut(t);
                upper = Vector3.Slerp(eu.normalized, idleUpper, rec);
                lower = Vector3.Slerp(el.normalized, idleLower, rec);
                bladeRoll = Mathf.Lerp(roll, 0f, rec);
                break;
        }

        AimSegment(rightUpperArm, rightLowerArm, upper);
        AimSegment(rightLowerArm, rightHand, lower);

        // Blade leads along the forearm line; per-attack roll squares the cutting
        // edge to the swing arc (first-class data per the design doc).
        if (weaponPivot != null)
            weaponPivot.rotation = Quaternion.LookRotation(lower, transform.up)
                * Quaternion.Euler(BladeAlign.x, BladeAlign.y, BladeAlign.z + bladeRoll);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseOutCubic(float t) { float a = 1f - t; return 1f - a * a * a; }

    private static void AimSegment(Transform bone, Transform child, Vector3 desiredWorldDirection)
    {
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.0001f || desiredWorldDirection.sqrMagnitude < 0.0001f)
            return;
        Quaternion correction = Quaternion.FromToRotation(current.normalized, desiredWorldDirection.normalized);
        bone.rotation = correction * bone.rotation;
    }
}
