// SettingsPanel.cs
#nullable enable
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(SettingsPanel),
    gameObjectName: nameof(SettingsPanel))]
public sealed class SettingsPanel : MonoBehaviour, IPopupContent, IProjectInitializable
{
    int IProjectInitializable.Order => 150;
    
    [Header("Sliders")]
    [SerializeField] private Slider bgmSlider = null!;
    [SerializeField] private Slider sfxSlider = null!;
    [SerializeField] private Slider vibrationSlider = null!; // 1 = ON, 0 = OFF

    [Header("Social Buttons")]
    [SerializeField] private Button facebookBtn = null!;
    [SerializeField] private Button twitchBtn   = null!;
    [SerializeField] private Button instagramBtn= null!;
    [SerializeField] private Button discordBtn  = null!;
    
    [SerializeField] private Button cancelButton  = null!;
    
    private UniTaskCompletionSource<PopupResult> _tcs;
    private PopupRequest req;
    
    [Inject] ISettingsService _settings;
    [Inject] private ISocialVisitService _social;
    
    UniTask IProjectInitializable.Initialize()
    {
        //DontDestroyOnLoad(gameObject);
        return UniTask.CompletedTask;
    }
    
    public void Bind(PopupRequest request, UniTaskCompletionSource<PopupResult> tcs)
    {
        req = request; _tcs = tcs;
        
        // Slider ranges (defensive)
        if (bgmSlider) { bgmSlider.minValue = 0f; bgmSlider.maxValue = 1f; bgmSlider.wholeNumbers = false; }
        if (sfxSlider) { sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f; sfxSlider.wholeNumbers = false; }
        if (vibrationSlider) { vibrationSlider.minValue = 0f; vibrationSlider.maxValue = 1f; vibrationSlider.wholeNumbers = true; }
        
        // Initial UI from service
        if (_settings != null)
        {
            if (bgmSlider)       bgmSlider.SetValueWithoutNotify(_settings.BgmVolume01);
            if (sfxSlider)       sfxSlider.SetValueWithoutNotify(_settings.SfxVolume01);
            if (vibrationSlider) vibrationSlider.SetValueWithoutNotify(_settings.VibrationEnabled ? 1f : 0f);
        }

        // Wire events
        if (bgmSlider) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (vibrationSlider) vibrationSlider.onValueChanged.AddListener(OnVibrationChanged);

        if (facebookBtn)  facebookBtn.onClick.AddListener(() => _social?.Open(SocialKind.Facebook));
        if (twitchBtn)    twitchBtn.onClick.AddListener(() => _social?.Open(SocialKind.Twitch));
        if (instagramBtn) instagramBtn.onClick.AddListener(() => _social?.Open(SocialKind.Instagram));
        if (discordBtn)   discordBtn.onClick.AddListener(() => _social?.Open(SocialKind.Discord));
        if (cancelButton) cancelButton.onClick.AddListener(OnCancelClicked);
    }
    
    private void OnCancelClicked()
    {
        // Reset UI, then close with 0 purchased
        req.PressedToButton.interactable = true;
        if (bgmSlider) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (sfxSlider) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (vibrationSlider) vibrationSlider.onValueChanged.RemoveListener(OnVibrationChanged);

        if (facebookBtn)  facebookBtn.onClick.RemoveAllListeners();
        if (twitchBtn)    twitchBtn.onClick.RemoveAllListeners();
        if (instagramBtn) instagramBtn.onClick.RemoveAllListeners();
        if (discordBtn)   discordBtn.onClick.RemoveAllListeners();
        _tcs?.TrySetResult(PopupResult.Secondary);
    }

    void OnBgmChanged(float v)         { if (_settings != null) _settings.BgmVolume01 = v; }
    void OnSfxChanged(float v)         { if (_settings != null) _settings.SfxVolume01 = v; }
    void OnVibrationChanged(float v01)
    {
        bool on = v01 >= 0.5f;
        if (_settings != null) _settings.VibrationEnabled = on;
        if (vibrationSlider) vibrationSlider.SetValueWithoutNotify(on ? 1f : 0f); // snap
    }
}
