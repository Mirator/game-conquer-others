using UnityEngine;
using UnityEngine.InputSystem;

// A lightweight on-screen performance overlay for the perf run. Toggle with F3.
// Shows smoothed FPS / frame time, a decaying worst-frame, live fighter count, and the
// active quality tier — enough to read the budget without the editor profiler. Costs
// nothing while hidden.
public sealed class PerformanceHud : MonoBehaviour
{
    private static bool visible;

    private BattleManager battle;
    private float smoothedMs;
    private float worstMs;
    private string text;
    private GUIStyle style;

    public void Configure(BattleManager manager) => battle = manager;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            visible = !visible;
        if (!visible)
            return;

        float ms = Time.unscaledDeltaTime * 1000f;
        smoothedMs = smoothedMs <= 0f ? ms : Mathf.Lerp(smoothedMs, ms, 0.1f);
        worstMs = Mathf.Max(worstMs * 0.97f, ms); // decays so a single spike fades
        float fps = smoothedMs > 0f ? 1000f / smoothedMs : 0f;
        // Format once per frame here, not in OnGUI (which runs for both layout + repaint).
        text = $"FPS {fps:0}   {smoothedMs:0.0} ms   (worst {worstMs:0.0} ms)\nQuality: {GraphicsQuality.Tier}";
        if (battle != null)
            text += $"\nFighters alive: {battle.AlliesAlive + battle.EnemiesAlive}";
    }

    private void OnGUI()
    {
        if (!visible || text == null || Event.current.type != EventType.Repaint)
            return;
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
        GUI.Box(new Rect(8f, 8f, 320f, 78f), GUIContent.none);
        GUI.Label(new Rect(16f, 14f, 320f, 78f), text, style);
    }
}
