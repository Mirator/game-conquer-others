using UnityEngine;

// Owns the procedural fighter model and animation. BattleFighter supplies combat
// state, while this class translates that state into presentation.
public sealed class BattleFighterPresentation
{
    private readonly Transform fighter;
    private readonly Transform modelRoot;
    private readonly Transform swordPivot;
    private readonly Transform shieldPivot;
    private readonly Transform bowPivot;
    private readonly Transform arrowPivot;
    private readonly Transform bowStringUpper;
    private readonly Transform bowStringLower;
    private readonly TrailRenderer swordTrail;
    private readonly Transform leftLeg;
    private readonly Transform rightLeg;
    private readonly Transform leftArm;
    private readonly Transform rightArm;
    private readonly Renderer[] renderers;
    private readonly Color[] baseColors;
    private readonly MaterialPropertyBlock colorProperties = new();
    private readonly Vector3 swordBasePosition;
    private readonly Vector3 leftArmBasePosition;
    private readonly Vector3 rightArmBasePosition;
    private readonly float swordLength;
    private readonly WeaponType weapon;
    private readonly Vector3 bowUpperTip = new Vector3(0f, 1.02f, 0.34f);
    private readonly Vector3 bowLowerTip = new Vector3(0f, -1.02f, 0.34f);

    private Vector3 previousPosition;
    private float walkCycle;
    private float previousWalkCycle;

