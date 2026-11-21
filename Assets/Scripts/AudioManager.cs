using UnityEngine;

// One persistent audio object: 1 looping music source + pooled SFX sources.
// Survives scene reloads (restart) so the music never restarts.
// Volumes are saved (PlayerPrefs) and editable from the Settings panel.
// Extra UI/game sounds (click, whoosh, bonk, beeps, jingles) are synthesised at startup,
// so they need no extra files and work identically on Android.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips (Assets/sfx)")]
    public AudioClip backgroundMusic;
    public AudioClip jump;
    public AudioClip slide;
    public AudioClip chewCheese;

    [Header("Volumes")]
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Minimum seconds between chewing sounds when grabbing cheese quickly.")]
    public float chewCooldown = 0.3f;

    const string MusicKey = "MusicVolume", SfxKey = "SfxVolume";
    AudioSource musicSource;
    AudioSource[] sfx;
    int nextSfx;
    float nextChewTime, duck = 1f, duckTarget = 1f;
    AudioClip click, whoosh, bonk, beep, go, ding, lose, fanfare;

    public static float MusicVolume => Instance ? Instance.musicVolume : 0f;
    public static float SfxVolume => Instance ? Instance.sfxVolume : 0f;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }   // keep the first one
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicVolume = PlayerPrefs.GetFloat(MusicKey, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, sfxVolume);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
        musicSource.priority = 0;

        sfx = new AudioSource[6];
        for (int i = 0; i < sfx.Length; i++)
        {
            sfx[i] = gameObject.AddComponent<AudioSource>();
            sfx[i].playOnAwake = false;
            sfx[i].spatialBlend = 0f;
        }

        BuildSynthClips();
        if (backgroundMusic) musicSource.Play();
        else Debug.LogWarning("[AudioManager] Background music clip not assigned.");
    }

    void Update()
    {
        duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * 1.5f);
        musicSource.volume = musicVolume * duck;
    }

    // ---- settings ----
    public static void SetMusicVolume(float v)
    {
        if (!Instance) return;
        Instance.musicVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(MusicKey, Instance.musicVolume);
    }

    public static void SetSfxVolume(float v)
    {
        if (!Instance) return;
        Instance.sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(SfxKey, Instance.sfxVolume);
    }

    // lowers the music under menus / game over
    public static void Duck(bool on) { if (Instance) Instance.duckTarget = on ? 0.45f : 1f; }

    // ---- static helpers so gameplay scripts don't need references ----
    public static void PlayJump()  { if (Instance) Instance.Play(Instance.jump, Random.Range(0.95f, 1.08f)); }
    public static void PlaySlide() { if (Instance) Instance.Play(Instance.slide, Random.Range(0.95f, 1.05f)); }
    public static void PlayClick() { if (Instance) Instance.Play(Instance.click, 1f, 0.7f); }
    public static void PlayWhoosh() { if (Instance) Instance.Play(Instance.whoosh, Random.Range(0.9f, 1.15f), 0.35f); }
    public static void PlayHit()   { if (Instance) Instance.Play(Instance.bonk, Random.Range(0.92f, 1.05f), 0.9f); }
    public static void PlayBeep(bool final) { if (Instance) Instance.Play(final ? Instance.go : Instance.beep, 1f, 0.6f); }
    public static void PlayLose()  { if (Instance) Instance.Play(Instance.lose, 1f, 0.8f); }
    public static void PlayFanfare() { if (Instance) Instance.Play(Instance.fanfare, 1f, 0.7f); }

    public static void PlayChew()
    {
        if (!Instance) return;
        Instance.Play(Instance.ding, Random.Range(0.97f, 1.12f), 0.35f);   // sparkle on every pickup
        if (Time.time < Instance.nextChewTime) return;                       // no chew spam/overlap
        Instance.nextChewTime = Time.time + Instance.chewCooldown;
        Instance.Play(Instance.chewCheese, Random.Range(0.95f, 1.1f));
    }

    void Play(AudioClip clip, float pitch = 1f, float gain = 1f)
    {
        if (!clip || sfxVolume <= 0.001f) return;
        var src = sfx[nextSfx];
        nextSfx = (nextSfx + 1) % sfx.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, sfxVolume * gain);
    }

    // ---------------------------------------------------------------- synthesis
    const int Rate = 44100;

    void BuildSynthClips()
    {
        click = Synth("ui_click", 0.06f, t => Tone(t, 1250f, 0.06f, 0.002f) * 0.7f + Tone(t, 2500f, 0.03f, 0.001f) * 0.2f);
        whoosh = Synth("whoosh", 0.22f, t => Noise() * Env(t, 0.22f, 0.06f) * 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 40f)));
        bonk = Synth("bonk", 0.35f, t =>
        {
            float f = Mathf.Lerp(320f, 90f, Mathf.Clamp01(t / 0.25f));
            return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.8f + Noise() * 0.25f * Env(t, 0.05f, 0.002f)) * Env(t, 0.35f, 0.004f);
        });
        beep = Synth("beep", 0.16f, t => Square(t, 660f) * 0.25f * Env(t, 0.16f, 0.005f));
        go = Synth("go", 0.4f, t => (Square(t, 990f) * 0.2f + Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.1f) * Env(t, 0.4f, 0.005f));
        ding = Synth("ding", 0.3f, t => (Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2640f * t) * 0.25f) * Env(t, 0.3f, 0.002f));
        lose = Synth("lose", 1.1f, t => Arp(t, new[] { 523f, 466f, 392f, 330f }, 0.22f, true) * 0.35f);
        fanfare = Synth("fanfare", 1.0f, t => Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.13f, false) * 0.35f);
    }

    delegate float Wave(float t);

    static AudioClip Synth(string name, float len, Wave w)
    {
        int n = Mathf.CeilToInt(len * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(w(i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static readonly System.Random rng = new System.Random(7);
    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float Env(float t, float len, float attack) => Mathf.Clamp01(t / attack) * Mathf.Pow(Mathf.Clamp01(1f - t / len), 2f);
    static float Tone(float t, float f, float len, float attack) => Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, len, attack);
    static float Square(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t) > 0f ? 1f : -1f;

    static float Arp(float t, float[] notes, float step, bool slide)
    {
        int i = Mathf.Min(notes.Length - 1, (int)(t / step));
        float lt = t - i * step;
        float f = notes[i] * (slide && i == notes.Length - 1 ? Mathf.Lerp(1f, 0.85f, Mathf.Clamp01(lt / 0.4f)) : 1f);
        float len = i == notes.Length - 1 ? step * 3.5f : step * 1.1f;
        return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Square(t, f) * 0.15f) * Env(lt, len, 0.004f);
    }
}
