using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using DG.Tweening;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(PortraitGameManager),
    context: AppContextType.PortraitScene)]
public class PortraitGameManager : MonoBehaviour, IPortraitGameInitializable
{
    int IPortraitGameInitializable.Order => 1;
    
    [Header("Level UI")]
    public RectTransform artContainerRC;     // parent that holds the 4 skill images
    public Image artContainerImage;     // parent that holds the 4 skill images
    public Image PortraitImage;
    public TextMeshProUGUI levelText;
    LevelsData _levelsData { get; set; }
    private int _currentIdx = -1;
    private ChampionEntry _current;
    
    [Header("Back Button")]
    public Button backButton;

    // Hook your skip button groups in the Inspector:
    [Header("Skip Button UI Groups")]
    public Button skipButton;
    [SerializeField] private GameObject skipCostGroup; // contains key icon + amount text
    [SerializeField] private GameObject skipAdGroup;   // contains play/ad icon

    [Header("Economy")]
    public int unlockCost = 1; // cost per skill unlock
    public int skipKeyCost = 2;
    
    [Header("Windows Keyboard Input")]
    [SerializeField] private bool enableWindowsInputInEditor = false;
    [SerializeField] private GameObject windowsInputRoot;
    [SerializeField] private TMP_InputField windowsInputField;
    [SerializeField] private Button windowsConfirmButton;
    
    [Header("Current Answer FX")]
    public CanvasGroup currentBannerCG;     // alpha 0 at rest
    
    [Header("Wrong Answer FX")]
    public CanvasGroup wrongBannerCG;     // alpha 0 at rest
    public CanvasGroup shutDownBannerCG;     // alpha 0 at rest
    
    [Header("Keyboard Settings")]
    public int keyboardSize = 16;
    public string alphabet = "abcdefghijklmnopqrstuvwxyz";
    private RectTransform _lastKeyRT;
    private readonly List<LetterButton> _keyboardButtons = new();
    public RectTransform keyboardContainer;    // GridLayoutGroup parent
    public LetterButton keyBoardButtonPrefab;
    public CanvasGroup   keyboardCG;
    private string _normalizedTarget = "";
    
    // NEW: which global slot currently has the cursor glow
    [Header("Slot Rows (set in Inspector)")]
    public RectTransform slotsRow1;
    public RectTransform slotsRow2;
    private int _focusSlotIndex = -1;
    private readonly List<SlotView> _slots = new();
    private readonly List<int> _letterSlotIndices = new(); // indices into _slots for Letter kinds
    [Header("Slots")]
    public RectTransform slotsContainer;       // HorizontalLayoutGroup parent
    public GameObject slotPrefab;
    public CanvasGroup   slotsCG;
    [Header("Layout")]
    public float maxSlotSize = 100f;   // target square size
    public float minSlotSize = 56f;    // smallest allowed before wrapping
    public float spaceGapWidth = 24f;
    public float rowHorizontalPadding = 24f;
    public float rowSpacing = 8f;
    public int targetBoxesFirstRowMax = 10;
    public event Action<int> SlotClicked;
    
    [Header("Reveal Roulette Tuning")]
    public Button RevealLetterButton;
    [SerializeField] float rouletteStepFast = 0.06f;
    [SerializeField] float rouletteStepSlow = 0.12f;
    [SerializeField] Vector2Int rouletteSpinSteps = new(18, 28); // random range
    [SerializeField] float landPause = 0.22f;      // small pause after landing
    [SerializeField] float placeDelay = 0.18f;     // delay between placing letters
    bool _revealBusy;

    // Track which slot each button filled, and vice versa
    private readonly Dictionary<LetterButton, int> _buttonToSlot = new();
    private readonly Dictionary<int, LetterButton> _slotToButton = new();

    [Header("Streak System")]
    private int streakTier;
    private CancellationTokenSource _streakCts;

    private bool inputBlocked = false;
    
    [Inject] private ILevelsProviderService _levelsProviderService;
    [Inject] private PopupService _popupService;
    [Inject] private ISceneLoader _sceneLoader;
    [Inject] private ISkipGateService _skipGate;
    [Inject] private StreakSystemView _streakSystemView;
    [Inject] private IAudioService _audioService;
    [Inject] private IAdsService _ads;
    [Inject] private IInterstitialPacingService _interPacing;
    [Inject] private IRewardFX _fx;
    [Inject] private IProgressionService _progressionService;
    
    [Header("Loading UI (Optional)")]
    [SerializeField] private LevelLoadingUI _loadingUI;
    
    async UniTask IPortraitGameInitializable.Initialize()
    {
        // Ensure discovery is done (should already be done in preloader)
        if (!_levelsProviderService.Initialized)
            await _levelsProviderService.DiscoverLevelsOnlyAsync();

        var mode = _levelsProviderService.ActiveModeKey;

        // Check if all solved using discovered IDs
        var levelIds = _levelsProviderService.GetLevelIds(mode);
        bool allSolvedNow = levelIds.Count > 0 && levelIds.All(id => GameProgress.Solved.Contains(id));
        if (allSolvedNow) ModeCompletionStore.MarkCompleted(mode);
        else if (ModeCompletionStore.IsCompleted(mode)) ModeCompletionStore.ClearCompleted(mode);

        // If finished, show the congrats popup once per completion
        if (ModeCompletionStore.TryConsumeCongratsOnce(mode))
        {
            await _popupService.ShowAsync(PopupPresets.Victory(), "Gameplay");
            return;
        }

        // Get next level ID to load
        string nextLevelId = _levelsProviderService.GetNextUnsolvedLevelId(mode);
        if (string.IsNullOrEmpty(nextLevelId))
        {
            Debug.LogWarning("No unsolved levels found; showing victory.");
            await _popupService.ShowAsync(PopupPresets.Victory(), "Gameplay");
            return;
        }

        StreakSystem.Reset();
        _streakSystemView.HideStreakTimer();

        if (skipButton)
        {
            skipButton.onClick.AddListener(OnSkipClicked);
            CurrencyStore.OnKeysChanged += HandleKeysChanged;
        }

        if (RevealLetterButton)
        {
            RevealLetterButton.onClick.AddListener(() =>
                RevealTwoLettersWithRouletteAsync(
                    lettersToReveal: 2,
                    scanCycles: 12,
                    scanStepMs: 80,
                    settleMs: 200,
                    betweenLettersMs: 180
                ).Forget());
        }

        if (backButton) backButton.onClick.AddListener(OnBackClicked);

        SlotClicked += OnSlotClicked;
        
        SetupWindowsInput();

        // Check if level is already downloaded (prefetched)
        bool isAlreadyDownloaded = _levelsProviderService.IsLevelDownloaded(nextLevelId, mode);
        bool shouldShowLoadingUI = !isAlreadyDownloaded || !_levelsProviderService.hideLoadingUIForPrefetchedLevels;

        // Prefetch the next unsolved level in background while player solves the first one
        PrefetchNextUnsolved(nextLevelId, mode);

        // Show loading UI only if level is not prefetched
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Show("Loading level...");
        else if (_loadingUI != null)
            _loadingUI.Hide();

        // Subscribe to progress events for loading UI updates
        var progressHandler = new Action<long, long, float>((downloaded, total, pct) =>
        {
            if (shouldShowLoadingUI && _loadingUI != null)
                _loadingUI.UpdateProgress(pct, $"Downloading... {(int)(pct * 100)}%");
        });
        _levelsProviderService.OnDownloadProgress += progressHandler;

        var assetProgressHandler = new Action<int, int, float>((loaded, total, pct) =>
        {
            if (shouldShowLoadingUI && _loadingUI != null)
                _loadingUI.UpdateProgress(pct, $"Loading... {(int)(pct * 100)}%");
        });
        _levelsProviderService.OnAssetsLoadProgress += assetProgressHandler;

        // Load first level (download + load asset)
        _current = await _levelsProviderService.LoadSingleLevelAsync(nextLevelId, mode);

        // Unsubscribe
        _levelsProviderService.OnDownloadProgress -= progressHandler;
        _levelsProviderService.OnAssetsLoadProgress -= assetProgressHandler;

        // Hide loading UI
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Hide();

        if (_current == null)
        {
            Debug.LogError("Failed to load first level; returning to Menu.");
            await _popupService.ShowAsync(PopupPresets.NoLevels(), "Gameplay");
            _sceneLoader.LoadSceneSingleAsync("scenes/MenuScene").Forget();
            return;
        }

        // Setup UI for loaded level
        SetupLevel(_current);
        UpdateLevelCounter();
        _audioService.PlaySfx(SfxId.StartGame);
        RefreshSkipButtonUI();
    }
    
