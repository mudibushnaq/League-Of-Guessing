// Assets/!OGAssets/Refactored/Scripts/Ads/InterstitialPacingService.cs
#nullable enable
using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

public interface IInterstitialPacingService
{
    /// Increment counter for the event and try to show if policy says so.
    UniTask TrackAsync(string eventKey);

    /// Force re-evaluate (e.g., after ads load state changes)
    UniTask TryShowIfEligibleAsync(string eventKey);

    /// Returns true if global cooldown allows showing right now (doesn’t check per-event caps).
    bool IsGlobalWindowOpen();
}

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(InterstitialPacingService),
    gameObjectName: nameof(InterstitialPacingService),
    extraBindings: typeof(IInterstitialPacingService))]
public sealed class InterstitialPacingService : MonoBehaviour, IInterstitialPacingService, IProjectInitializable
{
    int IProjectInitializable.Order => 150;
    
    [SerializeField] private InterstitialPacingConfig config;
    [Inject] private IAdsService _ads;

    // PlayerPrefs keys
    const string PP_EVT_COUNT_PREFIX = "IP_EVT_CNT_";      // + eventKey => int count
    const string PP_EVT_DAY_PREFIX   = "IP_EVT_DAY_";      // + eventKey => "yyyyMMdd:shownCount"
    const string PP_LAST_SHOWN_TICKS = "IP_LAST_SHOWN_TKS";

    UniTask IProjectInitializable.Initialize()
    {
        if (!config) Debug.LogWarning("[InterstitialPacing] Missing config.");
        Debug.Log("[IProjectInitializable.Initialize] InterstitialPacingService ready.");
        return UniTask.CompletedTask;
    }

    public bool IsGlobalWindowOpen()
    {
        var lastTicks = long.Parse(PlayerPrefs.GetString(PP_LAST_SHOWN_TICKS, "0"));
        if (lastTicks <= 0) return true;
        var last = new DateTime(lastTicks, DateTimeKind.Utc);
        var dt = (DateTime.UtcNow - last).TotalSeconds;
        int minGap = config ? config.globalMinSecondsBetweenShows : 45;
        return dt >= minGap;
    }

    public async UniTask TrackAsync(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || config == null) return;
        if (!config.TryGetRule(eventKey, out var rule)) return;

        // Increment the event counter
        int count = PlayerPrefs.GetInt(PP_EVT_COUNT_PREFIX + eventKey, 0) + 1;
        PlayerPrefs.SetInt(PP_EVT_COUNT_PREFIX + eventKey, count);
        PlayerPrefs.Save();

        // Only attempt when hitting a multiple of threshold
        if (rule.threshold <= 0 || (count % rule.threshold) != 0) return;

        await TryShowIfEligibleAsync(eventKey);
    }

    public async UniTask TryShowIfEligibleAsync(string eventKey)
    {
        if (_ads == null || !(_ads.IsInitialized)) return;
        if (config == null || !config.TryGetRule(eventKey, out var rule)) return;

        // Check per-day cap for this event
        if (!PassesPerDayCap(eventKey, rule.perDayCap)) return;

        // Check global window + per-event cooldown
        if (!IsGlobalWindowOpen()) return;
        if (!PassesPerEventCooldown(rule.minCooldownSeconds)) return;

        // Try to show
        if (!_ads.IsInterstitialReady)
            await _ads.PreloadInterstitialAsync();

        // Don’t block flow if it’s still not ready
        await _ads.ShowInterstitialAsync();

        // Record show
        PlayerPrefs.SetString(PP_LAST_SHOWN_TICKS, DateTime.UtcNow.Ticks.ToString());
        BumpPerDayCounter(eventKey);
        PlayerPrefs.Save();
    }

    bool PassesPerEventCooldown(int minCooldownSeconds)
    {
        var lastTicks = long.Parse(PlayerPrefs.GetString(PP_LAST_SHOWN_TICKS, "0"));
        if (lastTicks <= 0) return true;
        var last = new DateTime(lastTicks, DateTimeKind.Utc);
        var dt = (DateTime.UtcNow - last).TotalSeconds;
        return dt >= Mathf.Max(0, minCooldownSeconds);
    }

    bool PassesPerDayCap(string eventKey, int perDayCap)
    {
        if (perDayCap <= 0) return true; // unlimited
        var (today, shown) = ReadPerDay(eventKey);
        return shown < perDayCap;
    }

    void BumpPerDayCounter(string eventKey)
    {
        var (today, shown) = ReadPerDay(eventKey);
        shown++;
        PlayerPrefs.SetString(PP_EVT_DAY_PREFIX + eventKey, $"{today}:{shown}");
    }

    (string day, int shown) ReadPerDay(string eventKey)
    {
        var key = PP_EVT_DAY_PREFIX + eventKey;
        var today = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var packed = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(packed)) return (today, 0);
        var parts = packed.Split(':');
        if (parts.Length != 2) return (today, 0);
        if (parts[0] != today) return (today, 0);
        return (today, int.TryParse(parts[1], out var n) ? n : 0);
    }
}
