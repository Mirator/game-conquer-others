using UnityEngine;

// Owns the visible fighter model and combat presentation. BattleFighter supplies
// combat state, while this class translates that state into readable visuals.
public sealed class BattleFighterPresentation
{
    // Authored Quaternius humanoid bodies (valid avatars + curated animation
    // clips) drive the battle presentation; the procedural primitive rig remains
    // as a fallback when a catalog reference is unavailable.
    private const bool UseAuthoredFighterBodies = true;

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
    private readonly FighterView authoredView;
    private readonly bool hasAuthoredModel;
    private readonly Renderer[] renderers;
    private readonly Color[] baseColors;
    private readonly MaterialPropertyBlock colorProperties = new();
    private Vector3 swordBasePosition;
    private readonly Vector3 leftArmBasePosition;
    private readonly Vector3 rightArmBasePosition;
    private readonly float swordLength;
    private readonly WeaponType weapon;
    private readonly Team fighterTeam;
    private readonly Vector3 bowUpperTip = new Vector3(0f, 1.02f, 0.34f);
    private readonly Vector3 bowLowerTip = new Vector3(0f, -1.02f, 0.34f);

    private Vector3 previousPosition;
    private float walkCycle;
    private float previousWalkCycle;

    public BattleFighterPresentation(Transform fighterTransform, Team team, UnitType unitType, WeaponType fighterWeapon)
    {
        fighter = fighterTransform;
        weapon = fighterWeapon;
        fighterTeam = team;
        Color teamColor = team == Team.Allies ? new Color(0.12f, 0.39f, 0.82f) : new Color(0.72f, 0.12f, 0.08f);
        Color cloth = team == Team.Allies ? new Color(0.08f, 0.17f, 0.32f) : new Color(0.32f, 0.07f, 0.05f);
        Color metal = new Color(0.55f, 0.6f, 0.65f);
        Color leather = new Color(0.2f, 0.1f, 0.04f);
        Color rankColor = unitType == UnitType.Guard ? new Color(0.92f, 0.72f, 0.18f)
            : unitType == UnitType.Veteran ? new Color(0.62f, 0.66f, 0.68f) : leather;
        modelRoot = new GameObject("Animated Model").transform;
        modelRoot.SetParent(fighter, false);
        PresentationCatalog catalog = PresentationCatalog.Load();
        GameObject authoredPrefab = UseAuthoredFighterBodies && catalog != null
            ? catalog.Fighter(unitType, team, fighter.name.StartsWith("Player")) : null;
        if (authoredPrefab != null)
        {
            GameObject authored = Object.Instantiate(authoredPrefab, modelRoot);
            authored.name = "Authored Fighter View";
            StripUnsafeGeneratedAccents(authored.transform);
            authoredView = authored.GetComponentInChildren<FighterView>(true);
            hasAuthoredModel = true;
            authoredView?.ApplyTeam(team);
        }
        CreatePart("Torso", PrimitiveType.Capsule, modelRoot, new Vector3(0f, 1.13f, 0f), new Vector3(0.56f, 0.7f, 0.4f), cloth);
        CreatePart("Chest Plate", PrimitiveType.Cube, modelRoot, new Vector3(0f, 1.2f, 0.25f), new Vector3(0.42f, 0.5f, 0.08f), teamColor);
        CreatePart("Back Heraldry", PrimitiveType.Cube, modelRoot, new Vector3(0f, 1.22f, -0.255f), new Vector3(0.24f, 0.5f, 0.055f), teamColor);
        CreatePart("Head", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.76f, 0f), Vector3.one * 0.32f, new Color(0.72f, 0.5f, 0.32f));
        CreatePart("Helmet", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.86f, 0f), new Vector3(0.38f, 0.22f, 0.38f), metal);
        CreatePart("Helmet Ridge", PrimitiveType.Cube, modelRoot, new Vector3(0f, 1.98f, 0f),
            unitType == UnitType.Guard ? new Vector3(0.13f, 0.25f, 0.48f) : new Vector3(0.08f, 0.16f, 0.44f), rankColor);
        CreatePart("Belt", PrimitiveType.Cube, modelRoot, new Vector3(0f, 0.88f, 0f), new Vector3(0.58f, 0.09f, 0.44f), leather);

        leftLeg = NewPivot("Left Leg", modelRoot, new Vector3(-0.17f, 0.76f, 0f));
        rightLeg = NewPivot("Right Leg", modelRoot, new Vector3(0.17f, 0.76f, 0f));
        CreatePart("Left Leg Mesh", PrimitiveType.Capsule, leftLeg, new Vector3(0f, -0.38f, 0f), new Vector3(0.18f, 0.44f, 0.18f), leather);
        CreatePart("Right Leg Mesh", PrimitiveType.Capsule, rightLeg, new Vector3(0f, -0.38f, 0f), new Vector3(0.18f, 0.44f, 0.18f), leather);

        leftArm = NewPivot("Left Arm", modelRoot, new Vector3(-0.35f, 1.42f, 0.02f));
        rightArm = NewPivot("Right Arm", modelRoot, new Vector3(0.35f, 1.42f, 0.02f));
        leftArmBasePosition = leftArm.localPosition;
        rightArmBasePosition = rightArm.localPosition;
        CreatePart("Left Shoulder Pad", PrimitiveType.Sphere, modelRoot, new Vector3(-0.42f, 1.43f, 0.02f), new Vector3(0.22f, 0.14f, 0.24f), metal);
        CreatePart("Right Shoulder Pad", PrimitiveType.Sphere, modelRoot, new Vector3(0.42f, 1.43f, 0.02f), new Vector3(0.22f, 0.14f, 0.24f), metal);
        CreatePart("Left Arm Mesh", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.31f, 0f), new Vector3(0.16f, 0.37f, 0.16f), metal);
        CreatePart("Right Arm Mesh", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.31f, 0f), new Vector3(0.16f, 0.37f, 0.16f), metal);

        swordPivot = NewPivot("Sword Pivot", rightArm, new Vector3(0.02f, -0.58f, 0.15f));
        swordBasePosition = swordPivot.localPosition;
        swordLength = weapon == WeaponType.TwoHandedSword ? 1.95f : 1.42f;
        CreatePart("Sword", PrimitiveType.Cube, swordPivot, new Vector3(0f, 0f, swordLength * 0.5f),
            new Vector3(weapon == WeaponType.TwoHandedSword ? 0.12f : 0.09f, 0.07f, swordLength), metal);
        CreatePart("Sword Guard", PrimitiveType.Cube, swordPivot, new Vector3(0f, 0f, 0.06f),
            new Vector3(weapon == WeaponType.TwoHandedSword ? 0.58f : 0.4f, 0.09f, 0.09f), leather);
        if (catalog != null && catalog.swordPrefab != null)
        {
            SetChildRendererEnabled(swordPivot, "Sword", false);
            SetChildRendererEnabled(swordPivot, "Sword Guard", false);
            GameObject sword = Object.Instantiate(catalog.swordPrefab, swordPivot);
            sword.name = "Authored Sword";
            sword.transform.localPosition = Vector3.zero;
            sword.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Quaternius props embed a 100x FBX scale; multiply it rather than
            // overwriting so the blade keeps real-world size.
            sword.transform.localScale *= weapon == WeaponType.TwoHandedSword ? 1.3f : 1.0f;
        }
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

        shieldPivot = NewPivot("Shield Pivot", leftArm, new Vector3(-0.04f, -0.38f, 0.3f));
        CreatePart("Shield", PrimitiveType.Cylinder, shieldPivot, Vector3.zero, new Vector3(0.54f, 0.1f, 0.66f), teamColor, new Vector3(90f, 0f, 0f));
        CreatePart("Shield Boss", PrimitiveType.Sphere, shieldPivot, new Vector3(0f, 0f, -0.08f), Vector3.one * 0.22f, metal);
        if (catalog != null && catalog.shieldPrefab != null)
        {
            SetChildRendererEnabled(shieldPivot, "Shield", false);
            SetChildRendererEnabled(shieldPivot, "Shield Boss", false);
            GameObject shield = Object.Instantiate(catalog.shieldPrefab, shieldPivot);
            shield.name = "Authored Shield";
            shield.transform.localPosition = Vector3.zero;
            shield.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shield.transform.localScale *= 0.7f;
        }

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
        {
            Material shared = renderers[i].sharedMaterial;
            baseColors[i] = shared != null ? shared.color : Color.white;
        }
        if (hasAuthoredModel)
        {
            SetProceduralBodyVisible(false);
            AttachWeaponPivotsToAuthoredHands();
            authoredView?.SetWeaponPivot(swordPivot);
            PositionTrailAtAuthoredBladeTip();
        }
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
        // The library jog is a deep sprint pose that makes an entire AI formation
        // unreadable. Keep formation locomotion upright; reserve jog for the player.
        authoredView?.UpdateState(isPlayer ? movement : Mathf.Min(movement, 0.6f),
            isBlocking, phase, staggerTimer, true, attackDirection, !isPlayer);

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
        authoredView?.ApplyTeam(fighterTeam);
    }

    public void Fall(CombatDirection direction)
    {
        authoredView?.UpdateState(0f, false, CombatPhase.Idle, 0f, false);
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
            // On authored humanoids the bow rides the hand bone, whose axes are
            // arbitrary; hold it body-aligned (vertical, arrow forward) instead of
            // the primitive-rig local pose, which left it pointing sideways.
            if (hasAuthoredModel)
                bowPivot.rotation = fighter.rotation;
            else
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

        Vector3 swordEuler = new Vector3(72f, 0f, 8f);
        swordPivot.localPosition = swordBasePosition;
        if (isBlocking && weapon == WeaponType.TwoHandedSword)
            swordEuler = GetTwoHandedBlockPose(blockDirection);
        else if (phase == CombatPhase.AttackWindup)
            swordEuler = Vector3.Lerp(new Vector3(72f, 0f, 8f), prepared, progress);
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
            swordEuler = Vector3.Lerp(recoveryStart, new Vector3(72f, 0f, 8f), progress);
        }

        // Authored fighters carry the sword in a fixed blade-up grip on the hand
        // bone and let the Sword_Idle/Attack/Block clips swing the arm. The
        // procedural per-phase angles below are tuned for the primitive rig and
        // would force the blade flat-forward (a "lance") on the authored hand.
        if (hasAuthoredModel)
        {
            swordEuler = new Vector3(-90f, 0f, 0f);
            swordPivot.localPosition = swordBasePosition;
        }
        swordPivot.localRotation = Quaternion.Euler(swordEuler);
        shieldPivot.localRotation = Quaternion.Euler(isBlocking ? GetBlockPose(blockDirection) : new Vector3(-12f, 0f, -8f));
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
            leftArm.localRotation = Quaternion.Euler(isBlocking ? -55f : -32f, 0f, isBlocking ? -20f : -12f);
            rightArm.localRotation = Quaternion.Euler(IsAttacking(phase) ? swordEuler.x * 0.25f : 18f,
                0f, IsAttacking(phase) ? swordEuler.z * 0.16f - 8f : -18f);
        }
        // The view interpolates the swing per phase, so it just needs the phase and
        // the 0..1 progress within that phase.
        authoredView?.SetCombatPose(weapon, isBlocking, attackDirection, blockDirection, phase, progress);
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

    private void SetProceduralBodyVisible(bool visible)
    {
        string[] names =
        {
            "Torso", "Chest Plate", "Back Heraldry", "Head", "Helmet", "Helmet Ridge", "Belt",
            "Left Leg Mesh", "Right Leg Mesh", "Left Arm Mesh", "Right Arm Mesh",
            "Left Shoulder Pad", "Right Shoulder Pad"
        };
        foreach (string name in names)
            SetChildRendererEnabled(modelRoot, name, visible);
    }

    private static void SetChildRendererEnabled(Transform root, string childName, bool enabled)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.gameObject.name == childName)
                renderer.enabled = enabled;
    }

    private static void StripUnsafeGeneratedAccents(Transform root)
    {
        string[] unsafeNames =
        {
            "Captain Crest", "Captain Sash", "Veteran Sash", "Guard Crest",
            "Guard Badge", "Enemy Sash", "Militia Patch"
        };
        foreach (string unsafeName in unsafeNames)
        {
            Transform child = FindDeepChild(root, unsafeName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
        }
    }

    // The sword trail was placed for the old primitive blade (forward * swordLength).
    // The authored blade is shorter and runs along a different local axis, so move the
    // trail to the authored blade's actual tip (computed from its mesh bounds).
    private void PositionTrailAtAuthoredBladeTip()
    {
        if (swordTrail == null)
            return;
        Transform authored = FindDeepChild(swordPivot, "Authored Sword");
        if (authored == null)
            return;
        Bounds local = default;
        bool found = false;
        foreach (MeshFilter mf in authored.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
                continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                Vector3 lp = swordPivot.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!found) { local = new Bounds(lp, Vector3.zero); found = true; }
                else local.Encapsulate(lp);
            }
        }
        if (!found)
            return;
        // The blade's longest local axis is its length; the tip is the far end of it.
        Vector3 size = local.size;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        float tip = Mathf.Abs(local.min[axis]) >= Mathf.Abs(local.max[axis]) ? local.min[axis] : local.max[axis];
        Vector3 pos = local.center;
        pos[axis] = tip;
        swordTrail.transform.localPosition = pos;
    }

    private void AttachWeaponPivotsToAuthoredHands()
    {
        if (authoredView == null || authoredView.anchors == null)
            return;
        if (authoredView.anchors.rightHand != null)
        {
            swordPivot.SetParent(authoredView.anchors.rightHand, false);
            swordPivot.localPosition = new Vector3(0.02f, 0.02f, 0.06f);
            swordBasePosition = swordPivot.localPosition;
        }
        if (authoredView.anchors.leftHand != null)
        {
            shieldPivot.SetParent(authoredView.anchors.leftHand, false);
            shieldPivot.localPosition = new Vector3(0.02f, 0.02f, 0.05f);
            bowPivot.SetParent(authoredView.anchors.leftHand, false);
            bowPivot.localPosition = new Vector3(0.02f, 0.02f, 0.05f);
            bowPivot.localScale = Vector3.one * 0.7f;
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;
            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
