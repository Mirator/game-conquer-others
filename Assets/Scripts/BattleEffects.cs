using System.Collections.Generic;
using UnityEngine;

public sealed class BattleEffects : MonoBehaviour
{
    private readonly List<Spark> sparks = new();
    private AudioSource uiSource;
    private AudioSource ambienceSource;
    private AudioSource drumSource;
    private AudioClip hitClip;
    private AudioClip blockClip;
    private AudioClip swingClip;
    private AudioClip footstepClip;
    private AudioClip victoryClip;

    private sealed class Spark
    {
        public GameObject GameObject;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
    }

    private void Awake()
    {
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.spatialBlend = 0f;
        uiSource.volume = 0.42f;

        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.clip = CreateAmbience("Courtyard Wind", 4f);
        ambienceSource.loop = true;
        ambienceSource.volume = 0.11f;
        ambienceSource.Play();

        drumSource = gameObject.AddComponent<AudioSource>();
        drumSource.clip = CreateDrumLoop();
        drumSource.loop = true;
        drumSource.volume = 0.12f;
        drumSource.Play();

        hitClip = CreateTone("Hit", 115f, 0.12f, true);
        blockClip = CreateTone("Block", 620f, 0.13f, true);
        swingClip = CreateTone("Swing", 240f, 0.11f, false);
        footstepClip = CreateTone("Footstep", 72f, 0.09f, true);
        victoryClip = CreateTone("Victory", 440f, 0.55f, false);
    }

    public void PlaySwing(Vector3 position, bool player)
    {
        PlaySpatial(swingClip, position + Vector3.up, player ? 0.6f : 0.32f, player ? 1.08f : Random.Range(0.82f, 0.98f), 14f);
    }

    public void PlayFootstep(Vector3 position, bool player)
    {
        PlaySpatial(footstepClip, position, player ? 0.18f : 0.07f, Random.Range(0.82f, 1.12f), player ? 7f : 4f);
    }

    public void PlayImpact(Vector3 position, bool blocked)
    {
        PlaySpatial(blocked ? blockClip : hitClip, position + Vector3.up * 1.2f, blocked ? 0.9f : 0.82f, Random.Range(0.92f, 1.08f), 18f);
        SpawnSparks(position + Vector3.up * 1.2f, blocked ? new Color(1f, 0.78f, 0.25f) : new Color(0.85f, 0.12f, 0.05f), blocked ? 9 : 7);
    }

    public void PlayVictory()
    {
        uiSource.pitch = 1f;
        uiSource.PlayOneShot(victoryClip, 0.7f);
    }

    private void Update()
    {
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = sparks[i];
            spark.Life -= Time.unscaledDeltaTime;
            if (spark.Life <= 0f)
            {
                Destroy(spark.GameObject);
                sparks.RemoveAt(i);
                continue;
            }
            spark.Velocity += Physics.gravity * 0.35f * Time.unscaledDeltaTime;
            spark.GameObject.transform.position += spark.Velocity * Time.unscaledDeltaTime;
            spark.GameObject.transform.localScale = Vector3.one * (spark.Life / spark.MaxLife * 0.11f);
        }
    }

    private void PlaySpatial(AudioClip clip, Vector3 position, float volume, float pitch, float maxDistance)
    {
        GameObject soundObject = new GameObject($"Sound - {clip.name}");
        soundObject.transform.position = position;
        soundObject.transform.SetParent(transform);
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.2f;
        source.maxDistance = maxDistance;
        source.Play();
        Destroy(soundObject, clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.15f);
    }

    private void SpawnSparks(Vector3 position, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject sparkObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sparkObject.name = "Impact Spark";
            sparkObject.transform.SetParent(transform);
            sparkObject.transform.position = position + Random.insideUnitSphere * 0.18f;
            sparkObject.transform.localScale = Vector3.one * 0.1f;
            Destroy(sparkObject.GetComponent<Collider>());
            sparkObject.GetComponent<Renderer>().material = BattleBootstrap.CreateMaterial(color, true);
            sparks.Add(new Spark
            {
                GameObject = sparkObject,
                Velocity = Random.onUnitSphere * Random.Range(1.5f, 3.5f) + Vector3.up * 1.2f,
                Life = 0.42f,
                MaxLife = 0.42f
            });
        }
    }

    private static AudioClip CreateAmbience(string clipName, float duration)
    {
        const int sampleRate = 22050;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        float filtered = 0f;
        for (int i = 0; i < length; i++)
        {
            filtered = Mathf.Lerp(filtered, Random.Range(-1f, 1f), 0.015f);
            float gust = 0.35f + 0.2f * Mathf.Sin(i / (float)sampleRate * Mathf.PI * 0.65f);
            samples[i] = filtered * gust * 0.16f;
        }
        AudioClip clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateDrumLoop()
    {
        const int sampleRate = 22050;
        const float duration = 4f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float beatPhase = t % 1.33f;
            float envelope = Mathf.Exp(-beatPhase * 11f);
            samples[i] = Mathf.Sin(t * 66f * Mathf.PI * 2f) * envelope * 0.22f;
        }
        AudioClip clip = AudioClip.Create("Distant War Drums", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateTone(string clipName, float frequency, float duration, bool noisy)
    {
        const int sampleRate = 22050;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - i / (float)length;
            float wave = Mathf.Sin(t * frequency * Mathf.PI * 2f);
            if (noisy)
                wave = wave * 0.45f + Random.Range(-0.55f, 0.55f);
            samples[i] = wave * envelope * envelope * 0.45f;
        }
        AudioClip clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
