using UnityEngine;

// Synthesized musical beds, used as the fallback when no curated music clip is
// wired into the PresentationCatalog. Both themes are built so the buffer loops
// seamlessly: sustained drones are snapped to a whole number of cycles over the
// clip length, and every plucked note decays to silence before the loop point,
// so only the seamless drone ever crosses the seam.
public static class ProceduralMusic
{
    private const int SampleRate = 22050;

    // A slow, modal battle bed: a low drone fifth in D Dorian under a sparse
    // plucked melody. Designed to sit quietly beneath the ambience and war-drum beds.
    public static AudioClip BattleTheme()
    {
        const float duration = 12f;
        int length = Mathf.RoundToInt(SampleRate * duration);
        float[] samples = new float[length];
        float Snap(float freq) => Mathf.Round(freq * duration) / duration;

        float droneRoot = Snap(73.42f);   // D2
        float droneFifth = Snap(110.0f);  // A2
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float swell = 0.82f + 0.18f * Mathf.Sin(t * Mathf.PI * 2f / duration);
            float drone = Mathf.Sin(t * droneRoot * Mathf.PI * 2f) * 0.5f
                        + Mathf.Sin(t * droneFifth * Mathf.PI * 2f) * 0.3f;
            samples[i] = drone * swell * 0.16f;
        }

        float[] scale = { 146.83f, 164.81f, 174.61f, 196.0f, 220.0f, 246.94f, 261.63f, 293.66f };
        int[] phrase = { 0, 2, 3, 4, 3, 2, 4, 5, 4, 2, 0, 3 };
        AddPluckPhrase(samples, length, scale, phrase, 0.4f, 0.9f, 0.8f, 0.14f, Snap);

        Normalize(samples);
        return Build("Battle Theme", samples, length);
    }

    // A calmer overworld bed: a warm open drone under a gentle wandering harp-like
    // arpeggio. No percussion — a travelling theme, not a fight.
    public static AudioClip OverworldTheme()
    {
        const float duration = 14f;
        int length = Mathf.RoundToInt(SampleRate * duration);
        float[] samples = new float[length];
        float Snap(float freq) => Mathf.Round(freq * duration) / duration;

        float droneRoot = Snap(98.0f);    // G2
        float droneFifth = Snap(146.83f); // D3
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float swell = 0.8f + 0.2f * Mathf.Sin(t * Mathf.PI * 2f / duration + 0.5f);
            float drone = Mathf.Sin(t * droneRoot * Mathf.PI * 2f) * 0.42f
                        + Mathf.Sin(t * droneFifth * Mathf.PI * 2f) * 0.26f;
            samples[i] = drone * swell * 0.12f;
        }

        // G major pentatonic across two octaves for an untroubled wander.
        float[] scale = { 196.0f, 220.0f, 246.94f, 293.66f, 329.63f, 392.0f, 440.0f, 493.88f };
        int[] phrase = { 0, 2, 4, 5, 4, 2, 3, 5, 6, 5, 3, 1, 0, 2 };
        AddPluckPhrase(samples, length, scale, phrase, 0.6f, 0.95f, 0.9f, 0.1f, Snap);

        Normalize(samples);
        return Build("Overworld Theme", samples, length);
    }

    // Lays a sequence of soft plucked notes over the buffer. Each note's exponential
    // decay completes before the loop point, so only the (seamless) drone crosses it.
    private static void AddPluckPhrase(float[] samples, int length, float[] scale, int[] phrase,
        float startSeconds, float stepSeconds, float noteSeconds, float amplitude, System.Func<float, float> snap)
    {
        int noteLen = Mathf.RoundToInt(noteSeconds * SampleRate);
        for (int n = 0; n < phrase.Length; n++)
        {
            float freq = snap(scale[phrase[n]]);
            int start = Mathf.RoundToInt((startSeconds + n * stepSeconds) * SampleRate);
            for (int i = 0; i < noteLen && start + i < length; i++)
            {
                float u = i / (float)noteLen;
                float env = Mathf.Exp(-u * 3.5f) * Mathf.Sin(Mathf.Min(1f, u * 12f) * Mathf.PI * 0.5f);
                float wave = Mathf.Sin((start + i) / (float)SampleRate * freq * Mathf.PI * 2f)
                           + 0.3f * Mathf.Sin((start + i) / (float)SampleRate * freq * 2f * Mathf.PI * 2f);
                samples[start + i] += wave * env * amplitude;
            }
        }
    }

    private static void Normalize(float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
    }

    private static AudioClip Build(string clipName, float[] samples, int length)
    {
        AudioClip clip = AudioClip.Create(clipName, length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
