using UnityEngine;

// Owns the visible fighter model and combat presentation. BattleFighter supplies
// combat state, while this class translates that state into readable visuals.
public sealed class BattleFighterPresentation
{
    // Authored Quaternius humanoid bodies (valid avatars + curated animation
    // clips) drive the battle presentation; the procedural primitive rig remains
    // as a fallback when a catalog reference is unavailable.
    private const bool UseAuthoredFighterBodies = true;

    // How far to lower an authored corpse so the (root-motion-disabled) Death clip,
    // which lays the body out around hip height, rests on the ground instead of floating.
    private const float AuthoredDeathDrop = 0.85f;

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
    private readonly Vector3 bowUpperTip = new Vector3(0f, 1.02f, 0.34f);
    private readonly Vector3 bowLowerTip = new Vector3(0f, -1.02f, 0.34f);

    private Vector3 previousPosition;
    private float walkCycle;
    private float previousWalkCycle;
    private bool flashActive;
    // Smoothed feel state so combat poses ease instead of snapping between frames.
    private float guardWeight;
    private float attackWeightSmoothed;
    private float hitImpulse;
    private float hitPitchTarget;
    private float hitRollTarget;
    private float previousStaggerTimer;

    public BattleFighterPresentation(Transform fighterTransform, Team team, UnitType unitType, WeaponType fighterWeapon,
        Archetype archetype = Archetype.Soldier)
    {
        fighter = fighterTransform;
        weapon = fighterWeapon;
        bool isPlayerFighter = fighterTransform.name.StartsWith("Player");
        Color teamColor = team == Team.Allies ? new Color(0.12f, 0.39f, 0.82f) : new Color(0.72f, 0.12f, 0.08f);
        Color cloth = team == Team.Allies ? new Color(0.08f, 0.17f, 0.32f) : new Color(0.32f, 0.07f, 0.05f);
        Color metal = new Color(0.55f, 0.6f, 0.65f);
        Color leather = new Color(0.2f, 0.1f, 0.04f);
        // Captains wear a bright crest so they read as the elite target.
        Color rankColor = archetype == Archetype.Captain ? new Color(0.96f, 0.84f, 0.26f)
            : unitType == UnitType.Guard ? new Color(0.92f, 0.72f, 0.18f)
            : unitType == UnitType.Veteran ? new Color(0.62f, 0.66f, 0.68f) : leather;
        modelRoot = new GameObject("Animated Model").transform;
        modelRoot.SetParent(fighter, false);
        // A larger silhouette marks the captain without touching the collider.
        if (archetype == Archetype.Captain)
            modelRoot.localScale = Vector3.one * 1.18f;
        PresentationCatalog catalog = PresentationCatalog.Load();
        GameObject authoredPrefab = UseAuthoredFighterBodies && catalog != null
            ? catalog.Fighter(unitType, team, isPlayerFighter) : null;
        if (authoredPrefab != null)
        {
            GameObject authored = Object.Instantiate(authoredPrefab, modelRoot);
            authored.name = "Authored Fighter View";
            StripUnsafeGeneratedAccents(authored.transform);
            authoredView = authored.GetComponentInChildren<FighterView>(true);
            hasAuthoredModel = true;
            authoredView?.ApplyTeam(team);
            // The player goes bare-headed so their face reads as the hero; the ranger
            // body's hood would otherwise hide the authored head. Other ranger-based
            // units (enemy captains, veterans, guards) keep their hood.
            if (isPlayerFighter)
                foreach (Renderer renderer in authored.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.Contains("Head_Hood"))
                        renderer.enabled = false;
        }
        // The procedural primitive body is only the fallback look. When an authored
        // model drives the visuals these 13 cosmetic meshes were created and then
        // immediately hidden — at 120 fighters that is ~1,500 needless GameObjects and
        // renderers. Skip creating them outright on the authored path. The animated
        // pivots (legs, arms, and the weapon mounts parented to the arms) are NOT
        // cosmetic and are always created below.
        if (!hasAuthoredModel)
        {
            CreatePart("Torso", PrimitiveType.Capsule, modelRoot, new Vector3(0f, 1.13f, 0f), new Vector3(0.56f, 0.7f, 0.4f), cloth);
            CreatePart("Chest Plate", PrimitiveType.Cube, modelRoot, new Vector3(0f, 1.2f, 0.25f), new Vector3(0.42f, 0.5f, 0.08f), teamColor);
            CreatePart("Back Heraldry", PrimitiveType.Cube, modelRoot, new Vector3(0f, 1.22f, -0.255f), new Vector3(0.24f, 0.5f, 0.055f), teamColor);
            CreatePart("Head", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.76f, 0f), Vector3.one * 0.32f, new Color(0.72f, 0.5f, 0.32f));
            CreatePart("Helmet", PrimitiveType.Sphere, modelRoot, new Vector3(0f, 1.86f, 0f), new Vector3(0.38f, 0.22f, 0.38f), metal);
            CreatePart("Helmet Ridge", PrimitiveType.Cube, modelRoot, new Vector3(0f, 1.98f, 0f),
                unitType == UnitType.Guard ? new Vector3(0.13f, 0.25f, 0.48f) : new Vector3(0.08f, 0.16f, 0.44f), rankColor);
            CreatePart("Belt", PrimitiveType.Cube, modelRoot, new Vector3(0f, 0.88f, 0f), new Vector3(0.58f, 0.09f, 0.44f), leather);
        }