    private void OnSlotClicked(int slotIndex)
    {
        // If this slot was filled by some key, restore that key
        if (_slotToButton.TryGetValue(slotIndex, out var btn))
        {
            // Clear the slot visually
            ClearSlotAt(slotIndex);

            // Restore the key
            btn.SetSelected(false);
            btn.SetHidden(false);

            // Remove mappings
            _slotToButton.Remove(slotIndex);
            _buttonToSlot.Remove(btn);

            _audioService.PlaySfx(SfxId.SlotRemove);
            Debug.Log("removing letter and taking it back to the keyboard container");
        }
        else
        {
            Debug.Log("Slot is empty, nothing happens");
            // Slot empty: optional feedback
        }
    }
    
    private void UpdateLevelCounter()
    {
        var mode = _levelsProviderService.ActiveModeKey;
        int total = _levelsProviderService.GetLevelCount(mode);
        
        if (total <= 0)
        {
            SetLevelText("0/0");
            return;
        }

        // Count solved levels from discovered IDs
        var levelIds = _levelsProviderService.GetLevelIds(mode);
        int solvedCount = levelIds.Count(id => GameProgress.Solved.Contains(id));
        int display = Mathf.Min(solvedCount + 1, total);
        
        SetLevelText($"{display}/{total}");
    }
    
    public void SetLevelText(string txt)
    {
        levelText.SetText(txt);
    }
    
    void ClearSlots()
    {
        void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
        if (slotsRow1) Clear(slotsRow1);
        if (slotsRow2) Clear(slotsRow2);

        _slots.Clear();
        _letterSlotIndices.Clear();
    }
    
    private void OnSkipClicked()
    {
        // Guard: if only one unsolved remains, do nothing (your logic)
        var mode = _levelsProviderService.ActiveModeKey;
        var levelIds = _levelsProviderService.GetLevelIds(mode);
        int unsolvedCount = levelIds.Count(id => !GameProgress.Solved.Contains(id));
        if (unsolvedCount <= 1) return;

        var gate = _skipGate.GetGate(CurrencyStore.Keys);
        if (gate == SkipGate.Keys)
        {
            if (!CurrencyStore.TrySpendKeys(skipKeyCost))
            {
                // no keys at the time of click -> fall back to rewarded path
                TrySkipWithRewarded().Forget();
                return;
            }

            ExecuteSkipAndRecord().Forget();
            return;
        }

        // Rewarded-gated
        TrySkipWithRewarded().Forget();
    }
    
    private async UniTask ExecuteSkipAndRecord()
    {
        await ExecuteSkip();        // your existing skip implementation (move level, clear maps, load next…)
        _skipGate.RecordSkip(); // <- advance the alternating rule
        RefreshSkipButtonUI();  // reflect new state (next skip may be ad-gated)
    }
    
    private async UniTask ExecuteSkip()
    {
        StreakSystem.Reset();
        _streakSystemView.StopStreakTimerLoop();

        // Unload current level
        if (_current != null)
        {
            _levelsProviderService.UnloadCurrentLevel();
        }

        // Clear mappings
        foreach (var kv in _buttonToSlot) kv.Key.SetSelected(false);
        _buttonToSlot.Clear();
        _slotToButton.Clear();

        // Get next level ID
        var modeKey = _levelsProviderService.ActiveModeKey;
        string nextLevelId = _levelsProviderService.GetNextUnsolvedLevelId(modeKey);

        if (string.IsNullOrEmpty(nextLevelId))
        {
            Debug.LogWarning("No unsolved levels after skip.");
            return;
        }

        // Check if level is already downloaded (prefetched)
        bool isAlreadyDownloaded = _levelsProviderService.IsLevelDownloaded(nextLevelId, modeKey);
        bool shouldShowLoadingUI = !isAlreadyDownloaded || !_levelsProviderService.hideLoadingUIForPrefetchedLevels;

        // Show loading UI only if level is not prefetched
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Show("Loading level...");
        else if (_loadingUI != null)
            _loadingUI.Hide();

        // Subscribe to progress events
        var progressHandler = new Action<long, long, float>((downloaded, total, pct) =>
        {
            if (shouldShowLoadingUI && _loadingUI != null)
                _loadingUI.UpdateProgress(pct, $"Downloading... {(int)(pct * 100)}%");
        });
        _levelsProviderService.OnDownloadProgress += progressHandler;

        var assetProgressHandler = new Action<int, int, float>((loaded, total, pct) =>
        {
            if (shouldShowLoadingUI && _loadingUI != null)
                _loadingUI.UpdateProgress(pct, $"Loading... {(int)(pct * 100)}%");
        });
        _levelsProviderService.OnAssetsLoadProgress += assetProgressHandler;

        // Load next level
        _current = await _levelsProviderService.LoadSingleLevelAsync(nextLevelId, modeKey);

        // Unsubscribe
        _levelsProviderService.OnDownloadProgress -= progressHandler;
        _levelsProviderService.OnAssetsLoadProgress -= assetProgressHandler;

        // Hide loading UI
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Hide();

        if (_current == null)
        {
            Debug.LogError($"Failed to load level after skip: {nextLevelId}");
            return;
        }

        // Prefetch the next unsolved level in background while player solves this one
        PrefetchNextUnsolved(nextLevelId, modeKey);

        // Setup UI
        SetupLevel(_current);
        UpdateLevelCounter();
        RefreshSkipButtonUI();
    }

