// Assets/Audio/AudioManager.cs
#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.Audio;

// Assets/Audio/IAudioService.cs
public interface IAudioService
{
    float SfxVolume01 { get; set; } // 0..1 (linear)
    float BgmVolume01 { get; set; } // 0..1 (linear)

    void PlaySfx(SfxId id);
    void PlaySfx(SfxId id, Vector3 worldPos); // 3D if you want

    UniTask PlayBgmAsync(BgmId id, float crossFade = 0.5f);
    UniTask StopBgmAsync(float fadeOut = 0.3f);

    /// Soft fade (duck) the BGM, then restore. Perfect for “correct answer”.
    UniTask DuckBgmAsync(float duckDb = -10f, float fadeOut = 0.12f, float hold = 0.25f, float fadeIn = 0.25f);
}

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(AudioManager),
    gameObjectName: nameof(AudioManager),
    extraBindings: typeof(IAudioService))]
public sealed class AudioManager : MonoBehaviour, IAudioService, IProjectInitializable
{
    int IProjectInitializable.Order => -100;
    
    [Header("Auto-Duck Defaults (by Category)")]
    [SerializeField] private bool duck_Default = false;
    [SerializeField, Range(-30f, 0f)] private float duckDb_Default = -8f;
    [SerializeField, Min(0f)] private float duckFadeOut_Default = 0.12f;
    [SerializeField, Min(0f)] private float duckFadeIn_Default  = 0.25f;

    [SerializeField] private bool duck_UI = false;
    [SerializeField, Range(-30f, 0f)] private float duckDb_UI = -6f;
    [SerializeField, Min(0f)] private float duckFadeOut_UI = 0.08f;
    [SerializeField, Min(0f)] private float duckFadeIn_UI  = 0.18f;

    [SerializeField] private bool duck_Gameplay = false;
    [SerializeField, Range(-30f, 0f)] private float duckDb_Gameplay = -6f;
    [SerializeField, Min(0f)] private float duckFadeOut_Gameplay = 0.10f;
    [SerializeField, Min(0f)] private float duckFadeIn_Gameplay  = 0.22f;

    [SerializeField] private bool duck_Voice = true;     // ← ON by default
    [SerializeField, Range(-30f, 0f)] private float duckDb_Voice = -12f;
    [SerializeField, Min(0f)] private float duckFadeOut_Voice = 0.12f;
    [SerializeField, Min(0f)] private float duckFadeIn_Voice  = 0.30f;

    [SerializeField] private bool duck_Priority = true;  // ← ON by default
    [SerializeField, Range(-30f, 0f)] private float duckDb_Priority = -14f;
    [SerializeField, Min(0f)] private float duckFadeOut_Priority = 0.12f;
    [SerializeField, Min(0f)] private float duckFadeIn_Priority  = 0.30f;
    readonly List<float> _activeDucksDb = new(); // store targetDb (absolute mixer dB)
    bool _duckAnimating;
    float _pendingFadeIn = 0.25f;
    float _bgmBaseDb; // un-ducked target for BGM_VOL
    
    [Header("Mixer & Params")]
    [SerializeField] public AudioMixer mixer;
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup sfxGroup; // optional but recommended
    
    [SerializeField] private string bgmParam = "BGM_VOL"; // dB
    [SerializeField] private string sfxParam = "SFX_VOL"; // dB

    [Header("Libraries")]
    [SerializeField] private SfxLibrary sfxLibrary;
    [SerializeField] private BgmLibrary bgmLibrary;
    
    [Header("BGM")]
    [SerializeField] private AudioSource bgmA;
    [SerializeField] private AudioSource bgmB;// route to BGM group
    
    private AudioSource _bgmActive, _bgmIdle;
    private bool _bgmBusy;

    [Header("SFX")]
    [SerializeField] private Transform sfxRoot;
    [SerializeField] private AudioSource sfxPrefab; // route to SFX group
    [SerializeField] private int sfxPoolSize = 12;
    private readonly Queue<AudioSource> _sfxPool = new();

    // Persisted volumes
    const string PP_SFX = "AUDIO_SFX";
    const string PP_BGM = "AUDIO_BGM";
    
