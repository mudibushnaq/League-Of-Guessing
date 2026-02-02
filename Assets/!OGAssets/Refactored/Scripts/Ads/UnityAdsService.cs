// Assets/!OGAssets/Refactored/Scripts/Ads/UnityAdsService.cs
#nullable enable
using System;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.Advertisements;

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(UnityAdsService),
    gameObjectName: nameof(UnityAdsService),
    extraBindings: typeof(IAdsService))]
public sealed class UnityAdsService :
    MonoBehaviour,
    IAdsService,
    IUnityAdsInitializationListener,
    IUnityAdsLoadListener,
    IUnityAdsShowListener, IProjectInitializable
{
    int IProjectInitializable.Order => -100;
    
    [Header("Assign config asset with Game IDs + Placement IDs")]
    [SerializeField] private UnityAdsConfig config;

    // ---- IAdsService ----
    public bool IsInitialized { get; private set; }
    public event Action? OnInterstitialClosed;
    public event Action? OnRewardedClosed;
    public event Action? OnRewardGranted;

    // Track readiness
    private bool _rewardedReady;
    private bool _interstitialReady;

    // Await show completion
    private UniTaskCompletionSource<bool>? _showTcs;
    private Action? _pendingRewardCallback;

    // Optional init awaiter
    private UniTaskCompletionSource? _initTcs;

    // ---- Platform helpers ----
    private string GameId =>
#if UNITY_IOS
        config.iosGameId;
#else
        config.androidGameId; // Editor defaults to Android ids
#endif

    private string RewardedId =>
#if UNITY_IOS
        config.rewardedAdUnitId_iOS;
#else
        config.rewardedAdUnitId_Android;
#endif

    private string InterstitialId =>
#if UNITY_IOS
        config.interstitialAdUnitId_iOS;
#else
        config.interstitialAdUnitId_Android;
#endif

    private string BannerId =>
#if UNITY_IOS
        config.bannerAdUnitId_iOS;
#else
        config.bannerAdUnitId_Android;
#endif
    
    async UniTask IProjectInitializable.Initialize()
    {
        await InitializeAsync();
        Debug.Log("[IProjectInitializable.Initialize] UnityAdsService ready.");
    }

    // ---- IAdsService.InitializeAsync (userId ignored by Ads v4) ----
    public async UniTask InitializeAsync(string userId = null)
    {
        if (IsInitialized) return;
        if (_initTcs != null) { await _initTcs.Task; return; }

        // Ads are not supported on desktop builds; skip initialization.
        if (Application.platform != RuntimePlatform.Android &&
            Application.platform != RuntimePlatform.IPhonePlayer)
        {
            IsInitialized = true;
            _rewardedReady = false;
            _interstitialReady = false;
            Debug.Log("[Ads] Skipping Unity Ads init on non-mobile platform.");
            return;
        }

        if (config == null)
            throw new InvalidOperationException("[Ads] UnityAdsConfig is not assigned.");

        _initTcs = new UniTaskCompletionSource();

        // New API: pass this as IUnityAdsInitializationListener
        Advertisement.Initialize(GameId, config.testMode, this);

        // Wait for OnInitializationComplete / OnInitializationFailed
        await _initTcs.Task;

        // Proactively load placements (guard empty)
        if (Has(RewardedId))     Advertisement.Load(RewardedId, this);
        else Debug.LogWarning("[Ads] RewardedId is empty; skipping preload.");

        if (Has(InterstitialId)) Advertisement.Load(InterstitialId, this);
        else Debug.LogWarning("[Ads] InterstitialId is empty; skipping preload.");
    }

    // ---------------- IUnityAdsInitializationListener ----------------
    public void OnInitializationComplete()
    {
        IsInitialized = true;
        _initTcs?.TrySetResult();
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.LogError($"[Ads] Init failed: {error} - {message}");
        // Let the awaiter continue; you can choose to throw in callers if needed
        _initTcs?.TrySetResult();
    }

    // ---------------- IUnityAdsLoadListener ----------------
    public void OnUnityAdsAdLoaded(string placementId)
    {
        if (placementId == RewardedId)      _rewardedReady = true;
        else if (placementId == InterstitialId) _interstitialReady = true;
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        if (placementId == RewardedId)      _rewardedReady = false;
        else if (placementId == InterstitialId) _interstitialReady = false;

        Debug.LogWarning($"[Ads] Load failed: {placementId} - {error} - {message}");
    }

    // ---------------- IUnityAdsShowListener ----------------
    public void OnUnityAdsShowStart(string placementId) { }

    public void OnUnityAdsShowClick(string placementId) { }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogWarning($"[Ads] Show failed: {placementId} - {error} - {message}");
        _showTcs?.TrySetResult(false);

        // Invalidate and reload for next time
        if (placementId == RewardedId)      _rewardedReady = false;
        else if (placementId == InterstitialId) _interstitialReady = false;

        Advertisement.Load(placementId, this);
    }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
    {
        bool rewarded = placementId == RewardedId && state == UnityAdsShowCompletionState.COMPLETED;
        if (rewarded)
        {
            _pendingRewardCallback?.Invoke();
            OnRewardGranted?.Invoke();
        }

        if (placementId == RewardedId)      OnRewardedClosed?.Invoke();
        else if (placementId == InterstitialId) OnInterstitialClosed?.Invoke();

        _showTcs?.TrySetResult(true);

        // Mark not-ready and start loading next
        if (placementId == RewardedId)      _rewardedReady = false;
        else if (placementId == InterstitialId) _interstitialReady = false;

        Advertisement.Load(placementId, this);
    }

    // ---------------- IAdsService: Rewarded ----------------
    public bool IsRewardedReady => _rewardedReady;

    public async UniTask<bool> ShowRewardedAsync(string placement = null, Action onReward = null)
    {
        if (!IsMobilePlatform)
            return false;

        var id = placement ?? RewardedId;
        if (!Has(id)) { Debug.LogWarning("[Ads] Rewarded placement is empty."); return false; }
        if (!_rewardedReady) { Advertisement.Load(id, this); return false; }

        _pendingRewardCallback = onReward;
        _showTcs = new UniTaskCompletionSource<bool>();
        Advertisement.Show(id, this);
        var result = await _showTcs.Task;
        _pendingRewardCallback = null;
        return result;
    }

    // ---------------- IAdsService: Interstitial ----------------
    public bool IsInterstitialReady => _interstitialReady;

    public async UniTask<bool> ShowInterstitialAsync(string placement = null)
    {
        if (!IsMobilePlatform)
            return false;

        var id = placement ?? InterstitialId;
        if (!Has(id)) { Debug.LogWarning("[Ads] Interstitial placement is empty."); return false; }
        if (!_interstitialReady) { Advertisement.Load(id, this); return false; }

        _showTcs = new UniTaskCompletionSource<bool>();
        Advertisement.Show(id, this);
        return await _showTcs.Task;
    }

    // ---------------- IAdsService: Banners ----------------
    public void ShowBanner()
    {
        // Ads are not supported on desktop/editor builds.
        if (!IsMobilePlatform)
        {
            Debug.Log("[Ads][Banner] Skipping banner on non-mobile platform.");
            return;
        }

        var id =  BannerId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[Ads][Banner] Banner placement is empty.");
            return;
        }

        if (!IsInitialized)
        {
            Debug.Log("[Ads][Banner] SDK not initialized yet; delaying banner load...");
            // try again on next frame
            print(""); // no-op to keep on main thread
            Advertisement.Initialize(GameId, config.testMode, this);
            // You can also schedule a retry from BannerController after init event.
            return;
        }

        Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);

        var loadOpts = new BannerLoadOptions
        {
            loadCallback = () =>
            {
                Debug.Log("[Ads][Banner] Load success → showing.");
                var showOpts = new BannerOptions
                {
                    showCallback = () => Debug.Log("[Ads][Banner] Shown."),
                    hideCallback = () => Debug.Log("[Ads][Banner] Hidden."),
                    clickCallback = () => Debug.Log("[Ads][Banner] Click.")
                };
                Advertisement.Banner.Show(id, showOpts);
            },
            errorCallback = (msg) =>
            {
                Debug.LogWarning($"[Ads][Banner] Load error: {msg}");
            }
        };

        Debug.Log($"[Ads][Banner] Loading banner: {id}");
        Advertisement.Banner.Load(id, loadOpts);
    }

    public void HideBanner()
    {
        Advertisement.Banner.Hide();
    }
    
    private static bool Has(string s) => !string.IsNullOrEmpty(s);
    
    public UniTask PreloadRewardedAsync()
    {
        if (!IsMobilePlatform)
            return UniTask.CompletedTask;

        if (Has(RewardedId) && !_rewardedReady)
            Advertisement.Load(RewardedId, this);
        return UniTask.CompletedTask;
    }

    public UniTask PreloadInterstitialAsync()
    {
        if (!IsMobilePlatform)
            return UniTask.CompletedTask;

        if (Has(InterstitialId) && !_interstitialReady)
            Advertisement.Load(InterstitialId, this);
        return UniTask.CompletedTask;
    }

    private static bool IsMobilePlatform =>
        Application.platform == RuntimePlatform.Android ||
        Application.platform == RuntimePlatform.IPhonePlayer;
}