    /// <summary>
    /// Find the next unsolved level after currentLevelId (excluding it) and prefetch it silently.
    /// Uses PrefetchLevelAsync directly to avoid cancelling any in-progress downloads.
    /// </summary>
    private void PrefetchNextUnsolved(string currentLevelId, string mode)
    {
        var ids = _levelsProviderService.GetLevelIds(mode);
        int idx = ids.FindIndex(id => string.Equals(id, currentLevelId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        for (int i = 1; i < ids.Count; i++)
        {
            var id = ids[(idx + i) % ids.Count];
            if (string.Equals(id, currentLevelId, StringComparison.OrdinalIgnoreCase)) continue;
            if (GameProgress.Solved.Contains(id)) continue;

            _levelsProviderService.PrefetchLevelAsync(id, mode).Forget();
            return;
        }
    }
    
    private async UniTaskVoid TrySkipWithRewarded()
    {
        if (!_ads.IsRewardedReady)
            await _ads.PreloadRewardedAsync();

        bool finished = await _ads.ShowRewardedAsync(onReward: () =>
        {
            ExecuteSkipAndRecord().Forget();
        });

        if (!finished)
            Debug.LogWarning("[Skip] Rewarded ad not completed.");

        RefreshSkipButtonUI(); // update UI state after attempt
    }
    
    private void RefreshSkipButtonUI()
    {
        var gate = _skipGate.GetGate(CurrencyStore.Keys);
        bool useAd = (gate == SkipGate.Rewarded);

        if (skipCostGroup) skipCostGroup.SetActive(!useAd);
        if (skipAdGroup) skipAdGroup.SetActive(useAd);

        bool interactable = true;
        if (useAd)
        {
            // needs ads
            interactable = _ads is { IsRewardedReady: true };
        }
        else
        {
            // needs keys
            interactable = CurrencyStore.Keys >= skipKeyCost;
        }

        if (skipButton) skipButton.interactable = interactable;
    }
    
    void SetInputEnabled(bool on)
    {
        inputBlocked = on;
        if (keyboardCG) { keyboardCG.blocksRaycasts = on; }
        if (slotsCG)    { slotsCG.blocksRaycasts    = on; }
    }
    
    int GetLetterSlotCount() => _letterSlotIndices.Count;
    IReadOnlyList<LetterButton> GetKeyboardButtons() => _keyboardButtons;
    bool IsLetterSlotEmptyAtPos(int pos)
    {
        if (pos < 0 || pos >= _letterSlotIndices.Count) return false;
        var gi = _letterSlotIndices[pos];
        return gi >= 0 && gi < _slots.Count && _slots[gi].IsEmptyLetter;
    }
    char GetTargetLetterAtPos(int pos)
    {
        if (string.IsNullOrEmpty(_normalizedTarget) || pos < 0 || pos >= _normalizedTarget.Length) return '\0';
        return _normalizedTarget[pos];
    }
    int GetGlobalSlotIndexForPos(int pos)
    {
        if (pos < 0 || pos >= _letterSlotIndices.Count) return -1;
        return _letterSlotIndices[pos];
    }
    
    /// Call this from your “Reveal 2 Letters” button
    async UniTask RevealTwoLettersWithRouletteAsync(
        int lettersToReveal = 2,
        int scanCycles = 12,
        int scanStepMs = 80,
        int settleMs = 200,
        int betweenLettersMs = 180)
    {
        if (_revealBusy || _current == null) return;

        _revealBusy = true;
        SetInputEnabled(false);

        try
        {
            int totalPos = GetLetterSlotCount();
            if (totalPos <= 0) return;

            // --- Build fillable candidates (left-to-right) ---
            var freeByLetter = new Dictionary<char, List<LetterButton>>();
            foreach (var kb in GetKeyboardButtons())
            {
                if (kb == null || !kb.gameObject.activeInHierarchy) continue;
                if (_buttonToSlot.ContainsKey(kb)) continue; // already used/hidden

                var ltr = kb.ExtractLetter();
                if (ltr == '\0') continue;
                ltr = char.ToUpperInvariant(ltr);

                if (!freeByLetter.TryGetValue(ltr, out var list))
                    freeByLetter[ltr] = list = new List<LetterButton>();
                list.Add(kb);
            }

            // collect all empty slot positions, then shuffle them
            var emptyPositions = new List<int>();
            for (int pos = 0; pos < totalPos; pos++)
                if (IsLetterSlotEmptyAtPos(pos))
                    emptyPositions.Add(pos);

            // Fisher–Yates shuffle
            for (int i = 0; i < emptyPositions.Count; i++)
            {
                int j = Random.Range(i, emptyPositions.Count);
                (emptyPositions[i], emptyPositions[j]) = (emptyPositions[j], emptyPositions[i]);
            }

            var candidates = new List<(int pos, char L, LetterButton key)>();
            foreach (var pos in emptyPositions)
            {
                char L = char.ToUpperInvariant(GetTargetLetterAtPos(pos));
                if (L == '\0') continue;

                if (!freeByLetter.TryGetValue(L, out var keys) || keys.Count == 0)
                    continue; // no free key that matches this letter

                var key = keys[0];     // take one matching key
                keys.RemoveAt(0);      // consume it so we don't reuse the same key twice
                candidates.Add((pos, L, key));

                if (candidates.Count >= lettersToReveal)
                    break; // we have enough
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[RevealTwoLetters] No fillable positions found (no free matching keys).");
                SetInputEnabled(true);
                _revealBusy = false;
                return;
            }

            // 🔑 COST: spend 1 key up front (block if none)
            if (!CurrencyStore.TrySpendKeys(1))
            {
                // Optional popup if you want:
                // _popupService?.ShowAsync(PopupPresets.NotEnoughKeys(1, CurrencyStore.Keys), "Gameplay").Forget();
                return;
            }

            // --- Roulette scan across currently free keys ---
            var scanKeys = new List<LetterButton>();
            // (Note: original snippet left scanKeys empty; keeping that behavior.)

            if (scanKeys.Count > 0)
            {
                int idx = Random.Range(0, scanKeys.Count);
                for (int c = 0; c < scanCycles; c++)
                {
                    var btn = scanKeys[idx];
                    btn.PulseKey(0.08f, keepGlow: false); // brief glow while pulsing
                    _audioService.PlaySfx(SfxId.Tick);
                    idx = (idx + 1) % scanKeys.Count;
                    await UniTask.Delay(scanStepMs);
                }
            }

            // Small pause after “choosing”
            await UniTask.Delay(settleMs);

            // --- Place letters one-by-one with delay ---
            for (int i = 0; i < candidates.Count; i++)
            {
                var (pos, L, key) = candidates[i];

                // mini roulette that lands on the actual key (keep glow ON)
                await MiniRouletteTowardsKey(key, scanKeys, ticks: 6, stepMs: 70);

                // If slot had a wrong key, clear & return it first
                int gi = GetGlobalSlotIndexForPos(pos);
                if (gi >= 0 && _slotToButton.TryGetValue(gi, out var priorBtn) && priorBtn)
                {
                    ClearSlotAt(gi);
                    priorBtn.SetSelected(false);
                    priorBtn.SetHidden(false);
                    _slotToButton.Remove(gi);
                    _buttonToSlot.Remove(priorBtn);
                }
                else
                {
                    // focus the slot we’re about to fill
                    ClearLetterSlotAtPos(pos);
                }

                // punch + keep glow during the press
                var rt = key.GetComponent<RectTransform>();
                if (rt) rt.DOPunchScale(Vector3.one * 0.09f, 0.12f, 12, 0.95f);
                key.SetKeyGlow(true);

                // Simulate a real press so your mapping stays correct
                OnKeyboardLetterPressed(key.Letter, key);

                // after it’s consumed/hidden, ensure glow is off
                key.SetKeyGlow(false);

                await UniTask.Delay(betweenLettersMs);
            }
        }
        finally
        {
            SetInputEnabled(true);
            _revealBusy = false;
        }

        // --- local helpers ---
        async UniTask MiniRouletteTowardsKey(LetterButton target, List<LetterButton> pool, int ticks, int stepMs)
        {
            if (target == null) return;
            if (pool == null || pool.Count == 0) pool = new List<LetterButton> { target };

            for (int i = 0; i < ticks - 1; i++)
            {
                var b = pool[Random.Range(0, pool.Count)];
                b.PulseKey(0.07f, keepGlow: false);
                _audioService.PlaySfx(SfxId.Tick);
                await UniTask.Delay(stepMs);
            }

            // land on the target (keep glow ON)
            target.PulseKey(0.11f, keepGlow: true);
            _audioService.PlaySfx(SfxId.Tick);
            await UniTask.Delay(stepMs);
        }
    }
    
    void ClearLetterSlotAtPos(int pos)
    {
        var gi = GetGlobalSlotIndexForPos(pos);
        if (gi >= 0) ClearSlotAt(gi);
    }
    
    void ClearSlotAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;
        var sv = _slots[slotIndex];
        if (sv.kind != SlotKind.Letter) return;

        sv.ClearLetter();
        _focusSlotIndex = slotIndex; // focus the just-cleared slot
        UpdateGlow();
    }
    
    void OnBackClicked() => LoadMenu().Forget();

    private async UniTaskVoid LoadMenu()
    {
        await _sceneLoader.LoadSceneSingleAsync("scenes/MenuScene");
    }
    
    /// <summary>
    /// NEW: Setup level UI with loaded entry (replaces old LoadLevelAt).
    /// </summary>
    private void SetupLevel(ChampionEntry entry)
    {
        if (entry == null) return;

        // Clear maps
        foreach (var kv in _buttonToSlot) kv.Key.SetSelected(false);
        _buttonToSlot.Clear();
        _slotToButton.Clear();

        var mode = _levelsProviderService.ActiveModeKey;
        _levelsProviderService.SaveLastCurrent(entry.id, mode);

        if (entry.portrait) SetPortrait(entry.portrait);
        
        BuildSlotsForAnswer(entry.displayName);
        SetNormalizedAnswer(entry.normalizedName);

        BuildKeyboardForAnswer(entry.normalizedName);
        ResetWindowsInput();
    }
    
    void SetPortrait(Sprite image)
    {
        if (PortraitImage)
        {
            PortraitImage.enabled = true;
            PortraitImage.sprite = image;
        }
    }
    
    private List<Token> Tokenize(string displayName)
    {
        var list = new List<Token>(displayName.Length);
        foreach (char ch in displayName)
        {
            if (ch == ' ')
                list.Add(new Token { kind = SlotKind.Spacer });
            else if (ch == '.' || ch == '\'' || ch == '&')   // <--- added ampersand
                list.Add(new Token { kind = SlotKind.FixedChar, ch = ch });
            else
                list.Add(new Token { kind = SlotKind.Letter });
        }
        return list;
    }
    
    int CountLetters(List<Token> tks)
    {
        int n = 0;
        for (int i = 0; i < tks.Count; i++)
            if (tks[i].kind == SlotKind.Letter) n++;
        return n;
    }
    
    private bool TryComputeSingleLineSize(RectTransform row, List<Token> tokens, out float slotSize)
    {
        slotSize = maxSlotSize;

        if (row) LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        float available = row ? Mathf.Max(0f, row.rect.width - 2f * rowHorizontalPadding) : 0f;

        int boxes=0, spacers=0;
        foreach (var t in tokens) { if (t.kind == SlotKind.Spacer) spacers++; else boxes++; }

        float totalSpacing = Mathf.Max(0, (tokens.Count - 1)) * rowSpacing;
        float spacersWidth = spacers * spaceGapWidth;

        // same math as before, but with size instead of width
        float minTotal = boxes * minSlotSize + spacersWidth + totalSpacing;
        if (available <= 0f || minTotal > available) { slotSize = minSlotSize; return false; }

        float neededAtMax = boxes * maxSlotSize + spacersWidth + totalSpacing;
        if (neededAtMax <= available) { slotSize = maxSlotSize; return true; }

        float freeForBoxes = Mathf.Max(0f, available - spacersWidth - totalSpacing);
        slotSize = Mathf.Clamp(freeForBoxes / Mathf.Max(1, boxes), minSlotSize, maxSlotSize);
        return true;
    }
    
    private void BuildRow(RectTransform row, List<Token> tokens, float slotSize)
    {
        int baseIndex = _slots.Count;

        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            var go = Instantiate(slotPrefab, row);
            var sv = go.GetComponent<SlotView>();
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.flexibleWidth = 0f;  // don’t let layout squeeze
            le.flexibleHeight = 0f;

            if (t.kind == SlotKind.Spacer)
            {
                sv.InitSpacer(spaceGapWidth);
                le.preferredWidth = le.minWidth = spaceGapWidth;
                le.preferredHeight = le.minHeight = slotSize;   // keep row height consistent
                var btn = go.GetComponent<Button>(); if (btn) btn.onClick.RemoveAllListeners();
            }
            else if (t.kind == SlotKind.FixedChar)
            {
                sv.InitFixed(t.ch);
                le.preferredWidth  = le.minWidth  = slotSize;
                le.preferredHeight = le.minHeight = slotSize;    // ← square
            }
            else // Letter
            {
                int globalIndex = baseIndex + i;
                var idx = globalIndex;                    // capture to avoid closure bug
                sv.InitLetter(() => SlotClicked?.Invoke(idx));
                le.preferredWidth  = le.minWidth  = slotSize;    // ← square
                le.preferredHeight = le.minHeight = slotSize;    // ← square
                _letterSlotIndices.Add(globalIndex);
            }

            _slots.Add(sv);
        }
    }
    
