using UnityEngine;

[CreateAssetMenu(menuName = "LOG/Config/Ads/UnityAds")]
public class UnityAdsConfig : ScriptableObject
{
    [Header("Unity Ads Game IDs")]
    public string androidGameId;
    public string iosGameId;

    [Header("Ad Unit IDs (from Unity Dashboard)")]
    public string rewardedAdUnitId_Android;
    public string interstitialAdUnitId_Android;
    public string bannerAdUnitId_Android;

    public string rewardedAdUnitId_iOS;
    public string interstitialAdUnitId_iOS;
    public string bannerAdUnitId_iOS;

    [Header("Settings")]
    public bool testMode = true;
    public bool enablePerPlacementLoad = true;
}