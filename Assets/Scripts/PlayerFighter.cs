using UnityEngine;
using UnityEngine.InputSystem;

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

    public CombatDirection SelectedDirection => selectedDirection;

    public void SetCamera(ThirdPersonCamera rig)
    {
        cameraRig = rig;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsAlive || IsInHitStop || battle == null || !battle.IsBattleRunning || Keyboard.current == null)
            return;

        if (Mouse.current != null)
        {
            bool combatGestureActive = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
            UpdateCombatGesture(combatGestureActive ? Mouse.current.delta.ReadValue() : Vector2.zero, combatGestureActive);

            bool blockHeld = Mouse.current.rightButton.isPressed;
            if (Mouse.current.rightButton.wasPressedThisFrame)
                blockBufferTimer = ActionBufferDuration;
            blockBufferTimer = Mathf.Max(0f, blockBufferTimer - Time.unscaledDeltaTime);
            blockLatchTimer = Mathf.Max(0f, blockLatchTimer - Time.unscaledDeltaTime);
            bool wantsBlock = blockHeld || blockBufferTimer > 0f || blockLatchTimer > 0f;
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
            if (Mouse.current.leftButton.isPressed)
                AimHeldAttack(selectedDirection);

            attackBufferTimer = Mathf.Max(0f, attackBufferTimer - Time.unscaledDeltaTime);
            if (attackBufferTimer > 0f && !wantsBlock && PrepareAttack(selectedDirection))
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
}