    UniTask IProjectInitializable.Initialize()
    {
        // Prepare BGM dual sources
        if (!bgmA) bgmA = gameObject.AddComponent<AudioSource>();
        if (!bgmB) bgmB = gameObject.AddComponent<AudioSource>();
        
        _bgmActive = bgmA; _bgmIdle = bgmB;
        bgmA.loop = true; bgmB.loop = true;
        bgmA.playOnAwake = false; bgmB.playOnAwake = false;
        
        // 🔧 Route to mixer groups
        if (bgmGroup != null) {
            bgmA.outputAudioMixerGroup = bgmGroup;
            bgmB.outputAudioMixerGroup = bgmGroup;
        }
        
        // Build SFX pool
        if (!sfxRoot) sfxRoot = transform;
        for (int i = 0; i < sfxPoolSize; i++)
        {
            var src = Instantiate(sfxPrefab, sfxRoot);
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.gameObject.SetActive(true);
            if (sfxGroup != null) src.outputAudioMixerGroup = sfxGroup;
            _sfxPool.Enqueue(src);
        }

        // Load persisted volumes
        var sfx = PlayerPrefs.GetFloat(PP_SFX, 0.8f);
        var bgm = PlayerPrefs.GetFloat(PP_BGM, 0.8f);
        SfxVolume01 = sfx;
        BgmVolume01 = bgm;
        _bgmBaseDb  = ToDb(BgmVolume01);        // <-- base at boot
        Debug.Log("[IProjectInitializable.Initialize] AudioManager ready.");
        return UniTask.CompletedTask;
    }

    // ---------------- Volumes (0..1 -> dB) ----------------
    public float SfxVolume01
    {
        get => _get01(sfxParam);
        set { _set01(sfxParam, value); PlayerPrefs.SetFloat(PP_SFX, value); PlayerPrefs.Save(); }
    }
    public float BgmVolume01
    {
        get => _get01(bgmParam);
        set
        {
            _set01(bgmParam, value);
            PlayerPrefs.SetFloat(PP_BGM, value);
            PlayerPrefs.Save();
            _bgmBaseDb = ToDb(value);            // <-- keep base in sync
        }
    }

    float _get01(string p)
    {
        if (!mixer || string.IsNullOrEmpty(p)) return 1f;
        mixer.GetFloat(p, out var db);
        return DbTo01(db);
    }
    void _set01(string p, float v01)
    {
        if (!mixer || string.IsNullOrEmpty(p)) return;
        mixer.SetFloat(p, ToDb(v01));
    }

    // perceptual mapping (–80dB..0dB). Clamp to avoid -inf.
    public static float ToDb(float v01)
    {
        v01 = Mathf.Clamp01(v01);
        return (v01 <= 0.0001f) ? -80f : Mathf.Lerp(-40f, 0f, Mathf.Sqrt(v01)); // gentle curve
    }
    public static float DbTo01(float db)
    {
        db = Mathf.Clamp(db, -80f, 0f);
        var t = (db + 40f) / 40f; // inverse of above (approx)
        return Mathf.Clamp01(t * t);
    }

    // ---------------- SFX ----------------
    [SerializeField] bool debugAudio = true;
    public void PlaySfx(SfxId id) => PlaySfxInternal(id, null);
    public void PlaySfx(SfxId id, Vector3 worldPos) => PlaySfxInternal(id, worldPos);

