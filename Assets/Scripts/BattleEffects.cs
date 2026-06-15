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
    private AudioClip perfectBlockClip;
    private AudioClip counterClip;
    private AudioClip swingClip;
    private AudioClip heavySwingClip;
    private AudioClip bowReleaseClip;
    private AudioClip arrowImpactClip;
    private AudioClip whiffClip;
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
        ambienceSource.clip = RuntimeAssets.Audio("Courtyard Wind", () => CreateAmbience(4f));
        ambienceSource.loop = true;
        ambienceSource.volume = 0.11f;
        ambienceSource.Play();

        drumSource = gameObject.AddComponent<AudioSource>();
        drumSource.clip = RuntimeAssets.Audio("Distant War Drums", CreateDrumLoop);
        drumSource.loop = true;
        drumSource.volume = 0.12f;
        drumSource.Play();

        hitClip = Tone("Hit", 115f, 0.12f, true);
        blockClip = Tone("Block", 620f, 0.13f, true);
        perfectBlockClip = Tone("Perfect Block", 920f, 0.2f, true);
        counterClip = Tone("Counter Strike", 360f, 0.18f, false);
        swingClip = Tone("Swing", 240f, 0.11f, false);
        heavySwingClip = Tone("Heavy Swing", 155f, 0.18f, false);
        bowReleaseClip = RuntimeAssets.Audio("Bow Release", CreateBowRelease);
        arrowImpactClip = Tone("Arrow Impact", 430f, 0.1f, true);
        whiffClip = Tone("Whiff", 150f, 0.16f, true);
        footstepClip = Tone("Footstep", 72f, 0.09f, true);
        victoryClip = Tone("Victory", 440f, 0.55f, false);
    }

    public void PlayAttack(Vector3 position, bool player, WeaponType weapon)
    {
        AudioClip clip = weapon == WeaponType.Bow ? bowReleaseClip
            : weapon == WeaponType.TwoHandedSword ? heavySwingClip : swingClip;
        float volume = weapon == WeaponType.Bow ? (player ? 0.72f : 0.42f)
            : weapon == WeaponType.TwoHandedSword ? (player ? 0.74f : 0.42f) : (player ? 0.6f : 0.32f);
        float pitch = weapon == WeaponType.Bow ? Random.Range(0.96f, 1.04f)
            : weapon == WeaponType.TwoHandedSword ? Random.Range(0.86f, 0.94f)
            : player ? 1.08f : Random.Range(0.82f, 0.98f);
        PlaySpatial(clip, position + Vector3.up, volume, pitch, weapon == WeaponType.Bow ? 22f : 14f);
    }

    public void PlayFootstep(Vector3 position, bool player)
    {
        PlaySpatial(footstepClip, position, player ? 0.18f : 0.07f, Random.Range(0.82f, 1.12f), player ? 7f : 4f);
    }

    public void PlayWhiff(Vector3 position, bool player)
    {
        PlaySpatial(whiffClip, position + Vector3.up, player ? 0.38f : 0.12f, player ? 0.9f : Random.Range(0.75f, 0.9f), 9f);
    }

    public void PlayImpact(Vector3 position, bool blocked, bool perfectBlock, bool counterStrike)
    {
        AudioClip clip = perfectBlock ? perfectBlockClip : counterStrike ? counterClip : blocked ? blockClip : hitClip;
        PlaySpatial(clip, position + Vector3.up * 1.2f, perfectBlock ? 1f : blocked ? 0.9f : 0.82f,
            perfectBlock ? 1f : Random.Range(0.92f, 1.08f), 18f);
        Color color = perfectBlock ? new Color(0.7f, 0.95f, 1f)
            : counterStrike ? new Color(1f, 0.82f, 0.18f)
            : blocked ? new Color(1f, 0.78f, 0.25f) : new Color(0.85f, 0.12f, 0.05f);
        SpawnSparks(position + Vector3.up * 1.2f, color, perfectBlock ? 16 : counterStrike ? 12 : blocked ? 9 : 7);
    }

    public void PlayArrowImpact(Vector3 position, bool fighterHit)
    {
        PlaySpatial(arrowImpactClip, position, fighterHit ? 0.62f : 0.42f,
            fighterHit ? Random.Range(0.86f, 0.96f) : Random.Range(1.02f, 1.16f), 18f);
        SpawnSparks(position, fighterHit ? new Color(0.72f, 0.12f, 0.06f) : new Color(0.7f, 0.52f, 0.22f),
            fighterHit ? 6 : 9);
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
            sparkObject.GetComponent<Renderer>().sharedMaterial = RuntimeAssets.Material(color, true);
            sparks.Add(new Spark
            {
                GameObject = sparkObject,
                Velocity = Random.onUnitSphere * Random.Range(1.5f, 3.5f) + Vector3.up * 1.2f,
                Life = 0.42f,
                MaxLife = 0.42f
            });
        }
    }

    private static AudioClip CreateAmbience(float duration)
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
        AudioClip clip = AudioClip.Create("Courtyard Wind", length, 1, sampleRate, false);
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

    private static AudioClip CreateBowRelease()
    {
        const int sampleRate = 22050;
        const float duration = 0.18f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 24f);
            float stringSnap = Mathf.Sin(t * 820f * Mathf.PI * 2f) * envelope;
            float body = Mathf.Sin(t * 118f * Mathf.PI * 2f) * Mathf.Exp(-t * 14f);
            samples[i] = stringSnap * 0.32f + body * 0.24f;
        }
        AudioClip clip = AudioClip.Create("Bow Release", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip Tone(string clipName, float frequency, float duration, bool noisy)
    {
        return RuntimeAssets.Audio(clipName, () => CreateTone(clipName, frequency, duration, noisy));
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
