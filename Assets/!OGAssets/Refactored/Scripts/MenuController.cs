using System.Linq;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(MenuController),
    context: AppContextType.MenuScene)]
public class MenuController : MonoBehaviour, IMenuInitializable
{
    int IMenuInitializable.Order => 0;
    
    [SerializeField] private Button portraitButton;
    [SerializeField] private Button legacyButton;
    [SerializeField] private Button defaultButton;
    [Header("Loading Indicators")]
    [SerializeField] private GameObject defaultLoading;
    [SerializeField] private GameObject legacyLoading;
    [SerializeField] private GameObject portraitLoading;
    [SerializeField] private TextMeshProUGUI defaultLevelsCountText;
    [SerializeField] private TextMeshProUGUI legacyLevelsCountText;
    [SerializeField] private TextMeshProUGUI portraitLevelsCountText;
    
    public string QWERscene = "scenes/QWER_GameMode";
    public string LegacyScene = "scenes/Legacy_GameMode";
    public string PortraitScene = "scenes/Portrait_GameMode";
    
    
    [System.Serializable]
    public struct ModeEdgeUI
    {
        public string fromMode;     // e.g., "Portrait"
        public string toMode;       // e.g., "Legacy"
        public Image  arrowImage;   // arrow between buttons
        public Image  lockImage;    // lock icon near the *destination* button
    }

    [SerializeField] private ModeEdgeUI[] edges;       // set in Inspector
    [SerializeField] private Sprite lockClosedSprite;  // assign the PNG I gave you
    [SerializeField] private Sprite lockOpenSprite;    // assign the PNG I gave you
    [Header("Editor Toggles")]
    [SerializeField] private bool enableWindowsModeSelectorInEditor = false;
    
    [Inject] private ILevelsProviderService _levelsProviderService;
    [Inject] private ISceneLoader _sceneLoader;
    [Inject] private IInterstitialPacingService _interPacing;
    [Inject] private IAudioService _audio;
    [Inject] private IIapService _ads;
    [Inject] private IModeSelectionService _modeSel;
    [Inject] private IProgressionService _progression;

    private bool _startingMode;
    
    async UniTask IMenuInitializable.Initialize()
    {
        _startingMode = false;
        SetLoadingIndicators(null);

        await _interPacing.TrackAsync("menu_visit");
        
        if (defaultButton)
        {
            UpdateLevelCounterFromDiscovered(defaultLevelsCountText, "Default");
            defaultButton.onClick.AddListener(() => StartMode("Default"));
        }

        if (legacyButton)
        {
            UpdateLevelCounterFromDiscovered(legacyLevelsCountText, "Legacy");
            legacyButton.onClick.AddListener(() => StartMode("Legacy"));
        }
        
        if (portraitButton)
        {
            UpdateLevelCounterFromDiscovered(portraitLevelsCountText, "Portrait");
            portraitButton.onClick.AddListener(() => StartMode("Portrait"));
        }
        
        _audio.PlayBgmAsync(BgmId.Background).Forget();
        _ads?.ClaimAllPendingEntitlements("IAP_Restore");
        
        bool unlockAllModes = IsWindowsModeSelectorEnabled();
        _progression.Configure(new [] {
            new ModeRule {
                ModeKey = "Portrait",
                UnlockedByDefault = true
            },
            new ModeRule {
                ModeKey = "Legacy",
                UnlockedByDefault = unlockAllModes,
                Conditions = unlockAllModes ? new() : new() {
                    (() => AllSolved("Portrait"), () => SolveAllHint("Portrait")),
                }
            },
            new ModeRule {
                ModeKey = "Default", // your QWER mode
                UnlockedByDefault = unlockAllModes,
                Conditions = unlockAllModes ? new() : new() {
                    (() => AllSolved("Portrait"), () => SolveAllHint("Portrait")),
                    (() => AllSolved("Legacy"),   () => SolveAllHint("Legacy")),
                }
            }
        });
        _progression.OnProgressionChanged += RefreshMenuButtons;
        _progression.OnProgressionChanged += RefreshProgressionUI;
        RefreshMenuButtons();
        
    }
    
    private void UpdateLevelCounter(TextMeshProUGUI count, int resumeIndex,LevelsData levelsData)
    {
        var total = levelsData.Entries.Count;
        var display = Mathf.Min(resumeIndex+1, total);
        count.SetText($"{display}/{total}");
    }

