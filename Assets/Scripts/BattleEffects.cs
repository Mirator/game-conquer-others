using System.Collections.Generic;
using UnityEngine;

public sealed class BattleEffects : MonoBehaviour
{
    private const int SpatialVoices = 16;
    private const float AmbienceBaseVolume = 0.11f;
    private const float DrumBaseVolume = 0.12f;
    private const float MusicBaseVolume = 0.13f;

    private readonly List<ParticleSystem> particlePool = new();
    private readonly List<ParticleSystem> dustPool = new();
    private readonly List<AudioSource> spatialPool = new();
    private readonly List<(AudioSource source, float baseVolume)> ambientEmitters = new();
    private PresentationCatalog catalog;
    private int particleCursor;
    private int dustCursor;
    private int spatialCursor;
    private AudioSource uiSource;
    private AudioSource ambienceSource;
    private AudioSource drumSource;
    private AudioSource musicSource;
    private AudioSource weatherSource;
    private float weatherBaseVolume;
    // Fades the martial beds (music + drums) to silence when the fight ends so the
    // victory fanfare rings out; the environmental wind bed keeps playing.
    private float musicDuck = 1f;
    private float musicDuckTarget = 1f;
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
    private AudioClip guardBreakClip;

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
        // Initialize(arena) may later swap in a per-region ambient bed.

        drumSource = gameObject.AddComponent<AudioSource>();
        drumSource.clip = RuntimeAssets.Audio("Distant War Drums", CreateDrumLoop);
        drumSource.loop = true;
        drumSource.volume = DrumBaseVolume * MusicVolume;
        drumSource.Play();

