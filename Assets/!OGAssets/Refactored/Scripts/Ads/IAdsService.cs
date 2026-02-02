// IAdsService.cs
using Cysharp.Threading.Tasks;

public interface IAdsService
{
    bool IsInitialized { get; }
    UniTask InitializeAsync(string userId = null);

    // NEW:
    UniTask PreloadRewardedAsync();
    UniTask PreloadInterstitialAsync();

    bool IsRewardedReady { get; }
    UniTask<bool> ShowRewardedAsync(string placement = null, System.Action onReward = null);

    bool IsInterstitialReady { get; }
    UniTask<bool> ShowInterstitialAsync(string placement = null);

    void ShowBanner();
    void HideBanner();

    event System.Action OnInterstitialClosed;
    event System.Action OnRewardedClosed;
    event System.Action OnRewardGranted;
}