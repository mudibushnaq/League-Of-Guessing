using System;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(TopBarController),
    context: AppContextType.MenuScene)]
public class TopBarController : MonoBehaviour, IMenuInitializable, IGameInitializable, IPortraitGameInitializable
{
    int IMenuInitializable.Order => 20;
    int IGameInitializable.Order => 0;
    
    int IPortraitGameInitializable.Order => 0;
    
    public WalletHud PlayerKeysHud;
    public WalletHud PlayerLPHud;

    public GameObject thisBase;
    
    [SerializeField] private Button shopButton;
    [SerializeField] private Button settingsButton;
    
    [Inject] private SettingsPanel _settingsPanel;
    [Inject] private CatalogShopUIGrid _shopGrid;
    [Inject] private PopupService _popupService;

    UniTask IMenuInitializable.Initialize()
    {
        if (shopButton) shopButton.onClick.AddListener(OnShopClicked);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);
        Debug.Log("[IProjectInitializable.Initialize] TopBarController ready.");
        return UniTask.CompletedTask;
    }
    
    UniTask IGameInitializable.Initialize()
    {
        if (shopButton) shopButton.onClick.AddListener(OnShopClicked);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);
        Debug.Log("[IProjectInitializable.Initialize] TopBarController ready.");
        return UniTask.CompletedTask;
    }
    
    UniTask IPortraitGameInitializable.Initialize()
    {
        if (shopButton) shopButton.onClick.AddListener(OnShopClicked);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);
        Debug.Log("[IPortraitGameInitializable.Initialize] TopBarController ready.");
        return UniTask.CompletedTask;
    }
    
    void OnSettingsClicked()
    { 
        var PopupRequest = new PopupRequest
        {
            Title = "Settings",
            Message = "Settings UI",
            Style = PopupStyle.Info,
            TemplateMode = PopupTemplateMode.GameObjectView,
            gm = _settingsPanel.gameObject,
            PressedToButton = settingsButton,
        };
        
        _popupService.ShowAsync(PopupRequest).Forget();
    }
    
    void OnShopClicked()
    { 
        var PopupRequest = new PopupRequest
        {
            Title = "Shop",
            Message = "Shop UI",
            Style = PopupStyle.Info,
            TemplateMode = PopupTemplateMode.GameObjectView,
            gm = _shopGrid.gameObject,
            AddressableCanvasKey = "9999",
            PressedToButton = shopButton,
            Payload = new CatalogShopUIGrid.Args { PricePerKey = CurrencyStore.LpPerKey},
        };
        
        _popupService.ShowAsync(PopupRequest).Forget();
    }
}
