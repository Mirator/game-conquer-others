using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ThirdPersonCamera : MonoBehaviour
{
    public Vector3 FlatForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    public Vector3 FlatRight => Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

    private Transform target;
    private Camera attachedCamera;
    private Vector3 previousTargetPosition;
    private Vector3 smoothPosition;
    private float yaw;
    private float pitch = 12f;
    private float shake;
    private float movementAmount;
    private float stride;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        yaw = followTarget.eulerAngles.y;
        attachedCamera = GetComponent<Camera>();
        previousTargetPosition = followTarget.position;
        smoothPosition = transform.position;
    }

    public void AddShake(float amount)
    {
        shake = Mathf.Max(shake, amount);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            // Combat gestures select attack/block direction without dragging
            // the camera away from the opponent.
            float lookScale = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed ? 0f : 1f;
            yaw += delta.x * 0.11f * lookScale;
            pitch = Mathf.Clamp(pitch - delta.y * 0.085f * lookScale, -12f, 36f);
        }

        Vector3 targetDelta = target.position - previousTargetPosition;
        targetDelta.y = 0f;
        float speed = targetDelta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        previousTargetPosition = target.position;
        movementAmount = Mathf.Lerp(movementAmount, Mathf.Clamp01(speed / 4.5f), 7f * Time.deltaTime);
        stride += movementAmount * Time.deltaTime * (speed > 6f ? 12f : 9f);

        bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && speed > 1f;
        bool blocking = Mouse.current != null && Mouse.current.rightButton.isPressed;
        float distance = sprinting ? 6.6f : blocking ? 5.45f : 5.95f;
        float shoulder = blocking ? -0.5f : -0.38f;

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = orbit * Vector3.forward;
        Vector3 right = orbit * Vector3.right;
        Vector3 focus = target.position + Vector3.up * 1.43f + forward * 0.35f;

        float bob = Mathf.Sin(stride * 2f) * 0.035f * movementAmount;
        float sway = Mathf.Sin(stride) * 0.045f * movementAmount;
        Vector3 wanted = focus - forward * distance + right * (shoulder + sway) + Vector3.up * bob;
        Vector3 castStart = focus + right * 0.25f;
        Vector3 castDirection = wanted - castStart;
        if (Physics.SphereCast(castStart, 0.24f, castDirection.normalized, out RaycastHit hit, castDirection.magnitude, ~0, QueryTriggerInteraction.Ignore))
            wanted = hit.point + hit.normal * 0.28f;

        smoothPosition = Vector3.Lerp(smoothPosition, wanted, 18f * Time.deltaTime);
        shake = Mathf.MoveTowards(shake, 0f, 3.8f * Time.unscaledDeltaTime);
        Vector3 shakeOffset = Random.insideUnitSphere * shake;
        transform.position = smoothPosition + shakeOffset;

        Vector3 lookTarget = focus + forward * 4f + Vector3.up * (blocking ? 0.12f : 0.05f);
        Quaternion lookRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = lookRotation * Quaternion.Euler(shakeOffset.y * 2.2f, shakeOffset.x * 2.2f, 0f);

        if (attachedCamera != null)
        {
            float targetFov = sprinting ? 64f : blocking ? 56f : 59f;
            attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFov, 6f * Time.deltaTime);
        }
    }
}