    void PlaySfxInternal(SfxId id, Vector3? pos)
    {
        if (sfxLibrary == null) { if (debugAudio) Debug.LogWarning("[Audio] No SfxLibrary"); return; }

        if (!sfxLibrary.TryPick(id, out var pick) || pick.clip == null)
        {
            if (debugAudio) Debug.LogWarning($"[Audio] No variant/clip for {id}");
            return;
        }
        
        if (_sfxPool.Count == 0)
        {
            if (debugAudio) Debug.LogWarning("[Audio] SFX pool empty; increase pool size");
            return;
        }
        
        var src = _sfxPool.Dequeue();
        if (debugAudio) Debug.Log($"[Audio] Play {id} :: {pick.clip.name}");
        src.transform.position = pos ?? Vector3.zero;
        src.spatialBlend = pos.HasValue ? 1f : 0f;
        src.clip   = pick.clip;
        src.volume = pick.volume;
        //src.pitch  = pick.pitch * UnityEngine.Random.Range(1f - pick.pitchJitter, 1f + pick.pitchJitter);
        src.Play();

        // Decide ducking
        if (GetDuckSettings(pick, out var duck, out var duckDb, out var fadeOut, out var fadeIn) && duck)
        {
            AutoDuckWhilePlaying(src, duckDb, fadeOut, fadeIn).Forget();
        }

        ReturnWhenDone(src).Forget();
    }
    
    async UniTaskVoid AutoDuckWhilePlaying(AudioSource s, float duckDbRel, float fadeOut, float fadeIn)
    {
        // Begin duck
        var handleTask = BeginDuckAsync(duckDbRel, fadeOut);
        _pendingFadeIn = fadeIn;   // used when handle.Dispose() triggers EndDuckAsync
        var handle = await handleTask;

        // Hold while playing
        try
        {
            while (s && s.isPlaying) await UniTask.Yield();
        }
        finally
        {
            handle.Dispose(); // triggers EndDuckAsync with stored fade-in
        }
    }

    async UniTaskVoid ReturnWhenDone(AudioSource s)
    {
        // simple wait; no alloc
        while (s && s.isPlaying) await UniTask.Yield();
        if (s) { s.clip = null; s.pitch = 1f; s.volume = 1f; _sfxPool.Enqueue(s); }
    }

    // ---------------- BGM (cross-fade) ----------------
    public async UniTask PlayBgmAsync(BgmId id, float crossFade = 0.5f)
    {
        if (_bgmBusy) return; // serialize calls
        
        if (!bgmLibrary.TryPick(id, out var pick) || pick.clip == null)
        {
            if (debugAudio) Debug.LogWarning($"[Audio] No variant/clip for {id}");
            return;
        }
        
        if (_bgmActive.clip == pick.clip && _bgmActive.isPlaying) return;

        _bgmBusy = true;
        _bgmIdle.clip = pick.clip;
        _bgmIdle.volume = 0f;
        _bgmIdle.Play();

        float t = 0f;
        float startA = _bgmActive.volume;
        while (t < crossFade)
        {
            t += Time.unscaledDeltaTime;
            float k = (crossFade <= 0f) ? 1f : Mathf.Clamp01(t / crossFade);
            _bgmActive.volume = Mathf.Lerp(startA, 0f, k);
            _bgmIdle.volume   = Mathf.Lerp(0f, 1f, k);
            await UniTask.Yield();
        }

        _bgmActive.Stop();
        // swap
        ( _bgmActive, _bgmIdle ) = ( _bgmIdle, _bgmActive );
        _bgmBusy = false;
    }

