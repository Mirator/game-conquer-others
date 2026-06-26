using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ThirdPersonCamera : MonoBehaviour
{
    public Vector3 FlatForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    public Vector3 FlatRight => Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

    public Vector3 GetProjectileAim(Vector3 origin)
    {
        Vector3 crosshairPoint = transform.position + transform.forward * 80f;
        return (crosshairPoint - origin).normalized;
    }

    private Transform target;
    private PlayerFighter player;
    private Camera attachedCamera;
    private Vector3 previousTargetPosition;
    private Vector3 smoothPosition;
    private float yaw;
    private float pitch = 12f;
    private float shake;
    private float movementAmount;
    private float stride;
    private int cameraCollisionMask = ~0;
    private bool sweeping;
    private float sweepTime;
    private float sweepDuration;
    private Vector3 sweepStart;
    private Vector3 sweepLook;

    public bool IsSweeping => sweeping;
    public void SkipSweep() => sweeping = false;

    // Plays a one-off establishing flyover from startPosition (looking at lookPoint)
    // that eases into the normal over-the-shoulder follow. Runs on unscaled time.
    public void PlaySweep(Vector3 startPosition, Vector3 lookPoint, float duration)
    {
        sweepStart = startPosition;
        sweepLook = lookPoint;
        sweepDuration = Mathf.Max(0.1f, duration);
        sweepTime = 0f;
        sweeping = true;
        transform.position = startPosition;
    }

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        player = followTarget.GetComponent<PlayerFighter>();
        yaw = followTarget.eulerAngles.y;
        attachedCamera = GetComponent<Camera>();
        previousTargetPosition = followTarget.position;
        smoothPosition = transform.position;
        cameraCollisionMask = ~LayerMask.GetMask("Ignore Raycast");
    }

    public void AddShake(float amount)
    {
        shake = Mathf.Max(shake, amount);
    }

    private void UpdateSweep()
    {
        sweepTime += Time.unscaledDeltaTime;
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sweepTime / sweepDuration));
        Vector3 settle = target.position - target.forward * 7f + Vector3.up * 3f;
        Vector3 position = Vector3.Lerp(sweepStart, settle, e);
        Vector3 look = Vector3.Lerp(sweepLook, target.position + Vector3.up * 1.5f, e);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation((look - position).normalized);
        if (sweepTime >= sweepDuration)
        {
            sweeping = false;
            smoothPosition = position; // hand off smoothly to the follow camera
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (sweeping)
        {
            UpdateSweep();
            return;
        }

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            // Combat gestures select attack/block direction without dragging
            // the camera away from the opponent.
            bool directionalGesture = player == null || !player.IsRanged;
            float lookScale = directionalGesture && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed) ? 0f : 1f;
            float sensitivity = SettingsService.Current != null ? SettingsService.Current.mouseSensitivity : 1f;
            yaw += delta.x * 0.11f * sensitivity * lookScale;
            pitch = Mathf.Clamp(pitch - delta.y * 0.085f * sensitivity * lookScale, -12f, 36f);
        }

        Vector3 targetDelta = target.position - previousTargetPosition;
        targetDelta.y = 0f;
        float speed = targetDelta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        previousTargetPosition = target.position;
        movementAmount = Mathf.Lerp(movementAmount, Mathf.Clamp01(speed / 4.5f), 7f * Time.deltaTime);
        stride += movementAmount * Time.deltaTime * (speed > 6f ? 12f : 9f);

        bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && speed > 1f;
        bool blocking = player != null && player.CanBlock && Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool rangedAim = player != null && player.IsRangedAiming;
        float distance = rangedAim ? 5.6f : sprinting ? 7.4f : blocking ? 6.2f : 7.05f;
        float shoulder = rangedAim ? -0.52f : blocking ? -0.42f : -0.32f;

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = orbit * Vector3.forward;
        Vector3 right = orbit * Vector3.right;
        Vector3 focus = target.position + Vector3.up * 1.52f + forward * 0.35f;

        float bob = Mathf.Sin(stride * 2f) * 0.035f * movementAmount;
        float sway = Mathf.Sin(stride) * 0.045f * movementAmount;
        Vector3 wanted = focus - forward * distance + right * (shoulder + sway) + Vector3.up * bob;
        Vector3 castStart = focus + right * 0.25f;
        Vector3 castDirection = wanted - castStart;
        if (Physics.SphereCast(castStart, 0.24f, castDirection.normalized, out RaycastHit hit, castDirection.magnitude,
            cameraCollisionMask, QueryTriggerInteraction.Ignore))
            wanted = hit.point + hit.normal * 0.28f;

        smoothPosition = Vector3.Lerp(smoothPosition, wanted, 18f * Time.deltaTime);
        shake = Mathf.MoveTowards(shake, 0f, 3.8f * Time.unscaledDeltaTime);
        float shakeScale = SettingsService.Current != null ? SettingsService.Current.cameraShake : 1f;
        Vector3 shakeOffset = Random.insideUnitSphere * shake * shakeScale;
        transform.position = smoothPosition + shakeOffset;

        Vector3 lookTarget = focus + forward * 4f + Vector3.up * (blocking ? 0.12f : 0.05f);
        Quaternion lookRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = lookRotation * Quaternion.Euler(shakeOffset.y * 2.2f, shakeOffset.x * 2.2f, 0f);

        if (attachedCamera != null)
        {
            float targetFov = rangedAim ? 50f : sprinting ? 63f : blocking ? 55f : 57f;
            attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFov, 6f * Time.deltaTime);
        }
    }
}