        // A melodic battle bed on top of the drums. Starts on the synthesized theme;
        // Initialize(arena) swaps in a curated clip when the catalog provides one.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = RuntimeAssets.Audio("Battle Theme", ProceduralMusic.BattleTheme);
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = MusicBaseVolume * MusicVolume;
        musicSource.Play();

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
        guardBreakClip = RuntimeAssets.Audio("Guard Break", CreateGuardBreak);
        for (int i = 0; i < 8; i++)
            particlePool.Add(CreateParticleEmitter(i));
        for (int i = 0; i < 4; i++)
            dustPool.Add(CreateDustEmitter(i));
        for (int i = 0; i < SpatialVoices; i++)
            spatialPool.Add(CreateSpatialVoice(i));
    }

    // The music, drum, and ambience beds all track the music-volume setting live
    // (it can change from the pause menu). The martial beds (music + drums) also
    // fade out on victory via musicDuck; the environmental beds do not.
    private void Update()
    {
        musicDuck = Mathf.MoveTowards(musicDuck, musicDuckTarget, Time.unscaledDeltaTime / 1.2f);
        float music = MusicVolume;
        if (ambienceSource != null)
            ambienceSource.volume = AmbienceBaseVolume * music;
        if (drumSource != null)
            drumSource.volume = DrumBaseVolume * music * musicDuck;
        if (musicSource != null)
            musicSource.volume = MusicBaseVolume * music * musicDuck;
        foreach ((AudioSource source, float baseVolume) in ambientEmitters)
            if (source != null)
                source.volume = baseVolume * music;
        if (weatherSource != null)
            weatherSource.volume = weatherBaseVolume * music;
    }

    private static float MusicVolume => SettingsService.Current != null ? SettingsService.Current.musicVolume : 1f;
    private static float EffectsVolume => SettingsService.Current != null ? SettingsService.Current.effectsVolume : 1f;

    // Swaps in the arena's curated ambient and music beds if the theme provides them
    // (else the synthesized wind and battle theme from Awake stay). A per-arena music
    // clip wins over the catalog-wide battle track. Called once per battle build.
    public void Initialize(ArenaType arena)
    {
        ArenaThemeDefinition theme = catalog != null ? catalog.Theme(arena) : null;
        if (theme?.ambience != null && ambienceSource != null)
        {
            ambienceSource.clip = theme.ambience;
            ambienceSource.Play();
        }
        AudioClip music = theme != null && theme.music != null ? theme.music
            : catalog != null ? catalog.battleMusic : null;
        if (music != null && musicSource != null)
        {
            musicSource.clip = music;
            musicSource.Play();
        }
    }

    // A 2D rain hiss bed, played when the arena weather is rainy. Tracks music volume.
    public void PlayRainAmbience()
    {
        if (weatherSource == null)
            weatherSource = gameObject.AddComponent<AudioSource>();
        weatherSource.clip = RuntimeAssets.Audio("Rain Loop", CreateRainLoop);
        weatherSource.loop = true;
        weatherSource.spatialBlend = 0f;
        weatherBaseVolume = 0.16f;
        weatherSource.volume = weatherBaseVolume * MusicVolume;
        weatherSource.Play();
    }

    // Positional, looping environmental sound — placed by the bootstrap to give the
    // field spatial life. Volume tracks the music slider alongside the ambient bed.
    public void AddBirdsong(Vector3 position) => AddAmbientEmitter(position, "Birdsong", CreateBirdsong, 0.5f, 40f);
    public void AddMarshChorus(Vector3 position) => AddAmbientEmitter(position, "Marsh Chorus", CreateMarshChorus, 0.55f, 34f);
    public void AddWindGust(Vector3 position) => AddAmbientEmitter(position, "Wind Gust", CreateWindGust, 0.6f, 48f);

    private void AddAmbientEmitter(Vector3 position, string key, System.Func<AudioClip> factory, float baseVolume, float maxDistance)
    {
        GameObject go = new("Ambient Emitter");
        go.transform.SetParent(transform);
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = RuntimeAssets.Audio(key, factory);
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = maxDistance;
        source.volume = baseVolume * MusicVolume;
        if (source.clip != null)
            source.time = Random.Range(0f, source.clip.length); // desync identical emitters
        source.Play();
        ambientEmitters.Add((source, baseVolume));
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
        if (player)
            PlayDust(position, 2);
    }

    public void PlayWhiff(Vector3 position, bool player)
    {
        PlaySpatial(whiffClip, position + Vector3.up, player ? 0.38f : 0.12f, player ? 0.9f : Random.Range(0.75f, 0.9f), 9f);
    }

    // blowDirection points from attacker to victim; the spark burst flies that way
    // for a landed hit, and back off the guard (toward the attacker) for a block.
    public void PlayImpact(Vector3 position, bool blocked, bool perfectBlock, bool counterStrike, Vector3 blowDirection = default)
    {
        AudioClip clip = perfectBlock ? perfectBlockClip : counterStrike ? counterClip : blocked ? blockClip : hitClip;
        PlaySpatial(clip, position + Vector3.up * 1.2f, perfectBlock ? 1f : blocked ? 0.9f : 0.82f,
            perfectBlock ? 1f : Random.Range(0.92f, 1.08f), 18f);
        Color color = perfectBlock ? new Color(0.7f, 0.95f, 1f)
            : counterStrike ? new Color(1f, 0.82f, 0.18f)
            : blocked ? new Color(1f, 0.78f, 0.25f) : new Color(0.85f, 0.12f, 0.05f);
        Vector3 spray = blocked ? -blowDirection : blowDirection;
        SpawnSparks(position + Vector3.up * 1.2f, color, perfectBlock ? 18 : counterStrike ? 14 : blocked ? 10 : 10, spray);
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
        musicDuckTarget = 0f; // fade the martial beds so the fanfare rings out
        uiSource.pitch = 1f;
        uiSource.PlayOneShot(victoryClip, 0.7f);
        if (catalog != null && catalog.victoryMusic != null)
            uiSource.PlayOneShot(catalog.victoryMusic, 0.7f * MusicVolume);
    }

    // A heavier blood burst punctuating a lethal blow; the killing hit's own
    // impact sound already plays through PlayImpact. blowDirection sprays the blood
    // along the strike so the finisher reads directionally, distinct from the
    // ground splat decal the battle manager lays down.
    public void PlayKill(Vector3 position, Vector3 blowDirection = default)
    {
        SpawnSparks(position + Vector3.up * 1.1f, new Color(0.5f, 0.02f, 0.01f), 22, blowDirection);
        SpawnSparks(position + Vector3.up * 0.7f, new Color(0.4f, 0.03f, 0.02f), 12, blowDirection);
        PlayDust(position, 7);
    }

    // A shattered guard: a heavy metallic crack and a bright burst of sparks,
    // distinct from the soft thud of a guard that held. Plays on top of the
    // landed-hit impact that follows.
    public void PlayGuardBreak(Vector3 position)
    {
        PlaySpatial(guardBreakClip, position + Vector3.up * 1.2f, 0.95f, Random.Range(0.86f, 0.96f), 20f);
        SpawnSparks(position + Vector3.up * 1.2f, new Color(1f, 0.62f, 0.2f), 14);
    }

    private static AudioClip LoadClip(string resourcePath) => Resources.Load<AudioClip>(resourcePath);

    private void PlaySpatial(AudioClip clip, Vector3 position, float volume, float pitch, float maxDistance)
    {
        if (clip == null || spatialPool.Count == 0)
            return;
        AudioSource source = spatialPool[spatialCursor];
        spatialCursor = (spatialCursor + 1) % spatialPool.Count;
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

    private void SpawnSparks(Vector3 position, Color color, int count, Vector3 direction = default)
    {
        ParticleSystem particles = particlePool[particleCursor];
        particleCursor = (particleCursor + 1) % particlePool.Count;
        particles.transform.position = position;
        // The cone emitter fires along its local +Z; aim it down the blow (or up when
        // no direction is given, e.g. arrow hits and guard breaks).
        Vector3 aim = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up;
        particles.transform.rotation = Quaternion.LookRotation(aim);
        ParticleSystem.MainModule main = particles.main;
        main.startColor = color;
        particles.Emit(count);
    }

    // A soft kicked-up dust puff (footsteps, killing blows). Skipped on the low tier.
    public void PlayDust(Vector3 position, int count)
    {
        if (dustPool.Count == 0 || GraphicsQuality.IsLow)
            return;
        ParticleSystem particles = dustPool[dustCursor];
        dustCursor = (dustCursor + 1) % dustPool.Count;
        particles.transform.position = position;
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
        // A cone (rather than a sphere) so the burst throws along the blow direction
        // set by SpawnSparks; a wide angle keeps it reading as a splash, not a jet.
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.08f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = RuntimeAssets.SoftParticleMaterial(); // soft round sparks, not hard squares
        return particles;
    }

    private ParticleSystem CreateDustEmitter(int index)
    {
        GameObject go = new($"Dust {index}");
        go.transform.SetParent(transform);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.gravityModifier = -0.05f; // drifts up briefly then settles
        main.maxParticles = 32;
        main.startColor = new Color(0.55f, 0.5f, 0.42f, 0.5f);
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.2f;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = RuntimeAssets.SoftParticleMaterial();
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

    // Sparse, frequency-swept chirps over quiet — a synthesized songbird bed for the
    // wooded arenas (no curated nature clips ship yet).
    private static AudioClip CreateBirdsong()
    {
        const int sampleRate = 22050;
        const float duration = 7f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        for (int c = 0; c < 14; c++)
        {
            int start = Mathf.RoundToInt(Random.Range(0f, duration - 0.4f) * sampleRate);
            float f0 = Random.Range(2200f, 3200f);
            float f1 = f0 + Random.Range(-600f, 900f);
            int chirp = Mathf.RoundToInt(Random.Range(0.08f, 0.18f) * sampleRate);
            float amp = Random.Range(0.06f, 0.13f);
            for (int i = 0; i < chirp && start + i < length; i++)
            {
                float u = i / (float)chirp;
                float freq = Mathf.Lerp(f0, f1, u);
                float env = Mathf.Sin(u * Mathf.PI);
                samples[start + i] += Mathf.Sin((start + i) / (float)sampleRate * freq * Mathf.PI * 2f) * env * amp;
            }
        }
        AudioClip clip = AudioClip.Create("Birdsong", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // A faint high insect trill under periodic low frog croaks, for the marsh.
    private static AudioClip CreateMarshChorus()
    {
        const int sampleRate = 22050;
        const float duration = 6f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            samples[i] += Mathf.Sin(t * 4200f * Mathf.PI * 2f) * (0.5f + 0.5f * Mathf.Sin(t * 40f * Mathf.PI * 2f)) * 0.015f;
        }
        for (int c = 0; c < 10; c++)
        {
            int start = Mathf.RoundToInt(Random.Range(0f, duration - 0.5f) * sampleRate);
            float freq = Random.Range(90f, 150f);
            int croak = Mathf.RoundToInt(Random.Range(0.18f, 0.32f) * sampleRate);
            float amp = Random.Range(0.08f, 0.16f);
            for (int i = 0; i < croak && start + i < length; i++)
            {
                float u = i / (float)croak;
                float env = Mathf.Sin(u * Mathf.PI);
                float am = 0.6f + 0.4f * Mathf.Sin(u * 30f * Mathf.PI * 2f);
                samples[start + i] += Mathf.Sin((start + i) / (float)sampleRate * freq * Mathf.PI * 2f) * env * am * amp;
            }
        }
        AudioClip clip = AudioClip.Create("Marsh Chorus", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Steady, lightly-filtered noise — a rain hiss bed.
    private static AudioClip CreateRainLoop()
    {
        const int sampleRate = 22050;
        const float duration = 4f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        float filtered = 0f;
        for (int i = 0; i < length; i++)
        {
            filtered = Mathf.Lerp(filtered, Random.Range(-1f, 1f), 0.5f);
            samples[i] = filtered * 0.18f;
        }
        AudioClip clip = AudioClip.Create("Rain Loop", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Gustier filtered noise than the base ambient bed, for exposed highland wind.
    private static AudioClip CreateWindGust()
    {
        const int sampleRate = 22050;
        const float duration = 6f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        float filtered = 0f;
        for (int i = 0; i < length; i++)
        {
            filtered = Mathf.Lerp(filtered, Random.Range(-1f, 1f), 0.04f);
            float t = i / (float)sampleRate;
            float gust = 0.4f + 0.45f * Mathf.Sin(t * Mathf.PI * 0.5f) * Mathf.Sin(t * Mathf.PI * 0.17f + 1f);
            samples[i] = filtered * Mathf.Max(0f, gust) * 0.22f;
        }
        AudioClip clip = AudioClip.Create("Wind Gust", length, 1, sampleRate, false);
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

    // A metallic crack: a low fundamental with a clangy overtone and a noisy
    // attack, decaying fast — the sound of a guard buckling under a blow.
    private static AudioClip CreateGuardBreak()
    {
        const int sampleRate = 22050;
        const float duration = 0.26f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 16f);
            float fundamental = Mathf.Sin(t * 196f * Mathf.PI * 2f);
            float clang = Mathf.Sin(t * 523f * Mathf.PI * 2f) * 0.5f;
            float crack = Random.Range(-1f, 1f) * Mathf.Exp(-t * 60f);
            samples[i] = (fundamental + clang) * envelope * 0.32f + crack * 0.45f;
        }
        AudioClip clip = AudioClip.Create("Guard Break", length, 1, sampleRate, false);
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