    public async UniTask StopBgmAsync(float fadeOut = 0.3f)
    {
        if (!_bgmActive || !_bgmActive.isPlaying) return;
        float t = 0f;
        float start = _bgmActive.volume;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float k = (fadeOut <= 0f) ? 1f : Mathf.Clamp01(t / fadeOut);
            _bgmActive.volume = Mathf.Lerp(start, 0f, k);
            await UniTask.Yield();
        }
        _bgmActive.Stop();
        _bgmActive.volume = 1f;
    }

    // ---------------- Duck (soft fade for correct answer) ----------------
    public async UniTask DuckBgmAsync(float duckDb = -10f, float fadeOut = 0.12f, float hold = 0.25f, float fadeIn = 0.25f)
    {
        if (!mixer) return;

        // Read current linear → dB
        mixer.GetFloat(bgmParam, out var currentDb);
        float targetDb = Mathf.Clamp(currentDb + duckDb, -80f, 0f);

        // Fade down
        float t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float k = (fadeOut <= 0f) ? 1f : Mathf.Clamp01(t / fadeOut);
            mixer.SetFloat(bgmParam, Mathf.Lerp(currentDb, targetDb, k));
            await UniTask.Yield();
        }
        mixer.SetFloat(bgmParam, targetDb);

        // Hold
        if (hold > 0f) await UniTask.Delay(TimeSpan.FromSeconds(hold));

        // Fade up
        t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = (fadeIn <= 0f) ? 1f : Mathf.Clamp01(t / fadeIn);
            mixer.SetFloat(bgmParam, Mathf.Lerp(targetDb, currentDb, k));
            await UniTask.Yield();
        }
        mixer.SetFloat(bgmParam, currentDb);
    }
    
    async UniTask ApplyDuckAsync(float targetDb, float fade)
    {
        if (!mixer) return;
        mixer.GetFloat(bgmParam, out var currentDb);
        float t = 0f;
        _duckAnimating = true;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = (fade <= 0f) ? 1f : Mathf.Clamp01(t / fade);
            mixer.SetFloat(bgmParam, Mathf.Lerp(currentDb, targetDb, k));
            await UniTask.Yield();
        }
        mixer.SetFloat(bgmParam, targetDb);
        _duckAnimating = false;
    }

    float GetBaseBgmDb()
    {
        mixer.GetFloat(bgmParam, out var db);
        return db;
    }

    float ComputeAggregateDuckDb()
    {
        if (_activeDucksDb.Count == 0) return _bgmBaseDb; // back to base when no ducks
        float minDb = _activeDucksDb[0];
        for (int i = 1; i < _activeDucksDb.Count; i++)
            if (_activeDucksDb[i] < minDb) minDb = _activeDucksDb[i];
        return minDb; // deepest duck wins
    }

    async UniTask<IDisposable> BeginDuckAsync(float duckDbRel, float fadeOut)
    {
        // absolute target = base + relative
        float targetDb = Mathf.Clamp(_bgmBaseDb + duckDbRel, -80f, 0f);
        _activeDucksDb.Add(targetDb);

        float agg = ComputeAggregateDuckDb();
        await ApplyDuckAsync(agg, fadeOut);

        return new DuckHandle(this, targetDb);
    }

    async UniTask EndDuckAsync(float duckTargetDb, float fadeIn)
    {
        _activeDucksDb.Remove(duckTargetDb);

        float agg = ComputeAggregateDuckDb();   // if empty -> _bgmBaseDb
        await ApplyDuckAsync(agg, fadeIn);
    }

    sealed class DuckHandle : IDisposable
    {
        readonly AudioManager _mgr;
        readonly float _duckTargetDb;
        bool _disposed;
        public DuckHandle(AudioManager m, float targetDb) { _mgr = m; _duckTargetDb = targetDb; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _mgr.EndDuckAsync(_duckTargetDb, _mgr._pendingFadeIn).Forget();
        }
    }
    
    bool GetDuckSettings(in SfxLibrary.Variant v, out bool duck, out float duckDb, out float fadeOut, out float fadeIn)
    {
        if (v.overrideDuck)
        {
            duck    = v.duckBgm;
            duckDb  = v.duckDb;
            fadeOut = v.duckFadeOut;
            fadeIn  = v.duckFadeIn;
            return true;
        }

        switch (v.category)
        {
            case SfxCategory.UI:       duck = duck_UI;       duckDb = duckDb_UI;       fadeOut = duckFadeOut_UI;       fadeIn = duckFadeIn_UI;       return true;
            case SfxCategory.Gameplay: duck = duck_Gameplay; duckDb = duckDb_Gameplay; fadeOut = duckFadeOut_Gameplay; fadeIn = duckFadeIn_Gameplay; return true;
            case SfxCategory.Voice:    duck = duck_Voice;    duckDb = duckDb_Voice;    fadeOut = duckFadeOut_Voice;    fadeIn = duckFadeIn_Voice;    return true;
            case SfxCategory.Priority: duck = duck_Priority; duckDb = duckDb_Priority; fadeOut = duckFadeOut_Priority; fadeIn = duckFadeIn_Priority; return true;
            default:                   duck = duck_Default;  duckDb = duckDb_Default;  fadeOut = duckFadeOut_Default;  fadeIn = duckFadeIn_Default;  return true;
        }
    }
}
