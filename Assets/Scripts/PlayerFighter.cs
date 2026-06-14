using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerFighter : BattleFighter
{
    private ThirdPersonCamera cameraRig;
    private float verticalVelocity;
    private float dodgeTimer;
    private float dodgeCooldown;
    private Vector3 dodgeDirection;
    private Vector3 smoothedMovement;
    private CombatDirection selectedDirection = CombatDirection.Right;

    public void SetCamera(ThirdPersonCamera rig)
    {
        cameraRig = rig;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsAlive || battle == null || !battle.IsBattleRunning || Keyboard.current == null)
            return;

        if (Mouse.current != null)
        {
            selectedDirection = ReadDirection(Mouse.current.delta.ReadValue(), selectedDirection);
            SetBlock(Mouse.current.rightButton.isPressed, selectedDirection);
            if (Mouse.current.leftButton.wasPressedThisFrame)
                PrepareAttack(selectedDirection);
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                ReleasePreparedAttack();
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

    private static CombatDirection ReadDirection(Vector2 delta, CombatDirection fallback)
    {
        if (delta.sqrMagnitude < 4f)
            return fallback;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x < 0f ? CombatDirection.Left : CombatDirection.Right;
        return delta.y > 0f ? CombatDirection.Up : CombatDirection.Thrust;
    }
}