        leftLeg = NewPivot("Left Leg", modelRoot, new Vector3(-0.17f, 0.76f, 0f));
        rightLeg = NewPivot("Right Leg", modelRoot, new Vector3(0.17f, 0.76f, 0f));
        if (!hasAuthoredModel)
        {
            CreatePart("Left Leg Mesh", PrimitiveType.Capsule, leftLeg, new Vector3(0f, -0.38f, 0f), new Vector3(0.18f, 0.44f, 0.18f), leather);
            CreatePart("Right Leg Mesh", PrimitiveType.Capsule, rightLeg, new Vector3(0f, -0.38f, 0f), new Vector3(0.18f, 0.44f, 0.18f), leather);
        }

        leftArm = NewPivot("Left Arm", modelRoot, new Vector3(-0.35f, 1.42f, 0.02f));
        rightArm = NewPivot("Right Arm", modelRoot, new Vector3(0.35f, 1.42f, 0.02f));
        leftArmBasePosition = leftArm.localPosition;
        rightArmBasePosition = rightArm.localPosition;
        if (!hasAuthoredModel)
        {
            CreatePart("Left Shoulder Pad", PrimitiveType.Sphere, modelRoot, new Vector3(-0.42f, 1.43f, 0.02f), new Vector3(0.22f, 0.14f, 0.24f), metal);
            CreatePart("Right Shoulder Pad", PrimitiveType.Sphere, modelRoot, new Vector3(0.42f, 1.43f, 0.02f), new Vector3(0.22f, 0.14f, 0.24f), metal);
            CreatePart("Left Arm Mesh", PrimitiveType.Capsule, leftArm, new Vector3(0f, -0.31f, 0f), new Vector3(0.16f, 0.37f, 0.16f), metal);
            CreatePart("Right Arm Mesh", PrimitiveType.Capsule, rightArm, new Vector3(0f, -0.31f, 0f), new Vector3(0.16f, 0.37f, 0.16f), metal);
        }

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
            // The Quaternius blade ships with a flat white, untextured FBX material, so
            // it reads as a glowing white shard. Repaint every slot to steel.
            RuntimeAssets.TintModel(sword, metal);
        }
        // The swing trail is a close-up flourish that is invisible amid a 120-fighter
        // melee but costs dynamic trail-mesh generation per emitting blade. Build it
        // for the player only; AI swords simply have no trail (swordTrail stays null
        // and every use below is null-guarded).
        if (isPlayerFighter)
        {
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
        }

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
            // Same white-FBX-material problem as the sword: the shield otherwise renders
            // as a near-white glassy disc. Paint it in the team colour so it reads as a
            // solid shield and reinforces team identity in the melee.
            RuntimeAssets.TintModel(shield, teamColor);
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
        // Authored bow/arrow replace the primitive rig when wired (same pattern as the
        // sword/shield above): hide the primitive parts and mount the model on the pivot.
        // The animated string transforms are kept (now invisible) so the draw update never
        // null-refs. NOTE: model rotation/scale are first-cut and may want a visual nudge.
        if (catalog != null && catalog.bowPrefab != null)
        {
            foreach (Renderer part in bowPivot.GetComponentsInChildren<Renderer>())
                if (!part.transform.IsChildOf(arrowPivot))
                    part.enabled = false;
            GameObject bow = Object.Instantiate(catalog.bowPrefab, bowPivot);
            bow.name = "Authored Bow";
            bow.transform.localPosition = Vector3.zero;
            bow.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        if (catalog != null && catalog.arrowPrefab != null)
        {
            foreach (Renderer part in arrowPivot.GetComponentsInChildren<Renderer>())
                part.enabled = false;
            GameObject heldArrow = Object.Instantiate(catalog.arrowPrefab, arrowPivot);
            heldArrow.name = "Authored Held Arrow";
            heldArrow.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            heldArrow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        swordPivot.gameObject.SetActive(weapon != WeaponType.Bow);
        shieldPivot.gameObject.SetActive(weapon == WeaponType.SwordAndShield);
        bowPivot.gameObject.SetActive(weapon == WeaponType.Bow);
        arrowPivot.gameObject.SetActive(weapon == WeaponType.Bow);

        renderers = modelRoot.GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            // Authored fighters carry their team colour in a MaterialPropertyBlock
            // (set by FighterView.ApplyTeam above), not in the shared material. Capture
            // that override as the base colour so the hit-flash restore returns the red
            // cloth instead of wiping it to the FBX material's neutral default.
            Material shared = renderers[i].sharedMaterial;
            Color fallback = shared != null ? shared.color : Color.white;
            if (renderers[i].HasPropertyBlock())
            {
                renderers[i].GetPropertyBlock(colorProperties);
                Color tint = colorProperties.GetColor("_BaseColor");
                baseColors[i] = tint.a > 0f ? tint : fallback;
            }
            else
            {
                baseColors[i] = fallback;
            }
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
        if (swordTrail != null)
            swordTrail.emitting = weapon != WeaponType.Bow && phase == CombatPhase.AttackRelease;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        // The guard eases up quickly and lowers a touch slower, so raising a block
        // reads as a deliberate motion rather than an instant snap.
        guardWeight = Mathf.MoveTowards(guardWeight, isBlocking ? 1f : 0f, dt * (isBlocking ? 13f : 9f));
        // A fresh stagger (the timer jumps up) fires a sharp directional recoil scaled
        // by the hit's severity, which then eases out — no constant lean that pops back
        // to idle. Perfect blocks barely flinch; a full unblocked hit snaps hard.
        if (staggerTimer > previousStaggerTimer + 0.0001f)
        {
            hitImpulse = Mathf.Clamp01(staggerTimer / 0.24f);
            hitPitchTarget = reactionDirection == CombatDirection.Up ? 16f : -10f;
            float sign = reactionDirection == CombatDirection.Left ? 1f
                : reactionDirection == CombatDirection.Right ? -1f : 0f;
            hitRollTarget = sign * 16f;
        }
        previousStaggerTimer = staggerTimer;
        hitImpulse = Mathf.MoveTowards(hitImpulse, 0f, dt * 4.5f);

        float legSwing = Mathf.Sin(walkCycle) * 28f * movement;
        float phaseProgress = phaseDuration > 0f ? Mathf.Clamp01(1f - phaseTimer / phaseDuration) : 0f;
        float attackWeight = phase == CombatPhase.AttackWindup ? phaseProgress
            : phase == CombatPhase.AttackHold ? 1f
            : phase == CombatPhase.AttackRelease ? Mathf.Sin(phaseProgress * Mathf.PI)
            : phase == CombatPhase.AttackRecovery ? 1f - phaseProgress : 0f;
        // Easing the weight removes the brief dip at the hold-to-release hand-off and
        // softens the settle back to idle when a swing ends.
        attackWeightSmoothed = Mathf.MoveTowards(attackWeightSmoothed, attackWeight, dt * 9f);
        float heavy = weapon == WeaponType.TwoHandedSword ? 1.35f : 1f;
        float directionSign = attackDirection == CombatDirection.Left ? -1f
            : attackDirection == CombatDirection.Right ? 1f : 0f;
        float brace = IsAttacking(phase) ? attackWeightSmoothed * heavy : guardWeight * 0.45f;
        leftLeg.localRotation = Quaternion.Euler(legSwing - brace * 12f, 0f, brace * -6f);
        rightLeg.localRotation = Quaternion.Euler(-legSwing + brace * 9f, 0f, brace * 6f);

        // The on-hit recoil kick is discretionary motion, so reduced-motion drops it
        // (the eased transitions stay — they remove jarring snaps rather than add motion).
        float hitMotion = SettingsService.Current is { reduceMotion: true } ? 0f : hitImpulse;
        float hitPitch = hitPitchTarget * hitMotion;
        float hitRoll = hitRollTarget * hitMotion;
        float releaseLean = phase == CombatPhase.AttackRelease ? -Mathf.Sin(phaseProgress * Mathf.PI) * 11f * heavy : 0f;
        float recoveryLean = whiffRecovery && phase == CombatPhase.AttackRecovery ? 11f * (1f - phaseProgress) : 0f;
        float blockRollDir = blockDirection == CombatDirection.Left ? -7f
            : blockDirection == CombatDirection.Right ? 7f : 0f;
        float blockRoll = blockRollDir * guardWeight;
        float torsoYaw = directionSign * attackWeightSmoothed * 20f * heavy;
        modelRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkCycle)) * 0.035f * movement)
            + Vector3.forward * (-releaseLean * 0.008f);
        modelRoot.localRotation = Quaternion.Euler(hitPitch + releaseLean + recoveryLean,
            torsoYaw, hitRoll + blockRoll);

        // The hit-flash whitens every renderer, but the colour only changes on the
        // frames the flash turns on or off. Rewriting ~30 property blocks per fighter
        // every frame is the dominant presentation cost at scale, so only touch them
        // on a flash transition: white on, base colour off. The per-instance colour
        // rides GPU instancing, and team colour never changes mid-battle, so ApplyTeam
        // stays a construction-time call.
        bool wantFlash = hitFlashTimer > 0f;
        if (wantFlash != flashActive)
        {
            flashActive = wantFlash;
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = wantFlash ? Color.white : baseColors[i];
                colorProperties.SetColor("_BaseColor", color);
                colorProperties.SetColor("_Color", color);
                renderers[i].SetPropertyBlock(colorProperties);
            }
        }
    }

    public void Fall(CombatDirection direction)
    {
        authoredView?.UpdateState(0f, false, CombatPhase.Idle, 0f, false);
        if (swordTrail != null)
            swordTrail.emitting = false;
        // Authored humanoids collapse through the Death animation clip, which lays
        // the body out on the ground in local space. The manual tilt/sink and limb
        // posing below exist only for the primitive fallback rig, which has no death
        // clip. Stacking that 76-degree root tilt on top of the humanoid clip threw
        // the corpse to a broken, half-submerged angle (it read as "drowning"), so it
        // is gated to the primitive path like the rest of the procedural posing here.
        if (hasAuthoredModel)
        {
            // Vary only the facing (yaw) of the laid-out corpse so deaths don't all face
            // the same way — never pitch/roll, which re-submerges the humanoid death
            // clip (the "drowning" failure described above).
            fighter.rotation = Quaternion.Euler(0f, fighter.eulerAngles.y + Random.Range(-60f, 60f), 0f);
            // The Death clip plays with root motion disabled, so it lays the body out
            // around the hips (~standing height) without lowering the root — leaving the
            // corpse floating. Drop the root to rest the body on the ground. Vertical
            // only: any tilt re-submerges the clip (the "drowning" failure above).
            fighter.position += Vector3.down * AuthoredDeathDrop;
            return;
        }
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

        if (hasAuthoredModel)
        {
            // FighterView drives the sword arm procedurally, so the primitive-rig swing
            // math below is skipped (it would only be computed and discarded). Hold the
            // fixed blade grip and orient the shield on the hand.
            swordPivot.localPosition = swordBasePosition;
            swordPivot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            shieldPivot.localRotation = Damp(shieldPivot.localRotation,
                Quaternion.Euler(isBlocking ? GetBlockPose(blockDirection) : new Vector3(-12f, 0f, -8f)), 18f);
        }
        else
        {
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
            swordPivot.localRotation = Quaternion.Euler(swordEuler);
            shieldPivot.localRotation = Damp(shieldPivot.localRotation,
                Quaternion.Euler(isBlocking ? GetBlockPose(blockDirection) : new Vector3(-12f, 0f, -8f)), 18f);
            if (weapon == WeaponType.TwoHandedSword)
            {
                bool activeGrip = isBlocking || IsAttacking(phase);
                float grip = activeGrip ? 1f : 0.55f;
                leftArm.localPosition = Vector3.Lerp(leftArmBasePosition, new Vector3(-0.17f, 1.48f, 0.08f), grip);
                rightArm.localPosition = Vector3.Lerp(rightArmBasePosition, new Vector3(0.18f, 1.48f, 0.08f), grip);
                Vector3 gripPose = GetTwoHandedGripPose(isBlocking ? blockDirection : attackDirection, activeGrip);
                leftArm.localRotation = Quaternion.Euler(gripPose);
                rightArm.localRotation = Quaternion.Euler(gripPose + new Vector3(8f, 0f, -16f));
            }
            else
            {
                leftArm.localRotation = Quaternion.Euler(isBlocking ? -55f : -32f, 0f, isBlocking ? -20f : -12f);
                rightArm.localRotation = Quaternion.Euler(IsAttacking(phase) ? swordEuler.x * 0.25f : 18f,
                    0f, IsAttacking(phase) ? swordEuler.z * 0.16f - 8f : -18f);
            }
        }
        // The view interpolates the swing per phase, so it just needs the phase and
        // the 0..1 progress within that phase.
        authoredView?.SetCombatPose(weapon, isBlocking, attackDirection, blockDirection, phase, progress);
    }

    private static bool IsAttacking(CombatPhase phase) => phase == CombatPhase.AttackWindup
        || phase == CombatPhase.AttackHold || phase == CombatPhase.AttackRelease || phase == CombatPhase.AttackRecovery;

    // Frame-rate-independent exponential smoothing toward a target rotation.
    private static Quaternion Damp(Quaternion current, Quaternion target, float rate)
        => Quaternion.Slerp(current, target, 1f - Mathf.Exp(-rate * Time.deltaTime));

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
