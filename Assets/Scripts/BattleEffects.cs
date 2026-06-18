using System.Collections.Generic;
using UnityEngine;

public sealed class BattleEffects : MonoBehaviour
{
    private const int SpatialVoices = 16;
    private const float AmbienceBaseVolume = 0.11f;
    private const float DrumBaseVolume = 0.12f;

    private readonly List<ParticleSystem> particlePool = new();
    private readonly List<AudioSource> spatialPool = new();
    private PresentationCatalog catalog;
    private int particleCursor;
    private int spatialCursor;
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

    private void Awake()
    {
        catalog = PresentationCatalog.Load();
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.spatialBlend = 0f;
        uiSource.volume = 0.42f;

        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.clip = RuntimeAssets.Audio("Courtyard Wind", () => CreateAmbience(4f));
        ambienceSource.loop = true;
        ambienceSource.volume = AmbienceBaseVolume * MusicVolume;
        ambienceSource.Play();

        drumSource = gameObject.AddComponent<AudioSource>();
        drumSource.clip = RuntimeAssets.Audio("Distant War Drums", CreateDrumLoop);
        drumSource.loop = true;
        drumSource.volume = DrumBaseVolume * MusicVolume;
        drumSource.Play();

        hitClip = RandomClip(catalog != null ? catalog.impacts : null) ?? Tone("Hit", 115f, 0.12f, true);
        blockClip = RandomClip(catalog != null ? catalog.blocks : null) ?? Tone("Block", 620f, 0.13f, true);
        // Signature cues prefer curated CC0 clips from Resources, falling back to
        // the synthesized tone when a clip is absent.
        perfectBlockClip = LoadClip("Audio/swordClash1") ?? Tone("Perfect Block", 920f, 0.2f, true);
        counterClip = LoadClip("Audio/swordClash2") ?? Tone("Counter Strike", 360f, 0.18f, false);
        swingClip = RandomClip(catalog != null ? catalog.swings : null) ?? Tone("Swing", 240f, 0.11f, false);
        heavySwingClip = LoadClip("Audio/cloth4") ?? Tone("Heavy Swing", 155f, 0.18f, false);
        bowReleaseClip = RuntimeAssets.Audio("Bow Release", CreateBowRelease);
        arrowImpactClip = RandomClip(catalog != null ? catalog.arrows : null)
            ?? RandomClip(catalog != null ? catalog.impacts : null) ?? Tone("Arrow Impact", 430f, 0.1f, true);
        whiffClip = LoadClip("Audio/cloth1") ?? Tone("Whiff", 150f, 0.16f, true);
        footstepClip = RandomClip(catalog != null ? catalog.footsteps : null) ?? Tone("Footstep", 72f, 0.09f, true);
        victoryClip = RuntimeAssets.Audio("Victory Fanfare", CreateVictoryFanfare);
        for (int i = 0; i < 8; i++)
            particlePool.Add(CreateParticleEmitter(i));
        for (int i = 0; i < SpatialVoices; i++)
            spatialPool.Add(CreateSpatialVoice(i));
    }

    // The looping ambience and drum bed are the closest thing to music, so the
    // music-volume setting scales them live (it can change from the pause menu).
    private void Update()
    {
        float music = MusicVolume;
        if (ambienceSource != null)
            ambienceSource.volume = AmbienceBaseVolume * music;
        if (drumSource != null)
            drumSource.volume = DrumBaseVolume * music;
    }

    private static float MusicVolume => SettingsService.Current != null ? SettingsService.Current.musicVolume : 1f;
    private static float EffectsVolume => SettingsService.Current != null ? SettingsService.Current.effectsVolume : 1f;

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

    // A heavier blood burst punctuating a lethal blow; the killing hit's own
    // impact sound already plays through PlayImpact.
    public void PlayKill(Vector3 position)
    {
        SpawnSparks(position + Vector3.up * 1.1f, new Color(0.5f, 0.02f, 0.01f), 20);
    }

    private static AudioClip LoadClip(string resourcePath) => Resources.Load<AudioClip>(resourcePath);

    private void PlaySpatial(AudioClip clip, Vector3 position, float volume, float pitch, float maxDistance)
    {
        if (clip == null || spatialPool.Count == 0)
            return;
        AudioSource source = spatialPool[spatialCursor++ % spatialPool.Count];
        source.Stop();
        source.transform.position = position;
        source.clip = clip;
        source.volume = volume * EffectsVolume;
        source.pitch = pitch;
        source.maxDistance = maxDistance;
        source.Play();
    }

    private AudioSource CreateSpatialVoice(int index)
    {
        GameObject go = new($"Spatial Voice {index}");
        go.transform.SetParent(transform);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.2f;
        return source;
    }

    private void SpawnSparks(Vector3 position, Color color, int count)
    {
        ParticleSystem particles = particlePool[particleCursor++ % particlePool.Count];
        particles.transform.position = position;
        ParticleSystem.MainModule main = particles.main;
        main.startColor = color;
        particles.Emit(count);
    }

    private ParticleSystem CreateParticleEmitter(int index)
    {
        GameObject go = new($"Impact Particles {index}");
        go.transform.SetParent(transform);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
        main.gravityModifier = 0.5f;
        main.maxParticles = 48;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.16f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = RuntimeAssets.Material(Color.white, true);
        return particles;
    }

    private static AudioClip RandomClip(AudioClip[] clips)
        => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

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

    // A rising C-major arpeggio (C5-E5-G5-C6) with octave harmonics and ringing
    // decay — a triumphant sting instead of a single flat tone.
    private static AudioClip CreateVictoryFanfare()
    {
        const int sampleRate = 22050;
        const float duration = 0.85f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int n = 0; n < notes.Length; n++)
        {
            int startSample = Mathf.RoundToInt(n * 0.12f * sampleRate);
            for (int i = startSample; i < length; i++)
            {
                float t = (i - startSample) / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 4.5f);
                float wave = Mathf.Sin(t * notes[n] * Mathf.PI * 2f)
                    + 0.3f * Mathf.Sin(t * notes[n] * 2f * Mathf.PI * 2f);
                samples[i] += wave * envelope * 0.18f;
            }
        }
        for (int i = 0; i < length; i++)
            samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
        AudioClip clip = AudioClip.Create("Victory Fanfare", length, 1, sampleRate, false);
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
