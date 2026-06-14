using UnityEngine;

public static class CombatGesture
{
    public const float Window = 0.08f;
    public const float Threshold = 10f;
    private const float AxisHysteresis = 1.25f;

    public static bool TryResolve(Vector2 accumulatedDelta, out CombatDirection direction)
    {
        direction = CombatDirection.Right;
        if (accumulatedDelta.sqrMagnitude < Threshold * Threshold)
            return false;

        float horizontal = Mathf.Abs(accumulatedDelta.x);
        float vertical = Mathf.Abs(accumulatedDelta.y);
        if (horizontal >= vertical * AxisHysteresis)
        {
            direction = accumulatedDelta.x < 0f ? CombatDirection.Left : CombatDirection.Right;
            return true;
        }
        if (vertical >= horizontal * AxisHysteresis)
        {
            direction = accumulatedDelta.y > 0f ? CombatDirection.Up : CombatDirection.Thrust;
            return true;
        }
        return false;
    }
}
