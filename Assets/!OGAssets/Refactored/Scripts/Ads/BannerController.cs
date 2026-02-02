// Assets/!OGAssets/Refactored/Scripts/Ads/BannerController.cs
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(BannerController),
    context: AppContextType.MenuScene)]
public sealed class BannerController : MonoBehaviour, IMenuInitializable
{
    int IMenuInitializable.Order => 0;
    
    [Inject] private IAdsService _ads;
    
    
    UniTask IMenuInitializable.Initialize()
    {
        return UniTask.CompletedTask;
    }
    
    async void OnEnable()
    {
        // Wait until Ads is initialized
        for (int i = 0; i < 120 && _ads != null && !_ads.IsInitialized; i++)
            await UniTask.DelayFrame(1);

        // ✅ Always use config default from UnityAdsService
        //_ads?.ShowBanner();

#if UNITY_EDITOR
        // Optional: show your debug overlay/spacer even in Editor
#endif
        
    }

    void OnDestroy()
    {
        if (_ads != null) 
            _ads.HideBanner();
    }
}