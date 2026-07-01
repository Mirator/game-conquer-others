using UnityEngine;
using UnityEngine.InputSystem;

// Player-controlled combatant: translates input (move, sprint, dodge, aim
// gestures, attack, block) into the BattleFighter combat actions.
public sealed class PlayerFighter : BattleFighter
{
    private const float ActionBufferDuration = 0.15f;

    private ThirdPersonCamera cameraRig;
    private float verticalVelocity;
    private float dodgeTimer;
    private float dodgeCooldown;
    private Vector3 dodgeDirection;
    private Vector3 smoothedMovement;
    private CombatDirection selectedDirection = CombatDirection.Right;
    private Vector2 gestureDelta;
    private float gestureTimer;
    private float attackBufferTimer;
    private bool bufferedAttackReleased;
    private float blockBufferTimer;
    private float blockLatchTimer;
    private float debugAimOverrideTimer;

    public CombatDirection SelectedDirection => selectedDirection;
    public bool IsRangedAiming => IsRanged && IsChargingAttack;

    public void SetCamera(ThirdPersonCamera rig)
    {
        cameraRig = rig;
    }

    protected override void Update()
    {
        base.Update();
        // Player control needs the camera rig for movement/aim framing; without it
        // (e.g. headless tests, or a frame before SetCamera) there is nothing to do.
        if (!IsAlive || IsInHitStop || battle == null || !battle.IsBattleRunning
            || cameraRig == null || Keyboard.current == null)
            return;

        if (Mouse.current != null)
        {
            debugAimOverrideTimer = Mathf.Max(0f, debugAimOverrideTimer - Time.unscaledDeltaTime);
            if (IsRanged && debugAimOverrideTimer <= 0f)
                SetAimDirection(cameraRig.GetProjectileAim(transform.position + Vector3.up * 1.45f));
            bool combatGestureActive = !IsRanged && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
            UpdateCombatGesture(combatGestureActive ? Mouse.current.delta.ReadValue() : Vector2.zero, combatGestureActive);

            bool blockHeld = !IsRanged && Mouse.current.rightButton.isPressed;
            if (Mouse.current.rightButton.wasPressedThisFrame)
                blockBufferTimer = ActionBufferDuration;
            blockBufferTimer = Mathf.Max(0f, blockBufferTimer - Time.unscaledDeltaTime);
            blockLatchTimer = Mathf.Max(0f, blockLatchTimer - Time.unscaledDeltaTime);
            bool wantsBlock = !IsRanged && (blockHeld || blockBufferTimer > 0f || blockLatchTimer > 0f);
            if (SetBlock(wantsBlock, selectedDirection) && blockBufferTimer > 0f)
            {
                blockBufferTimer = 0f;
                if (!blockHeld)
                    blockLatchTimer = 0.1f;
            }
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                attackBufferTimer = ActionBufferDuration;
                bufferedAttackReleased = false;
            }
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bufferedAttackReleased = true;
                ReleasePreparedAttack(true);
            }
            if (Mouse.current.leftButton.isPressed && !IsRanged)
                AimHeldAttack(selectedDirection);

            attackBufferTimer = Mathf.Max(0f, attackBufferTimer - Time.unscaledDeltaTime);
            CombatDirection attackDirection = IsRanged ? CombatDirection.Thrust : selectedDirection;
            if (attackBufferTimer > 0f && !wantsBlock && PrepareAttack(attackDirection))
            {
                attackBufferTimer = 0f;
                if (bufferedAttackReleased)
                    ReleasePreparedAttack(true);
            }
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = cameraRig.FlatForward;
        Vector3 right = cameraRig.FlatRight;
        Vector3 movement = forward * input.y + right * input.x;
        float speed = Keyboard.current.leftShiftKey.isPressed ? 7.4f : 4.7f;
        dodgeCooldown -= Time.deltaTime;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && dodgeTimer <= 0f && dodgeCooldown <= 0f && TrySpendStamina(32f))
        {
            dodgeDirection = movement.sqrMagnitude > 0.01f ? movement.normalized : transform.forward;
            dodgeTimer = 0.3f;
            dodgeCooldown = 0.6f;
            SetBlock(false, selectedDirection);
            cameraRig.AddShake(0.055f);
            cameraRig.AddImpulse(1.2f, 0.18f, dodgeDirection); // a quick shove in the dodge direction
        }

        if (dodgeTimer > 0f)
        {
            dodgeTimer -= Time.deltaTime;
            movement = dodgeDirection;
            speed = 11.5f;
        }

        if (controller.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += Physics.gravity.y * Time.deltaTime;

        smoothedMovement = Vector3.MoveTowards(smoothedMovement, movement * speed, 28f * Time.deltaTime);
        Vector3 velocity = smoothedMovement;
        velocity.y = verticalVelocity;
        Move(velocity);

        if (IsBlocking || Mouse.current != null && Mouse.current.leftButton.isPressed)
            FaceDirection(forward, 18f);
        else
            FaceDirection(movement);
    }

    private void UpdateCombatGesture(Vector2 delta, bool active)
    {
        if (!active)
        {
            gestureDelta = Vector2.zero;
            gestureTimer = 0f;
            return;
        }

        gestureDelta += delta;
        gestureTimer += Time.unscaledDeltaTime;
        if (CombatGesture.TryResolve(gestureDelta, out CombatDirection resolved))
        {
            selectedDirection = resolved;
            gestureDelta = Vector2.zero;
            gestureTimer = 0f;
        }
        else if (gestureTimer >= CombatGesture.Window)
        {
            gestureDelta = Vector2.zero;
            gestureTimer = 0f;
        }
    }

    public override void DebugAimAt(BattleFighter target)
    {
        base.DebugAimAt(target);
        debugAimOverrideTimer = 2f;
    }
}