    void BuildSlotsForAnswer(string displayName)
    {
        ClearSlots();

        Canvas.ForceUpdateCanvases();
        if (slotsRow1) LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRow1);
        if (slotsRow2) LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRow2);

        var tokens = Tokenize(displayName);

        // Build normalized target (letters only, in the order we push Letter slots)
        System.Text.StringBuilder nb = new();
        foreach (var t in tokens)
            if (t.kind == SlotKind.Letter) nb.Append(' '); // reserve length; letters will be filled after BuildRow

        _normalizedTarget = ""; // temp; we’ll fix it after rows are built

        int letters = CountLetters(tokens);
        
        // Count "boxes" (Letter + FixedChar). Spacers (spaces) don't count.
        int boxCount = 0;
        for (int i = 0; i < tokens.Count; i++)
            if (tokens[i].kind != SlotKind.Spacer) boxCount++;
        
        if (letters <= 12)
        {
            float size;
            if (!TryComputeSingleLineSize(slotsRow1, tokens, out size))
                size = minSlotSize;
            if (slotsRow2) slotsRow2.gameObject.SetActive(false);
            BuildRow(slotsRow1, tokens, size);
            
            Canvas.ForceUpdateCanvases();
            if (slotsRow1) LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRow1);
            
            FocusFirstEmpty();
            return;
        }

        // --- otherwise use two rows (target ~10 boxes on first row) ---
        float _;
        int breakIndex = FindWrapIndexLetters(tokens, slotsRow1, targetLettersFirstRow: targetBoxesFirstRowMax);

        if (slotsRow2) slotsRow2.gameObject.SetActive(true);

        var firstLine  = tokens.GetRange(0, breakIndex);
        var secondLine = tokens.GetRange(breakIndex, tokens.Count - breakIndex);

        float s1, s2;
        if (!TryComputeSingleLineSize(slotsRow1, firstLine, out s1)) s1 = minSlotSize;
        if (!TryComputeSingleLineSize(slotsRow2, secondLine, out s2)) s2 = minSlotSize;

        BuildRow(slotsRow1, firstLine,  s1);
        BuildRow(slotsRow2, secondLine, s2);
        
        // Reconstruct normalized (letters only) from original displayName preserving letter order
        var plainLetters = new List<char>();
        foreach (char ch in displayName)
            if (char.IsLetter(ch)) plainLetters.Add(char.ToUpperInvariant(ch));

        // Ensure counts align with the number of letter slots
        if (plainLetters.Count == _letterSlotIndices.Count)
            _normalizedTarget = new string(plainLetters.ToArray());
        else
            _normalizedTarget = new string(plainLetters.ToArray()); // safe fallback
        
        Canvas.ForceUpdateCanvases();
        if (slotsRow1) LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRow1);
        if (slotsRow2 && slotsRow2.gameObject.activeSelf) LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRow2);
        
        FocusFirstEmpty();
    }
    
    private int FindWrapIndexLetters(List<Token> tokens, RectTransform rowForFirstLine, int targetLettersFirstRow)
    {
        // collect spacers
        List<int> spacers = new();
        for (int i = 0; i < tokens.Count; i++)
            if (tokens[i].kind == SlotKind.Spacer) spacers.Add(i);

        // count letters up to endExclusive
        int LettersUpTo(int endExclusive)
        {
            int c = 0;
            for (int i = 0; i < endExclusive; i++)
                if (tokens[i].kind == SlotKind.Letter) c++;
            return c;
        }

        // prefer a space that yields <= target letters on line 1 and actually fits
        int bestIdx = -1;
        int bestLetters = -1;
        foreach (var sp in spacers)
        {
            int letters = LettersUpTo(sp);
            if (letters > targetLettersFirstRow || letters <= bestLetters) continue;
            var first = tokens.GetRange(0, sp);
            float _;
            if (!TryComputeSingleLineSize(slotsRow1, first, out _)) continue;
            bestLetters = letters;
            bestIdx = sp;
        }
        if (bestIdx >= 0) return bestIdx + 1;

        // no suitable space → hard split after targetLettersFirstRow letters
        int lettersSoFar = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].kind == SlotKind.Letter) lettersSoFar++;
            if (lettersSoFar >= targetLettersFirstRow)
                return Mathf.Min(i + 1, tokens.Count);
        }
        return tokens.Count;
    }
    
    private void FocusFirstEmpty()
    {
        _focusSlotIndex = -1;
        foreach (var si in _letterSlotIndices)
        {
            if (_slots[si].IsEmptyLetter)
            {
                _focusSlotIndex = si;
                break;
            }
        }
        UpdateGlow();
    }
    
    void SetNormalizedAnswer(string normalized)
    {
        _normalizedTarget = string.IsNullOrWhiteSpace(normalized) ? "" : normalized.ToUpperInvariant();
    }
    
    private void BuildKeyboardForAnswer(string normalizedAnswer)
    {
        var needed = new List<char>(normalizedAnswer.ToCharArray());
        var pool = new List<char>(needed);

        while (pool.Count < keyboardSize)
        {
            var c = alphabet[Random.Range(0, alphabet.Length)];
            pool.Add(c);
        }

        // Shuffle
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        BuildKeyboard(pool, OnKeyboardLetterPressed);
    }
    
    void BuildKeyboardWithStagger(List<char> pool, Action<char, LetterButton> onKey)
    {
        ClearKeyboard();
        if (keyboardCG) keyboardCG.alpha = 0f;
        BuildKeyboard(pool, onKey); // your existing builder
        if (keyboardCG) keyboardCG.DOFade(1f, 0.2f).SetDelay(0.1f);

        for (int i = 0; i < keyboardContainer.childCount; i++)
        {
            var t = keyboardContainer.GetChild(i) as RectTransform;
            if (!t) continue;
            
            float d = 0.02f * i + 0.1f;
            t.localScale = Vector3.one * 0.85f;
            t.DOScale(1f, 0.12f).SetDelay(d).SetEase(Ease.OutBack);
            DOVirtual.DelayedCall(d, () => _audioService.PlaySfx(SfxId.Tick), ignoreTimeScale: true);
        }
    }
    
    void ClearKeyboard()
    {
        _keyboardButtons.Clear();
        for (int i = keyboardContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(keyboardContainer.GetChild(i).gameObject);
    }
    
    void BuildKeyboard(List<char> letters, Action<char, LetterButton> onPressed)
    {
        ClearKeyboard();
        _keyboardButtons.Clear();
        foreach (var c in letters)
        {
            var b = Instantiate(keyBoardButtonPrefab, keyboardContainer);
            b.Init(c, (ch, btn) => onPressed?.Invoke(ch, btn));
            _keyboardButtons.Add(b);
        }
    }
    
    private void OnKeyboardLetterPressed(char c, LetterButton btn)
    {
        // NEW: no toggle. Always try to consume this key.
        _lastKeyRT = btn ? btn.GetComponent<RectTransform>() : null;

        var filledIndex = FillNextEmpty(c);
        if (filledIndex >= 0)
        {
            // Hide the key
            btn.SetSelected(true);
            btn.SetHidden(true);

            // Remember mappings
            _buttonToSlot[btn] = filledIndex;
            _slotToButton[filledIndex] = btn;

            // Check answer
            var guessRaw = ReadCurrentGuess();
            if (!string.IsNullOrEmpty(guessRaw))
            {
                var guessNormalized = StringUtils.NormalizeAnswer(guessRaw);
                var answerNorm = _current.normalizedName;

                if (guessNormalized == answerNorm)
                {
                    GameProgress.MarkSolved(_current.id);
                    _progressionService.NotifyStateMaybeChanged();
                    //SetStatus($"Correct! It was {_current.displayName}.");
                    Debug.Log($"Correct! It was {_current.displayName}.");
                    HandleCorrectAndAdvanceAsync().Forget();
                }
                else
                {
                    // all letters filled but wrong?
                    // We use normalized lengths to avoid spaces/accents issues.
                    bool allFilled = guessNormalized.Length == answerNorm.Length;
                    if (allFilled)
                    {
                        HandleWrongGuessAsync().Forget();
                    }
                }
            }

            _audioService.PlaySfx(SfxId.LetterClick);
        }
        // If no empty slot, you can give feedback (shake).
    }
    
    private async UniTaskVoid HandleWrongGuessAsync()
    {
        // 1) Small wrong FX (shake rows + optional banner) – lives in LevelUI
            await PlayWrongGuessFXAsync(false);

        // 2) Restore all keys and clear all filled letter slots
        // Copy pairs first to avoid modifying the dictionary while iterating.
        var snapshot = new List<KeyValuePair<int, LetterButton>>(_slotToButton);
        foreach (var kv in snapshot)
        {
            int slotIndex = kv.Key;
            var btn = kv.Value;

            // clear slot
            ClearSlotAt(slotIndex);

            // return the key to keyboard
            if (btn != null)
            {
                btn.SetSelected(false);
                btn.SetHidden(false);
            }

            // remove mappings
            _slotToButton.Remove(slotIndex);
            if (btn != null) _buttonToSlot.Remove(btn);
        }

        // NEW: put the caret/glow back on the first empty slot
        FocusFirstEmptySlot();

        StreakSystem.Reset();
        _streakSystemView.StopStreakTimerLoop();
    }
    
    async UniTask PlayWrongGuessFXAsync(bool isShutDown)
    {
        // play a short “wrong” sound if provided
        _audioService.PlaySfx(isShutDown ? SfxId.ShutDown : SfxId.WrongAnswer);

        // shake whichever rows are active
        if (slotsRow1)
        {
            slotsRow1.DOKill(true);
            // Shake only X (horizontal) and return to base
            var basePos = slotsRow1.anchoredPosition;
            await slotsRow1.DOShakeAnchorPos(0.25f, new Vector2(16f, 0f), 25, 90f, false, true)
                .SetUpdate(true)
                .AsyncWaitForCompletion();
            slotsRow1.anchoredPosition = basePos;
        }
        if (slotsRow2 && slotsRow2.gameObject.activeSelf)
        {
            slotsRow2.DOKill(true);
            var basePos2 = slotsRow2.anchoredPosition;
            await slotsRow2.DOShakeAnchorPos(0.25f, new Vector2(16f, 0f), 25, 90f, false, true)
                .SetUpdate(true)
                .AsyncWaitForCompletion();
            slotsRow2.anchoredPosition = basePos2;
        }
        
        if (isShutDown)
        {
            if (shutDownBannerCG)
            {
                shutDownBannerCG.gameObject.SetActive(true);
                shutDownBannerCG.alpha = 0f;
                shutDownBannerCG.transform.localScale = Vector3.one * 0.9f;

                var seq = DOTween.Sequence().SetUpdate(true)
                    .Append(shutDownBannerCG.DOFade(1f, 0.12f))
                    .Join(shutDownBannerCG.transform.DOScale(1.05f, 0.12f))
                    .AppendInterval(0.25f)
                    .Append(shutDownBannerCG.DOFade(0f, 0.15f))
                    .Join(shutDownBannerCG.transform.DOScale(1f, 0.15f))
                    .OnComplete(() => shutDownBannerCG.gameObject.SetActive(false));

                await seq.AsyncWaitForCompletion();
            }
        }
        else
        {
            if (wrongBannerCG)
            {
                wrongBannerCG.gameObject.SetActive(true);
                wrongBannerCG.alpha = 0f;
                wrongBannerCG.transform.localScale = Vector3.one * 0.9f;

                var seq = DOTween.Sequence().SetUpdate(true)
                    .Append(wrongBannerCG.DOFade(1f, 0.12f))
                    .Join(wrongBannerCG.transform.DOScale(1.05f, 0.12f))
                    .AppendInterval(0.25f)
                    .Append(wrongBannerCG.DOFade(0f, 0.15f))
                    .Join(wrongBannerCG.transform.DOScale(1f, 0.15f))
                    .OnComplete(() => wrongBannerCG.gameObject.SetActive(false));

                await seq.AsyncWaitForCompletion();
            }
        }
    }
    
    void FocusFirstEmptySlot()
    {
        // reuse the existing logic
        FocusFirstEmpty();
    }
    
    void HideSlotsImmediate()
    {
        if (slotsCG) slotsCG.alpha = 0f;
    }
    
    async UniTask ShowSlotsStaggerAsync()
    {
        if (!slotsCG) return;
        slotsCG.alpha = 0f;
        await slotsCG.DOFade(1f, 0.2f).AsyncWaitForCompletion();

        for (int i = 0; i < slotsContainer.childCount; i++)
        {
            var t = slotsContainer.GetChild(i) as RectTransform;
            if (!t) continue;
            
            float d = 0.02f * i + 0.1f;
            t.localScale = Vector3.one * 0.85f;
            t.DOScale(1f, 0.12f).SetDelay(d).SetEase(Ease.OutBack);
            DOVirtual.DelayedCall(d, () => _audioService.PlaySfx(SfxId.Tick), ignoreTimeScale: true);
        }
        await UniTask.Delay(TimeSpan.FromMilliseconds(180));
    }
    
    private async UniTask HandleCorrectAndAdvanceAsync()
    {
        SetInputEnabled(false);
        StreakSystem.ClearWindow();
        _streakSystemView.StopStreakTimerLoop();

        await DoWinFXAndRewardsAsync();

        // Unload current level
        var currentLevelId = _current?.id;
        _levelsProviderService.UnloadCurrentLevel();

        // Get next level ID
        var mode = _levelsProviderService.ActiveModeKey;
        string nextLevelId = _levelsProviderService.GetNextUnsolvedLevelId(mode);

        if (string.IsNullOrEmpty(nextLevelId))
        {
            // All solved
            ModeCompletionStore.MarkCompleted(mode);
            _progressionService?.NotifyStateMaybeChanged();
            await _popupService.ShowAsync(PopupPresets.Victory(), "Gameplay");
            return;
        }

        await FlipOutAsync();
        UpdateLevelCounter();

        // Check if level is already downloaded (prefetched)
        bool isAlreadyDownloaded = _levelsProviderService.IsLevelDownloaded(nextLevelId, mode);
        bool shouldShowLoadingUI = !isAlreadyDownloaded || !_levelsProviderService.hideLoadingUIForPrefetchedLevels;

        // Show loading UI only if level is not prefetched
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Show("Loading next level...");
        else if (_loadingUI != null)
            _loadingUI.Hide();

        // Subscribe to progress events
        var progressHandler = new Action<long, long, float>((downloaded, total, pct) =>
        {
            if (shouldShowLoadingUI && _loadingUI != null)
                _loadingUI.UpdateProgress(pct, $"Downloading... {(int)(pct * 100)}%");
        });
        _levelsProviderService.OnDownloadProgress += progressHandler;

        var assetProgressHandler = new Action<int, int, float>((loaded, total, pct) =>
        {
            if (shouldShowLoadingUI && _loadingUI != null)
                _loadingUI.UpdateProgress(pct, $"Loading... {(int)(pct * 100)}%");
        });
        _levelsProviderService.OnAssetsLoadProgress += assetProgressHandler;

        // Load next level (download + load asset)
        // If already prefetched, download phase will be instant
        _current = await _levelsProviderService.LoadSingleLevelAsync(nextLevelId, mode);

        // Unsubscribe
        _levelsProviderService.OnDownloadProgress -= progressHandler;
        _levelsProviderService.OnAssetsLoadProgress -= assetProgressHandler;

        // Hide loading UI
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Hide();

        // Prefetch the next unsolved level in background while player solves this one
        PrefetchNextUnsolved(nextLevelId, mode);

        if (_current == null)
        {
            Debug.LogError($"Failed to load next level: {nextLevelId}");
            return;
        }

        // Save current level ID
        _levelsProviderService.SaveLastCurrent(_current.id, mode);
        
        // Setup UI for new level (with animations)
        if (_current.portrait) SetPortrait(_current.portrait);
        BuildSlotsForAnswer(_current.displayName);
        SetNormalizedAnswer(_current.normalizedName);

        HideSlotsImmediate();
        ClearKeyboard();

        await FlipInAsync();
        
        await ShowSlotsStaggerAsync();
        var pool = BuildLetterPool(_current.normalizedName);
        BuildKeyboardWithStagger(pool, OnKeyboardLetterPressed);

        await _interPacing.TrackAsync("level_solved");

        // Right before enabling input, RESUME the timer and loop
        if (streakTier >= StreakSystem.MaxStreak)
        {
            StreakSystem.Reset();
            _streakSystemView.StopStreakTimerLoop(); // keep hidden (no more window)
        }
        else
        {
            StreakSystem.StartWindowForNextStep(); // start the new countdown now
            _streakSystemView.StartStreakTimerLoop().Forget();       // show & tick the bar
        }

        SetInputEnabled(true);
        ResetWindowsInput();
    }
    
    private async UniTask DoWinFXAndRewardsAsync()
    {
        // Time-based bump + LP
        streakTier = StreakSystem.BumpAndSetDeadline(); // 1..5 (session-only)
        int award = (_streakSystemView.lpRewardByTier != null && streakTier < _streakSystemView.lpRewardByTier.Length) ? _streakSystemView.lpRewardByTier[streakTier] : 0;

        // 1) Always show CURRENT-ANSWER FX first (one consistent look/sound)
        await PlayCurrentAnswerFXAsync();

        if (award > 0)
        {
            await _fx.PlayGainFXAsync(
                award,
                WalletType.LP,
                () => { CurrencyStore.AddLP(award); },
                source: _lastKeyRT
            );
        }

        // Only show streak banner if at least Double
        if (streakTier >= 2)
            await _streakSystemView.PlayStreakTierFXAsync(streakTier, award);
    }
    
    async UniTask PlayCurrentAnswerFXAsync()
    {
        if (!currentBannerCG) return;
        _audioService.PlaySfx(SfxId.CurrentAnswer);

        currentBannerCG.gameObject.SetActive(true);
        currentBannerCG.alpha = 0f;
        currentBannerCG.transform.localScale = Vector3.one * 0.85f;

        var seq = DOTween.Sequence()
            .Append(currentBannerCG.DOFade(1f, 0.15f))
            .Join(currentBannerCG.transform.DOScale(1.05f, 0.15f))
            .AppendInterval(0.35f)
            .Append(currentBannerCG.DOFade(0f, 0.2f))
            .Join(currentBannerCG.transform.DOScale(1f, 0.2f));

        await seq.AsyncWaitForCompletion();
        currentBannerCG.gameObject.SetActive(false);
    }
    
    private List<char> BuildLetterPool(string normalizedAnswer)
    {
        var pool = new List<char>(normalizedAnswer.ToCharArray());

        // pad with random letters up to keyboardSize
        while (pool.Count < keyboardSize)
        {
            var c = alphabet[Random.Range(0, alphabet.Length)];
            pool.Add(c);
        }

        // shuffle
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }
    
    int FillNextEmpty(char c)
    {
        // 1) If we have a focused empty Letter, fill that one
        if (_focusSlotIndex >= 0 && _focusSlotIndex < _slots.Count)
        {
            var cur = _slots[_focusSlotIndex];
            if (cur.IsEmptyLetter)
            {
                cur.SetLetter(c);
                int filled = _focusSlotIndex;
                AdvanceFocusFrom(filled);
                return filled;
            }
        }

        // 2) Fallback: first empty letter from the start
        foreach (var si in _letterSlotIndices)
        {
            var sv = _slots[si];
            if (!sv.IsEmptyLetter) continue;
            sv.SetLetter(c);
            AdvanceFocusFrom(si);
            return si;
        }
        return -1; // no empty
    }
    
    private void AdvanceFocusFrom(int filledIndex)
    {
        // find next empty letter after the one we just filled
        int start = _letterSlotIndices.IndexOf(filledIndex);
        if (start < 0) start = 0;

        _focusSlotIndex = -1;
        for (int i = start + 1; i < _letterSlotIndices.Count; i++)
        {
            int si = _letterSlotIndices[i];
            if (_slots[si].IsEmptyLetter) { _focusSlotIndex = si; break; }
        }
        UpdateGlow();
    }
    
    private void UpdateGlow()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var sv = _slots[i];
            sv.SetGlow(i == _focusSlotIndex && sv.kind == SlotKind.Letter && sv.IsEmptyLetter);
        }
    }
    
    string ReadCurrentGuess()
    {
        System.Text.StringBuilder sb = new();
        foreach (int si in _letterSlotIndices)
        {
            var sv = _slots[si];
            if (sv.kind == SlotKind.Letter && sv.current != '\0')
                sb.Append(sv.current);
        }
        return sb.ToString();
    }
    
    async UniTask FlipInAsync()
    {
        _audioService.PlaySfx(SfxId.Flip);
        artContainerRC.localRotation = Quaternion.Euler(0, 90, 0);
        var t = artContainerRC.DOLocalRotate(Vector3.zero, 0.25f, RotateMode.Fast);
        await t.AsyncWaitForCompletion();
    }
    
    async UniTask FlipOutAsync()
    {
        //if (sfx && sfxFlip) sfx.PlayOneShot(sfxFlip);
        if (slotsCG) slotsCG.alpha = 0f;
        if (keyboardCG) keyboardCG.alpha = 0f;

        artContainerRC.localRotation = Quaternion.identity;
        var t = artContainerRC.DOLocalRotate(new Vector3(0, 90, 0), 0.25f, RotateMode.Fast);
        await t.AsyncWaitForCompletion();
    }
    
    void OnDestroy()
    {
        CurrencyStore.OnKeysChanged -= HandleKeysChanged;
        TeardownWindowsInput();
    }
    
    void HandleKeysChanged(int _)
    {
        RefreshSkipButtonUIOnMainThread().Forget();
    }

    async UniTaskVoid RefreshSkipButtonUIOnMainThread()
    {
        await UniTask.SwitchToMainThread();
        if (!this || !isActiveAndEnabled) return;
        RefreshSkipButtonUI();
    }
    
    private void SetupWindowsInput()
    {
        if (!IsWindowsKeyboardInputEnabled())
        {
            if (windowsInputRoot) windowsInputRoot.SetActive(false);
            SetKeyboardVisible(true);
            return;
        }

        if (windowsInputRoot) windowsInputRoot.SetActive(true);
        SetKeyboardVisible(false);

        if (windowsInputField)
        {
            windowsInputField.onSubmit.RemoveListener(OnWindowsSubmit);
            windowsInputField.onSubmit.AddListener(OnWindowsSubmit);
        }

        if (windowsConfirmButton)
        {
            windowsConfirmButton.onClick.RemoveAllListeners();
            windowsConfirmButton.onClick.AddListener(SubmitWindowsGuess);
        }

        ResetWindowsInput();
    }

    private void TeardownWindowsInput()
    {
        if (windowsInputField)
            windowsInputField.onSubmit.RemoveListener(OnWindowsSubmit);
        if (windowsConfirmButton)
            windowsConfirmButton.onClick.RemoveListener(SubmitWindowsGuess);
    }

    private void ResetWindowsInput()
    {
        if (!IsWindowsKeyboardInputEnabled()) return;
        if (!windowsInputField) return;
        windowsInputField.text = string.Empty;
        windowsInputField.ActivateInputField();
    }

    private void OnWindowsSubmit(string _)
    {
        SubmitWindowsGuess();
    }

    private void SubmitWindowsGuess()
    {
        if (!IsWindowsKeyboardInputEnabled()) return;
        if (_current == null) return;
        if (_revealBusy) return;

        var raw = windowsInputField ? windowsInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var guessNormalized = StringUtils.NormalizeAnswer(raw);
        var answerNorm = _current.normalizedName;

        if (guessNormalized == answerNorm)
        {
            GameProgress.MarkSolved(_current.id);
            _progressionService.NotifyStateMaybeChanged();
            ResetWindowsInput();
            HandleCorrectAndAdvanceAsync().Forget();
        }
        else
        {
            ResetWindowsInput();
            HandleWrongGuessAsync().Forget();
        }
    }

    private bool IsWindowsKeyboardInputEnabled()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer) return true;
        return Application.isEditor && enableWindowsInputInEditor;
    }
    
    private void SetKeyboardVisible(bool visible)
    {
        if (keyboardCG)
        {
            keyboardCG.gameObject.SetActive(visible);
            keyboardCG.alpha = visible ? 1f : 0f;
        }
        if (keyboardContainer)
            keyboardContainer.gameObject.SetActive(visible);
    }
    
    public void Debug_AutoSolve()
    {
        if (_current == null)
        {
            Debug.LogWarning("[LoG] No current level to auto-solve.");
            return;
        }

        if (inputBlocked) return;
        GameProgress.MarkSolved(_current.id);
        HandleCorrectAndAdvanceAsync().Forget(); // run your fancy win+transition flow
    }
}

public struct Token
{
    public SlotKind kind;   // Letter / FixedChar / Spacer
    public char ch;         // for FixedChar
}