    /// <summary>
    /// NEW: Update level counter using discovered level IDs (not loaded entries).
    /// Shows progress like "1/177" where 177 is the total discovered levels.
    /// </summary>
    private void UpdateLevelCounterFromDiscovered(TextMeshProUGUI countText, string modeKey)
    {
        var total = _levelsProviderService.GetLevelCount(modeKey);
        if (total <= 0)
        {
            countText.SetText("0/0");
            return;
        }

        // Count solved levels from discovered IDs
        var levelIds = _levelsProviderService.GetLevelIds(modeKey);
        int solvedCount = levelIds.Count(id => GameProgress.Solved.Contains(id));
        int displayIndex = Mathf.Min(solvedCount + 1, total);
        
        countText.SetText($"{displayIndex}/{total}");
    }
    
    private void StartMode(string modeId)
    {
        if (_startingMode) return;
        _startingMode = true;
        SetLoadingIndicators(modeId);

        _levelsProviderService.SetActiveMode(modeId);
        
        switch(modeId){
            case "Default":
                _sceneLoader.LoadSceneSingleAsync(QWERscene).Forget();
                break;
            case "Legacy":
                _sceneLoader.LoadSceneSingleAsync(LegacyScene).Forget();
                break;
            case "Portrait":
                _sceneLoader.LoadSceneSingleAsync(PortraitScene).Forget();
                break;
        }
    }

    private void SetLoadingIndicators(string activeMode)
    {
        if (defaultButton) defaultButton.interactable = false;
        if (legacyButton) legacyButton.interactable = false;
        if (portraitButton) portraitButton.interactable = false;

        if (defaultLoading) defaultLoading.SetActive(string.Equals(activeMode, "Default", System.StringComparison.OrdinalIgnoreCase));
        if (legacyLoading) legacyLoading.SetActive(string.Equals(activeMode, "Legacy", System.StringComparison.OrdinalIgnoreCase));
        if (portraitLoading) portraitLoading.SetActive(string.Equals(activeMode, "Portrait", System.StringComparison.OrdinalIgnoreCase));
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }
    
    void RefreshMenuButtons()
    {
        if (_startingMode) return;

        // Default/QWER
        if (defaultButton) {
            bool unlocked = _progression.IsUnlocked("Default");
            defaultButton.interactable = unlocked && _levelsProviderService.GetLevelCount("Default") > 0;
            // (Optional) show a lock hint label somewhere using _progression.GetLockedHint("Default")
        }
        // Legacy
        if (legacyButton) {
            bool unlocked = _progression.IsUnlocked("Legacy");
            legacyButton.interactable = unlocked && _levelsProviderService.GetLevelCount("Legacy") > 0;
        }
        // Portrait
        if (portraitButton) {
            bool unlocked = _progression.IsUnlocked("Portrait");
            portraitButton.interactable = unlocked && _levelsProviderService.GetLevelCount("Portrait") > 0;
        }

        // Also refresh the counters if you want them live:
        UpdateLevelCounterFromDiscovered(defaultLevelsCountText, "Default");
        UpdateLevelCounterFromDiscovered(legacyLevelsCountText, "Legacy");
        UpdateLevelCounterFromDiscovered(portraitLevelsCountText, "Portrait");
    }
    
    bool AllSolved(string mode) {
        var levelIds = _levelsProviderService.GetLevelIds(mode);
        if (levelIds == null || levelIds.Count == 0) return false;
        // Check if all discovered level IDs are solved
        return levelIds.All(id => GameProgress.Solved.Contains(id));
    }
    string SolveAllHint(string mode) {
        var total = _levelsProviderService.GetLevelCount(mode);
        return $"Solve all {mode} levels ({total} total).";
    }
    
    void OnDestroy()
    {
        if (_progression != null)
        {
            _progression.OnProgressionChanged -= RefreshProgressionUI;
            _progression.OnProgressionChanged -= RefreshMenuButtons;
        }
    }
    
    void RefreshProgressionUI()
    {
        // Keep your existing button `interactable` refresh if you have one,
        // then update the visual arrows/locks:
        foreach (var e in edges)
            UpdateModeEdge(e);
    }

    void UpdateModeEdge(ModeEdgeUI ui)
    {
        bool toUnlocked = _progression.IsUnlocked(ui.toMode);

        // Arrow: dim when locked, full when unlocked (keeps it “visible” as guidance)
        if (ui.arrowImage)
            ui.arrowImage.color = toUnlocked
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.45f);

        // Lock: closed when locked, open when unlocked
        if (ui.lockImage)
        {
            ui.lockImage.sprite = toUnlocked ? lockOpenSprite : lockClosedSprite;
            ui.lockImage.color  = toUnlocked
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.85f);

            // Optional: tooltip/hint if you want to show why it’s locked
            // var hint = _progression.GetLockedHint(ui.toMode);
            // Show hint near lock or as a tooltip if desired.
        }
    }

    private bool IsWindowsModeSelectorEnabled()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer) return true;
        return Application.isEditor && enableWindowsModeSelectorInEditor;
    }
}