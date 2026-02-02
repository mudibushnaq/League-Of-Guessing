// SocialVisitService.cs
#nullable enable
using System;
using UnityEngine;
using Zenject;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEditor;

[Serializable]
public sealed class SocialVisitConfig
{
    public SocialKind kind;
    [Tooltip("Full https:// URL")]
    public string url;
    [Tooltip("Pack rewarded on return (PackService handles cooldown/daily caps).")]
    public PackDefinition pack;
    [Tooltip("If true, reward can be claimed only once in the player's lifetime.")]
    public bool lifetimeOnce = true;

    [Tooltip("Still open the link even if the one-time reward has already been claimed.")]
    public bool openEvenIfClaimed = true;
}

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(SocialVisitService),
    gameObjectName: nameof(SocialVisitService),
    extraBindings: typeof(ISocialVisitService))]
public sealed class SocialVisitService : MonoBehaviour, ISocialVisitService, IProjectInitializable
{
    int IProjectInitializable.Order => 100;
    
    [Header("Links + Rewards")]
    [SerializeField] private SocialVisitConfig[] entries =
    {
        new() { kind = SocialKind.Facebook  },
        new() { kind = SocialKind.Twitch    },
        new() { kind = SocialKind.Instagram },
        new() { kind = SocialKind.Discord   },
    };

    [SerializeField] private bool debugLogs = true;

    [Inject] private IPackService _packs = null!;

    string? _pending;   // SocialKind.ToString()
    bool _awaitingReturn;
    
    UniTask IProjectInitializable.Initialize()
    {
        return UniTask.CompletedTask;
    }
    
    public bool Open(SocialKind kind)
    {
        var cfg = Find(kind);
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.url))
        {
            if (debugLogs) Debug.LogWarning($"[Social] Missing URL for {kind}");
            return false;
        }

        // 🔒 One-time lifetime check BEFORE opening
        if (cfg.lifetimeOnce && IsLifetimeClaimed(kind))
        {
            if (debugLogs) Debug.Log($"[Social] {kind} already claimed (lifetime).");
            if (!cfg.openEvenIfClaimed) return false; // block if desired
            // open anyway, but skip reward path
            var urlClaimed = SanitizeUrl(cfg.url);
            if (string.IsNullOrEmpty(urlClaimed)) return false;
#if UNITY_EDITOR
            EditorUtility.OpenWithDefaultApp(urlClaimed);
#else
        Application.OpenURL(urlClaimed);
#endif
            _pending = null;
            _awaitingReturn = false;
            return true;
        }
        var url = SanitizeUrl(cfg.url);
        if (string.IsNullOrEmpty(url))
        {
            if (debugLogs) Debug.LogWarning($"[Social] Invalid URL for {kind}.");
            return false;
        }

        _pending = kind.ToString();
        _awaitingReturn = true;

#if UNITY_EDITOR
        EditorUtility.OpenWithDefaultApp(url);
#else
    Application.OpenURL(url);
#endif

        if (debugLogs) Debug.Log($"[Social] Opened {kind} → {url}");
        return true;
    }

    SocialVisitConfig? Find(SocialKind k)
    {
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] != null && entries[i].kind == k) return entries[i];
        return null;
    }

    void OnApplicationFocus(bool focus) { if (focus) TryGrantOnReturn().Forget(); }
    void OnApplicationPause(bool pause) { if (!pause) TryGrantOnReturn().Forget(); }

    async UniTaskVoid TryGrantOnReturn()
    {
        if (!_awaitingReturn || string.IsNullOrEmpty(_pending)) return;

        var kind = (SocialKind)Enum.Parse(typeof(SocialKind), _pending!);
        var cfg  = Find(kind);

        _awaitingReturn = false; // single attempt per open
        _pending = null;

        if (cfg == null || cfg.pack == null) return;

        // 🔒 Re-check lifetime gate on return (defense in depth)
        if (cfg.lifetimeOnce && IsLifetimeClaimed(kind))
        {
            if (debugLogs) Debug.Log($"[Social] {kind} already lifetime-claimed (on return).");
            return;
        }

        // Let PackService enforce its own caps/cooldowns; we still want that behavior
        var ok = await _packs.ClaimAsync(cfg.pack);

        if (ok)
        {
            if (cfg.lifetimeOnce)
                MarkLifetimeClaimed(kind);

            if (debugLogs) Debug.Log($"[Social] Granted {cfg.pack.packId} for {kind} (lifetimeOnce={cfg.lifetimeOnce}).");
        }
        else if (debugLogs)
        {
            Debug.Log($"[Social] Claim denied by PackService for {kind} (cooldown/cap/etc).");
        }
    }
    
    const string PP_SOCIAL_ONCE_PREFIX = "SOCIAL_ONCE_";

    bool IsLifetimeClaimed(SocialKind kind)
        => PlayerPrefs.GetInt(PP_SOCIAL_ONCE_PREFIX + kind, 0) == 1;

    void MarkLifetimeClaimed(SocialKind kind)
    {
        PlayerPrefs.SetInt(PP_SOCIAL_ONCE_PREFIX + kind, 1);
        PlayerPrefs.Save();
    }
    
    static string SanitizeUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var u = raw.Trim();
        // if no scheme present, assume https
        if (!u.StartsWith("http://") && !u.StartsWith("https://"))
            u = "https://" + u;
        return u;
    }
    
    
    
}