    public BattleFighterPresentation(Transform fighterTransform, Team team, UnitType unitType, WeaponType fighterWeapon)
    {
        fighter = fighterTransform;
        weapon = fighterWeapon;
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
        leftArmBasePosition = leftArm.localPosition;
        rightArmBasePosition = rightArm.localPosition;
        CreatePart("Left Arm Mesh", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.2f, 0.34f, 0.2f), metal);
        CreatePart("Right Arm Mesh", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.28f, 0f), new Vector3(0.2f, 0.34f, 0.2f), metal);

        swordPivot = NewPivot("Sword Pivot", rightArm, new Vector3(0f, -0.52f, 0.08f));
        swordBasePosition = swordPivot.localPosition;
        swordLength = weapon == WeaponType.TwoHandedSword ? 1.95f : 1.42f;
        CreatePart("Sword", PrimitiveType.Cube, swordPivot, new Vector3(0f, 0f, swordLength * 0.5f),
            new Vector3(weapon == WeaponType.TwoHandedSword ? 0.12f : 0.09f, 0.07f, swordLength), metal);
        CreatePart("Sword Guard", PrimitiveType.Cube, swordPivot, new Vector3(0f, 0f, 0.06f),
            new Vector3(weapon == WeaponType.TwoHandedSword ? 0.58f : 0.4f, 0.09f, 0.09f), leather);
        GameObject trailObject = new GameObject("Sword Trail");
        trailObject.transform.SetParent(swordPivot, false);
        trailObject.transform.localPosition = Vector3.forward * swordLength;
        swordTrail = trailObject.AddComponent<TrailRenderer>();
        swordTrail.time = weapon == WeaponType.TwoHandedSword ? 0.14f : 0.1f;
        swordTrail.minVertexDistance = 0.06f;
        swordTrail.startWidth = weapon == WeaponType.TwoHandedSword ? 0.1f : 0.065f;
        swordTrail.endWidth = 0f;
        swordTrail.emitting = false;
        swordTrail.material = RuntimeAssets.Material(team == Team.Allies
            ? new Color(0.22f, 0.56f, 1f, 0.7f) : new Color(1f, 0.26f, 0.12f, 0.7f), true);

        shieldPivot = NewPivot("Shield Pivot", leftArm, new Vector3(0f, -0.3f, 0.25f));
        CreatePart("Shield", PrimitiveType.Cylinder, shieldPivot, Vector3.zero, new Vector3(0.68f, 0.12f, 0.78f), teamColor, new Vector3(90f, 0f, 0f));
        CreatePart("Shield Boss", PrimitiveType.Sphere, shieldPivot, new Vector3(0f, 0f, -0.08f), Vector3.one * 0.22f, metal);

        bowPivot = NewPivot("Bow Pivot", leftArm, new Vector3(0f, -0.48f, 0.18f));
        Color bowWood = new Color(0.42f, 0.19f, 0.045f);
        Color bowHighlight = new Color(0.64f, 0.34f, 0.08f);
        CreateSegment("Bow Upper Inner", bowPivot, Vector3.zero, new Vector3(0f, 0.54f, 0.11f), 0.065f, bowHighlight);
        CreateSegment("Bow Upper Outer", bowPivot, new Vector3(0f, 0.54f, 0.11f), bowUpperTip, 0.055f, bowWood);
        CreateSegment("Bow Lower Inner", bowPivot, Vector3.zero, new Vector3(0f, -0.54f, 0.11f), 0.065f, bowHighlight);
        CreateSegment("Bow Lower Outer", bowPivot, new Vector3(0f, -0.54f, 0.11f), bowLowerTip, 0.055f, bowWood);
        CreatePart("Bow Grip", PrimitiveType.Cylinder, bowPivot, Vector3.zero, new Vector3(0.095f, 0.18f, 0.095f), leather);
        CreatePart("Bow Grip Wrap", PrimitiveType.Cylinder, bowPivot, Vector3.zero, new Vector3(0.115f, 0.1f, 0.115f), rankColor);
        Color stringColor = new Color(0.84f, 0.79f, 0.63f);
        bowStringUpper = CreateSegment("Bow String Upper", bowPivot, bowUpperTip, Vector3.zero, 0.014f, stringColor);
        bowStringLower = CreateSegment("Bow String Lower", bowPivot, bowLowerTip, Vector3.zero, 0.014f, stringColor);

        arrowPivot = NewPivot("Held Arrow", bowPivot, Vector3.zero);
        CreatePart("Held Arrow Shaft", PrimitiveType.Cylinder, arrowPivot, new Vector3(0f, 0f, 0.55f),
            new Vector3(0.025f, 0.62f, 0.025f), leather, new Vector3(90f, 0f, 0f));
        CreatePart("Held Arrow Tip", PrimitiveType.Cube, arrowPivot, new Vector3(0f, 0f, 1.18f),
            new Vector3(0.08f, 0.08f, 0.15f), metal);
        CreatePart("Held Arrow Fletching H", PrimitiveType.Cube, arrowPivot, new Vector3(0f, 0f, -0.04f),
            new Vector3(0.09f, 0.018f, 0.14f), teamColor);
        CreatePart("Held Arrow Fletching V", PrimitiveType.Cube, arrowPivot, new Vector3(0f, 0f, -0.04f),
            new Vector3(0.018f, 0.09f, 0.14f), teamColor);

        swordPivot.gameObject.SetActive(weapon != WeaponType.Bow);
        shieldPivot.gameObject.SetActive(weapon == WeaponType.SwordAndShield);
        bowPivot.gameObject.SetActive(weapon == WeaponType.Bow);
        arrowPivot.gameObject.SetActive(weapon == WeaponType.Bow);

        renderers = modelRoot.GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].sharedMaterial.color;
        previousPosition = fighter.position;
    }

    public void Update(BattleManager battle, bool isPlayer, bool isBlocking, CombatDirection attackDirection,
        CombatDirection blockDirection, CombatPhase phase, float phaseTimer, float phaseDuration,
        float staggerTimer, bool whiffRecovery, float hitFlashTimer, CombatDirection reactionDirection)
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
        swordTrail.emitting = weapon != WeaponType.Bow && phase == CombatPhase.AttackRelease;
        float legSwing = Mathf.Sin(walkCycle) * 28f * movement;
        float phaseProgress = phaseDuration > 0f ? Mathf.Clamp01(1f - phaseTimer / phaseDuration) : 0f;
        float attackWeight = phase == CombatPhase.AttackWindup ? phaseProgress
            : phase == CombatPhase.AttackHold ? 1f
            : phase == CombatPhase.AttackRelease ? Mathf.Sin(phaseProgress * Mathf.PI)
            : phase == CombatPhase.AttackRecovery ? 1f - phaseProgress : 0f;
        float heavy = weapon == WeaponType.TwoHandedSword ? 1.35f : 1f;
        float directionSign = attackDirection == CombatDirection.Left ? -1f
            : attackDirection == CombatDirection.Right ? 1f : 0f;
        float brace = IsAttacking(phase) ? attackWeight * heavy : isBlocking ? 0.45f : 0f;
        leftLeg.localRotation = Quaternion.Euler(legSwing - brace * 12f, 0f, brace * -6f);
        rightLeg.localRotation = Quaternion.Euler(-legSwing + brace * 9f, 0f, brace * 6f);

        float hitSign = reactionDirection == CombatDirection.Left ? 1f
            : reactionDirection == CombatDirection.Right ? -1f : 0f;
        float hitPitch = staggerTimer > 0f ? reactionDirection == CombatDirection.Up ? 13f : -9f : 0f;
        float hitRoll = staggerTimer > 0f ? hitSign * 14f : 0f;
        float releaseLean = phase == CombatPhase.AttackRelease ? -Mathf.Sin(phaseProgress * Mathf.PI) * 11f * heavy : 0f;
        float recoveryLean = whiffRecovery && phase == CombatPhase.AttackRecovery ? 11f * (1f - phaseProgress) : 0f;
        float blockRoll = isBlocking ? blockDirection == CombatDirection.Left ? -7f
            : blockDirection == CombatDirection.Right ? 7f : 0f : 0f;
        float torsoYaw = directionSign * attackWeight * 20f * heavy;
        modelRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkCycle)) * 0.035f * movement)
            + Vector3.forward * (-releaseLean * 0.008f);
        modelRoot.localRotation = Quaternion.Euler(hitPitch + releaseLean + recoveryLean,
            torsoYaw, hitRoll + blockRoll);

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = hitFlashTimer > 0f ? Color.white : baseColors[i];
            colorProperties.SetColor("_BaseColor", color);
            colorProperties.SetColor("_Color", color);
            renderers[i].SetPropertyBlock(colorProperties);
        }
    }

    public void Fall(CombatDirection direction)
    {
        swordTrail.emitting = false;
        float side = direction == CombatDirection.Left ? 1f : direction == CombatDirection.Right ? -1f : Random.Range(-1f, 1f);
        fighter.rotation = Quaternion.Euler(direction == CombatDirection.Up ? -74f : 76f,
            fighter.eulerAngles.y, side * 22f);
        fighter.position += Vector3.down * 0.34f;
        leftArm.localRotation = Quaternion.Euler(-105f, 0f, -45f);
        rightArm.localRotation = Quaternion.Euler(-85f, 0f, 50f);
        leftLeg.localRotation = Quaternion.Euler(34f, 0f, -18f);
        rightLeg.localRotation = Quaternion.Euler(-22f, 0f, 16f);
    }

    public bool IsTrailEmitting => swordTrail != null && swordTrail.emitting;

    private void ApplyWeaponPose(bool isBlocking, CombatDirection attackDirection, CombatDirection blockDirection,
        CombatPhase phase, float phaseTimer, float phaseDuration, bool whiffRecovery)
    {
        float progress = phaseDuration > 0f ? 1f - phaseTimer / phaseDuration : 0f;
        leftArm.localPosition = leftArmBasePosition;
        rightArm.localPosition = rightArmBasePosition;
        if (weapon == WeaponType.Bow)
        {
            bool drawing = phase == CombatPhase.AttackWindup || phase == CombatPhase.AttackHold;
            float draw = phase == CombatPhase.AttackHold ? 1f : phase == CombatPhase.AttackWindup ? progress : 0f;
            Vector3 nock = new Vector3(0f, 0f, -draw * 0.58f);
            bowPivot.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            SetSegment(bowStringUpper, bowUpperTip, nock, 0.014f);
            SetSegment(bowStringLower, bowLowerTip, nock, 0.014f);
            arrowPivot.localRotation = Quaternion.identity;
            arrowPivot.localPosition = nock;
            arrowPivot.gameObject.SetActive(drawing);
            leftArm.localRotation = Quaternion.Euler(-76f, 0f, -5f);
            rightArm.localRotation = Quaternion.Euler(Mathf.Lerp(-24f, 42f, draw), -8f,
                Mathf.Lerp(-8f, -100f, draw));
            return;
        }

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
        if (isBlocking && weapon == WeaponType.TwoHandedSword)
            swordEuler = GetTwoHandedBlockPose(blockDirection);
        else if (phase == CombatPhase.AttackWindup)
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
        if (weapon == WeaponType.TwoHandedSword)
        {
            bool activeGrip = isBlocking || IsAttacking(phase);
            float grip = activeGrip ? 1f : 0.55f;
            leftArm.localPosition = Vector3.Lerp(leftArmBasePosition, new Vector3(-0.17f, 1.48f, 0.08f), grip);
            rightArm.localPosition = Vector3.Lerp(rightArmBasePosition, new Vector3(0.18f, 1.48f, 0.08f), grip);
            Vector3 gripPose = GetTwoHandedGripPose(isBlocking ? blockDirection : attackDirection,
                activeGrip);
            leftArm.localRotation = Quaternion.Euler(gripPose);
            rightArm.localRotation = Quaternion.Euler(gripPose + new Vector3(8f, 0f, -16f));
        }
        else
        {
            leftArm.localRotation = Quaternion.Euler(isBlocking ? -55f : 0f, 0f, isBlocking ? -20f : 0f);
            rightArm.localRotation = Quaternion.Euler(IsAttacking(phase) ? swordEuler.x * 0.25f : 0f,
                0f, IsAttacking(phase) ? swordEuler.z * 0.16f - 8f : -8f);
        }
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

    private static Vector3 GetTwoHandedBlockPose(CombatDirection direction) => direction switch
    {
        CombatDirection.Left => new Vector3(-18f, -62f, -32f),
        CombatDirection.Right => new Vector3(-18f, 62f, 32f),
        CombatDirection.Up => new Vector3(-92f, 0f, 0f),
        _ => new Vector3(-20f, 0f, 0f)
    };

    private static Vector3 GetTwoHandedGripPose(CombatDirection direction, bool active)
    {
        if (!active)
            return new Vector3(-26f, 0f, 24f);
        return direction switch
        {
            CombatDirection.Left => new Vector3(-48f, -18f, 62f),
            CombatDirection.Right => new Vector3(-48f, 18f, -62f),
            CombatDirection.Up => new Vector3(-92f, 0f, 18f),
            _ => new Vector3(-58f, 0f, 28f)
        };
    }

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

    private static Transform CreateSegment(string segmentName, Transform parent, Vector3 start, Vector3 end,
        float thickness, Color color)
    {
        GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        segment.name = segmentName;
        segment.transform.SetParent(parent, false);
        Object.Destroy(segment.GetComponent<Collider>());
        segment.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color);
        SetSegment(segment.transform, start, end, thickness);
        return segment.transform;
    }

    private static void SetSegment(Transform segment, Vector3 start, Vector3 end, float thickness)
    {
        Vector3 delta = end - start;
        segment.localPosition = (start + end) * 0.5f;
        segment.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        segment.localScale = new Vector3(thickness, delta.magnitude * 0.5f, thickness);
    }
}
