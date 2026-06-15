using UnityEngine;

// Owns the procedural fighter model and animation. BattleFighter supplies combat
// state, while this class translates that state into presentation.
public sealed class BattleFighterPresentation
{
    private readonly Transform fighter;
    private readonly Transform modelRoot;
    private readonly Transform swordPivot;
    private readonly Transform shieldPivot;
    private readonly Transform leftLeg;
    private readonly Transform rightLeg;
    private readonly Transform leftArm;
    private readonly Transform rightArm;
    private readonly Renderer[] renderers;
    private readonly Color[] baseColors;
    private readonly MaterialPropertyBlock colorProperties = new();
    private readonly Vector3 swordBasePosition;

    private Vector3 previousPosition;
    private float walkCycle;
    private float previousWalkCycle;

    public BattleFighterPresentation(Transform fighterTransform, Team team, UnitType unitType)
    {
        fighter = fighterTransform;
        Color teamColor = team == Team.Allies ? new Color(0.12f, 0.39f, 0.82f) : new Color(0.72f, 0.12f, 0.08f);
        Color cloth = team == Team.Allies ? new Color(0.08f, 0.17f, 0.32f) : new Color(0.32f, 0.07f, 0.05f);
        Color metal = new Color(0.55f, 0.6f, 0.65f);
        Color leather = new Color(0.2f, 0.1f, 0.04f);
        Color rankColor = unitType == UnitType.Guard ? new Color(0.92f, 0.72f, 0.18f)
            : unitType == UnitType.Veteran ? metal : leather;

        modelRoot = new GameObject("Animated Model").transform;
        modelRoot.SetParent(fighter, false);
        CreatePart("Torso", PrimitiveType.Capsule, modelRoot, new Vector3(0f, 1.16f, 0f), new Vector3(0.68f, 0.65f, 0.48f), cloth);
        CreatePart("Tabard", PrimitiveType.Cube, modelRoot, new Vector3(0f, 0.95f, 0.22f), new Vector3(0.5f, 0.72f, 0.08f), teamColor);
        CreatePart("Head", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.76f, 0f), Vector3.one * 0.4f, new Color(0.72f, 0.5f, 0.32f));
        CreatePart("Helmet", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.88f, 0f), new Vector3(0.47f, 0.27f, 0.47f), metal);
        CreatePart("Helmet Ridge", PrimitiveType.Cube, modelRoot, new Vector3(0f, 2.02f, 0f),
            unitType == UnitType.Guard ? new Vector3(0.16f, 0.32f, 0.6f) : new Vector3(0.1f, 0.2f, 0.55f), rankColor);
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
            baseColors[i] = renderers[i].sharedMaterial.color;
        previousPosition = fighter.position;
    }

    public void Update(BattleManager battle, bool isPlayer, bool isBlocking, CombatDirection attackDirection,
        CombatDirection blockDirection, CombatPhase phase, float phaseTimer, float phaseDuration,
        float staggerTimer, bool whiffRecovery, float hitFlashTimer)
    {
        Vector3 planarDelta = fighter.position - previousPosition;
        planarDelta.y = 0f;
        float movement = Mathf.Clamp01(planarDelta.magnitude / Mathf.Max(Time.deltaTime, 0.001f) / 4f);
        previousPosition = fighter.position;
        walkCycle += movement * Time.deltaTime * 11f;
        if (battle.IsBattleRunning && Mathf.FloorToInt(walkCycle / Mathf.PI) > Mathf.FloorToInt(previousWalkCycle / Mathf.PI))
            battle.PlayFootstep(fighter.position, isPlayer);
        previousWalkCycle = walkCycle;

        ApplyWeaponPose(isBlocking, attackDirection, blockDirection, phase, phaseTimer, phaseDuration, whiffRecovery);
        float legSwing = Mathf.Sin(walkCycle) * 28f * movement;
        leftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
        modelRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkCycle)) * 0.035f * movement);
        float whiffLean = whiffRecovery && phase == CombatPhase.AttackRecovery ? 8f : 0f;
        modelRoot.localRotation = Quaternion.Euler(staggerTimer > 0f ? -7f : whiffLean, 0f, isBlocking ? 5f : 0f);

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = hitFlashTimer > 0f ? Color.white : baseColors[i];
            colorProperties.SetColor("_BaseColor", color);
            colorProperties.SetColor("_Color", color);
            renderers[i].SetPropertyBlock(colorProperties);
        }
    }

    public void Fall()
    {
        fighter.rotation = Quaternion.Euler(78f, fighter.eulerAngles.y, Random.Range(-16f, 16f));
        fighter.position += Vector3.down * 0.36f;
    }

    private void ApplyWeaponPose(bool isBlocking, CombatDirection attackDirection, CombatDirection blockDirection,
        CombatPhase phase, float phaseTimer, float phaseDuration, bool whiffRecovery)
    {
        float progress = phaseDuration > 0f ? 1f - phaseTimer / phaseDuration : 0f;
        Vector3 prepared = attackDirection switch
        {
            CombatDirection.Left => new Vector3(-25f, -80f, -45f),
            CombatDirection.Right => new Vector3(-25f, 80f, 45f),
            CombatDirection.Up => new Vector3(-145f, 0f, -12f),
            _ => new Vector3(-5f, 0f, -8f)
        };
        Vector3 released = attackDirection switch
        {
            CombatDirection.Left => new Vector3(25f, 95f, 50f),
            CombatDirection.Right => new Vector3(25f, -95f, -50f),
            CombatDirection.Up => new Vector3(95f, 0f, -8f),
            _ => new Vector3(-5f, 0f, -8f)
        };

        Vector3 swordEuler = new Vector3(-18f, 0f, 0f);
        swordPivot.localPosition = swordBasePosition;
        if (phase == CombatPhase.AttackWindup)
            swordEuler = Vector3.Lerp(new Vector3(-18f, 0f, 0f), prepared, progress);
        else if (phase == CombatPhase.AttackHold)
            swordEuler = prepared;
        else if (phase == CombatPhase.AttackRelease)
        {
            swordEuler = Vector3.Lerp(prepared, released, progress);
            if (attackDirection == CombatDirection.Thrust)
                swordPivot.localPosition = swordBasePosition + Vector3.forward * Mathf.Sin(progress * Mathf.PI) * 0.7f;
        }
        else if (phase == CombatPhase.AttackRecovery)
        {
            Vector3 recoveryStart = whiffRecovery ? released + new Vector3(18f, 0f, 0f) : released;
            swordEuler = Vector3.Lerp(recoveryStart, new Vector3(-18f, 0f, 0f), progress);
        }

        swordPivot.localRotation = Quaternion.Euler(swordEuler);
        shieldPivot.localRotation = Quaternion.Euler(isBlocking ? GetBlockPose(blockDirection) : new Vector3(8f, 0f, 0f));
        leftArm.localRotation = Quaternion.Euler(isBlocking ? -55f : 0f, 0f, isBlocking ? -20f : 0f);
        rightArm.localRotation = Quaternion.Euler(IsAttacking(phase) ? swordEuler.x * 0.25f : 0f, 0f, -8f);
    }

    private static bool IsAttacking(CombatPhase phase) => phase == CombatPhase.AttackWindup
        || phase == CombatPhase.AttackHold || phase == CombatPhase.AttackRelease || phase == CombatPhase.AttackRecovery;

    private static Vector3 GetBlockPose(CombatDirection direction) => direction switch
    {
        CombatDirection.Left => new Vector3(-15f, -65f, -25f),
        CombatDirection.Right => new Vector3(-15f, 65f, 25f),
        CombatDirection.Up => new Vector3(-80f, 0f, 0f),
        _ => new Vector3(20f, 0f, 0f)
    };

    private static Transform NewPivot(string pivotName, Transform parent, Vector3 position)
    {
        Transform pivot = new GameObject(pivotName).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = position;
        return pivot;
    }

    private static void CreatePart(string partName, PrimitiveType type, Transform parent, Vector3 position,
        Vector3 scale, Color color, Vector3? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localEulerAngles = rotation ?? Vector3.zero;
        Object.Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
    }
}
