using UnityEngine;
using UnityEngine.InputSystem;

// Campaign-map camera framing and input, extracted from CampaignMapController:
// mouse-wheel zoom along the view axis, right-drag pan clamped to the table, and
// Home to recentre on the warband. The framing math (focus position, table clamp)
// is pure and static so it can be unit tested; the per-frame input reads stay here.
public sealed class CampaignMapCamera
{
    private const float MinCameraHeight = 14f;
    private const float MaxCameraHeight = 78f;
    private const float ZoomStep = 6f;
    private const float PanSpeed = 0.05f;
    // Zoomed in on the warband at campaign start, not the whole map.
    public const float StartCameraHeight = 22f;
    private const float MapCameraLimitX = 40f;
    private const float MapCameraLimitZMin = -31f;
    private const float MapCameraLimitZMax = 35f;

    private readonly Camera cam;

    public CampaignMapCamera(Camera cam) => this.cam = cam;

    // Centres the fixed-pitch camera over a map position at a given height by sliding
    // back along its view axis, so the point sits under the screen centre.
    public void Focus(Vector2 mapPosition, float height)
        => cam.transform.position = FocusPosition(CampaignMapView.WorldOf(mapPosition), cam.transform.forward, height);

    // Per-frame controls: Home recentres on partyPosition, wheel zooms, right-drag pans.
    public void HandleControls(Vector2 partyPosition)
    {
        if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            Focus(partyPosition, StartCameraHeight);
        if (Mouse.current == null)
            return;
        Transform t = cam.transform;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Vector3 desired = t.position + t.forward * Mathf.Sign(scroll) * ZoomStep;
            if (desired.y >= MinCameraHeight && desired.y <= MaxCameraHeight)
                t.position = desired;
        }

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            Vector3 planarForward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            float scale = PanSpeed * (t.position.y / 30f);
            Vector3 move = (-t.right * delta.x - planarForward * delta.y) * scale;
            move.y = 0f;
            t.position += move;
            t.position = ClampToTable(t.position, t.forward);
        }
    }

    // Camera position that puts `ground` (raised to `height`) under the screen centre
    // for a downward-looking camera (forward.y is negative). Pure for testing.
    public static Vector3 FocusPosition(Vector3 ground, Vector3 forward, float height)
    {
        float back = (height - ground.y) / -forward.y;
        return ground - forward * back;
    }

    // Slides the camera so the point under the screen centre stays on the table,
    // preventing right-drag into empty space. Pure for testing.
    public static Vector3 ClampToTable(Vector3 position, Vector3 forward)
    {
        float distance = position.y / Mathf.Max(-forward.y, 0.01f);
        Vector3 center = position + forward * distance;
        center.x = Mathf.Clamp(center.x, -MapCameraLimitX, MapCameraLimitX);
        center.z = Mathf.Clamp(center.z, MapCameraLimitZMin, MapCameraLimitZMax);
        return center - forward * distance;
    }
}
