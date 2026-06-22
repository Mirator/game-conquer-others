// A pool of billboarded world-space combat readouts (damage numbers and short
// cues like "PARRY!") that rise and fade above a struck fighter. Purely
// presentational; the simulation never depends on it, and it is gated behind
// the ShowDamageNumbers setting at the call site in BattleManager.
using System.Collections.Generic;
using UnityEngine;

public sealed class FloatingCombatText : MonoBehaviour
{
    private const int PoolSize = 24;
    private const float Lifetime = 0.95f;
    private const float RiseSpeed = 1.7f;

    private readonly List<Entry> pool = new();
    private int cursor;
    private Transform view;

    private sealed class Entry
    {
        public TextMesh mesh;
        public Transform transform;
        public Color color;
        public float timer;
    }

    private void Awake()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < PoolSize; i++)
            pool.Add(CreateEntry(i, font));
    }

    private Entry CreateEntry(int index, Font font)
    {
        GameObject go = new($"Combat Text {index}");
        go.transform.SetParent(transform);
        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.font = font;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 64;
        mesh.characterSize = 0.085f;
        mesh.fontStyle = FontStyle.Bold;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            // The built-in font ships its own material; reusing it keeps the glyphs
            // rendering without authoring a material asset.
            if (font != null)
                renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        go.SetActive(false);
        return new Entry { mesh = mesh, transform = go.transform };
    }

    // Spawns a readout above a world position. scale lets a counter or lethal
    // blow read bigger than a glancing hit.
    public void Spawn(Vector3 worldPosition, string content, Color color, float scale = 1f)
    {
        Entry entry = pool[cursor];
        cursor = (cursor + 1) % pool.Count;
        entry.transform.position = worldPosition + Vector3.up * 1.95f
            + new Vector3(Random.Range(-0.22f, 0.22f), 0f, 0f);
        entry.transform.localScale = Vector3.one * scale;
        entry.mesh.text = content;
        entry.color = color;
        entry.timer = Lifetime;
        entry.transform.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (view == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                view = cam.transform;
        }

        float dt = Time.deltaTime;
        // Reduced-motion keeps the readout (and its fade) but holds it still.
        float rise = SettingsService.Current is { reduceMotion: true } ? 0f : RiseSpeed;
        foreach (Entry entry in pool)
        {
            if (entry.timer <= 0f)
                continue;
            entry.timer -= dt;
            if (entry.timer <= 0f)
            {
                entry.transform.gameObject.SetActive(false);
                continue;
            }

            float life = entry.timer / Lifetime; // 1 -> 0 over its lifetime
            entry.transform.position += Vector3.up * rise * dt;
            if (view != null)
                entry.transform.rotation = Quaternion.LookRotation(entry.transform.position - view.position);
            // Hold full opacity for the first third, then fade out.
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(life * 1.5f));
            entry.mesh.color = new Color(entry.color.r, entry.color.g, entry.color.b, alpha);
        }
    }
}
