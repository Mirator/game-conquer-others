using System.Collections.Generic;
using UnityEngine;

// Drives wind sway for all scatter (trees, grass, ferns) from a single Update loop
// rather than one MonoBehaviour per tuft — hundreds of per-component Update callbacks
// collapse into one list walk. Each entry leans with a cheap sine, phase-offset by
// world position so a field doesn't sway in unison. The bootstrap only adds entries
// when sway is wanted (skipped on the low tier and under reduced motion), so this
// component does nothing when there is nothing to sway.
public sealed class WindSway : MonoBehaviour
{
    private struct Swayer
    {
        public Transform transform;
        public Quaternion baseRotation;
        public float amplitude;
        public float frequency;
        public float phase;
    }

    private readonly List<Swayer> swayers = new();

    public void Add(Transform target, float amplitudeDegrees, float frequency)
    {
        Vector3 p = target.position;
        swayers.Add(new Swayer
        {
            transform = target,
            baseRotation = target.localRotation,
            amplitude = amplitudeDegrees,
            frequency = frequency,
            phase = p.x * 0.7f + p.z * 0.9f
        });
    }

    private void Update()
    {
        float time = Time.time;
        for (int i = 0; i < swayers.Count; i++)
        {
            Swayer s = swayers[i];
            if (s.transform == null)
                continue;
            float t = time * s.frequency + s.phase;
            s.transform.localRotation = s.baseRotation
                * Quaternion.Euler(Mathf.Sin(t) * s.amplitude, 0f, Mathf.Cos(t * 0.7f) * s.amplitude * 0.5f);
        }
    }
}
