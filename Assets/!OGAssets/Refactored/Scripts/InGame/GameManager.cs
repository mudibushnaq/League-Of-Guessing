using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

[SingletonMonoBehaviourAttribute(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(GameManager),
    context: AppContextType.DefaultScene,
    extraBindings: typeof(IGameManager))]
public class GameManager : MonoBehaviour, IGameManager, IGameInitializable, ILegacyGameInitializable
{
    int IGameInitializable.Order => 1;
    int ILegacyGameInitializable.Order => 1;
    
    LevelsData _levelsData { get; set; }
    private int _currentIdx = -1;
    private ChampionEntry _current;
    
    public Button backButton;
    // Hook your skip button groups in the Inspector:
    [Header("Skip Button UI Groups")]
    public Button skipButton;
    [SerializeField] private GameObject skipCostGroup; // contains key icon + amount text
    [SerializeField] private GameObject skipAdGroup;   // contains play/ad icon
    public Button RevealLetterButton;

    [Header("Economy")]
    public int unlockCost = 1; // cost per skill unlock
    public int skipKeyCost = 2;
    
    [Header("Windows Keyboard Input")]
    [SerializeField] private bool enableWindowsInputInEditor = false;
    [SerializeField] private GameObject windowsInputRoot;
    [SerializeField] private TMP_InputField windowsInputField;
    [SerializeField] private Button windowsConfirmButton;

    [Header("Keyboard Settings")]
    public int keyboardSize = 16;
    public string alphabet = "abcdefghijklmnopqrstuvwxyz";

    private RectTransform _lastKeyRT;

    [Header("Reveal Roulette Tuning")]
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
    public int[] lpRewardByTier = new int[] { 0, 50, 100, 150, 200, 300 }; // index 1..5 used
    private CancellationTokenSource _streakCts;

    private bool inputBlocked = false;
    
    [Inject] private IRewardFX _fx;
    [Inject] private PopupService _popupService;
    [Inject] private ILevelsProviderService _levelsProviderService;
    [Inject] private ISceneLoader _sceneLoader;
    [Inject] private IQWER_LevelUI _iqwerLevelUI;
    [Inject] private IAdsService _ads;
    [Inject] private IUnlockGateService _unlockGate;
    [Inject] private ISkipGateService _skipGate;
    [Inject] private IInterstitialPacingService _interPacing;
    [Inject] private IAudioService _audioService;
    [Inject] private IModeSelectionService _modeSel;
    [Inject] private IProgressionService _progressionService;
    
    [Header("Loading UI (Optional)")]
    [SerializeField] private LevelLoadingUI _loadingUI;
    
    async UniTask IGameInitializable.Initialize()
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
        _iqwerLevelUI.HideStreakTimer();

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

        _iqwerLevelUI.SlotClicked += OnSlotClicked;
        CurrencyStore.OnKeysChanged += OnKeysChanged;
        CurrencyStore.OnLPChanged += OnLPChanged;
        
        SetupWindowsInput();

        // Check if level is already downloaded (prefetched)
        bool isAlreadyDownloaded = _levelsProviderService.IsLevelDownloaded(nextLevelId, mode);
        bool shouldShowLoadingUI = !isAlreadyDownloaded || !_levelsProviderService.hideLoadingUIForPrefetchedLevels;

        // Prefetch levels ahead (if enabled)
        if (_levelsProviderService.prefetchAheadCount > 0)
        {
            _levelsProviderService.PrefetchLevelsAheadAsync(nextLevelId, _levelsProviderService.prefetchAheadCount, mode).Forget();
        }

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
    
    async UniTask ILegacyGameInitializable.Initialize()
    {
        // Same as IGameInitializable - reuse the same logic
        await ((IGameInitializable)this).Initialize();
    }

    void OnKeysChanged(int amount)
    {
        Debug.Log($"OnKeysChangedamount={amount}, CurrencyStore.Keys={CurrencyStore.Keys}");
    }

    void OnLPChanged(int amount)
    {
        Debug.Log($"OnLPChanged={amount}, CurrencyStore.LP={CurrencyStore.LP}");
    }

    private void UpdateLevelCounter()
    {
        var mode = _levelsProviderService.ActiveModeKey;
        int total = _levelsProviderService.GetLevelCount(mode);
        
        if (total <= 0)
        {
            _iqwerLevelUI.SetLevelText("0/0");
            return;
        }

        // Count solved levels from discovered IDs
        var levelIds = _levelsProviderService.GetLevelIds(mode);
        int solvedCount = levelIds.Count(id => GameProgress.Solved.Contains(id));
        int display = Mathf.Min(solvedCount + 1, total);
        
        _iqwerLevelUI.SetLevelText($"{display}/{total}");
    }

    private async UniTask HandleCorrectAndAdvanceAsync()
    {
        _iqwerLevelUI.SetInputEnabled(false);
        StreakSystem.ClearWindow();
        StopStreakTimerLoop();

        await DoWinFXAndRewardsAsync();

        // Auto-unlock any remaining for THIS champ
        int mask = SkillRevealStore.NormalizeMask(_current.id, 4);
        if (mask != 0b1111)
        {
            mask = await _iqwerLevelUI.RevealRemainingSkillsSequentialAsync(mask);
            SkillRevealStore.SaveMask(_current.id, mask);
            _iqwerLevelUI.ApplySkillMask(mask);
            _iqwerLevelUI.RefreshUnlockButtons(mask);
            RefreshUnlockButtonsWithGate(mask);
        }

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

        await _iqwerLevelUI.FlipOutAsync();
        UpdateLevelCounter();

        // Check if level is already downloaded (prefetched)
        bool isAlreadyDownloaded = _levelsProviderService.IsLevelDownloaded(nextLevelId, mode);
        bool shouldShowLoadingUI = !isAlreadyDownloaded || !_levelsProviderService.hideLoadingUIForPrefetchedLevels;

        // Show loading UI only if level is not prefetched (or if hideLoadingUIForPrefetchedLevels is false)
        if (shouldShowLoadingUI && _loadingUI != null)
            _loadingUI.Show("Loading next level...");

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

        // Prefetch next level ahead (if enabled and we have a current level)
        if (_levelsProviderService.prefetchAheadCount > 0 && !string.IsNullOrEmpty(currentLevelId))
        {
            // Prefetch one more level to maintain the buffer
            _levelsProviderService.PrefetchLevelAsync(nextLevelId, mode).Forget();
        }

        // Load next level (download + load asset)
        // If already prefetched, download phase will be instant
        _current = await _levelsProviderService.LoadSingleLevelAsync(nextLevelId, mode);

        // Unsubscribe
        _levelsProviderService.OnDownloadProgress -= progressHandler;
        _levelsProviderService.OnAssetsLoadProgress -= assetProgressHandler;

        // Hide loading UI
        if (_loadingUI != null)
            _loadingUI.Hide();

        if (_current == null)
        {
            Debug.LogError($"Failed to load next level: {nextLevelId}");
            return;
        }

        // Save current level ID
        _levelsProviderService.SaveLastCurrent(_current.id, mode);

        if (_current.skills is { Length: 4 })
            _iqwerLevelUI.SetSkillSprites(_current.skills[0], _current.skills[1], _current.skills[2], _current.skills[3]);
        else
            _iqwerLevelUI.SetSkillSprites(null, null, null, null); // or show portrait UI if you add it

        int newMask = SkillRevealStore.EnsureHasAtLeastOneBit(_current.id, 4);

        _iqwerLevelUI.ApplySkillMask(0);
        _iqwerLevelUI.SetAllUnlockButtonsVisible(true);
        _iqwerLevelUI.SetUnlockButtonsInteractable(false);

        _iqwerLevelUI.BuildSlotsForAnswer(_current.displayName);
        _iqwerLevelUI.SetNormalizedAnswer(_current.normalizedName);

        _iqwerLevelUI.HideSlotsImmediate();
        _iqwerLevelUI.ClearKeyboard();

        await _iqwerLevelUI.FlipInAsync();

        newMask = await _iqwerLevelUI.PlayButtonRouletteAndRevealOneAsync(newMask); // levelUI.PlaySkillRouletteAndRevealOneAsync(newMask);
        SkillRevealStore.SaveMask(_current.id, newMask);
        _iqwerLevelUI.RefreshUnlockButtons(newMask);
        RefreshUnlockButtonsWithGate(newMask);
        _iqwerLevelUI.ApplySkillMask(newMask);

        await _iqwerLevelUI.ShowSlotsStaggerAsync();
        var pool = BuildLetterPool(_current.normalizedName); // your existing method that returns List<char>
        _iqwerLevelUI.BuildKeyboardWithStagger(pool, OnKeyboardLetterPressed);

        await _interPacing.TrackAsync("level_solved");

        // Right before enabling input, RESUME the timer and loop
        if (streakTier >= StreakSystem.MaxStreak)
        {
            StreakSystem.Reset();
            StopStreakTimerLoop(); // keep hidden (no more window)
        }
        else
        {
            StreakSystem.StartWindowForNextStep(); // start the new countdown now
            StartStreakTimerLoop().Forget();       // show & tick the bar
        }

        _iqwerLevelUI.SetInputEnabled(true);
        ResetWindowsInput();
    }

    // Call this inside your win flow (HandleCorrectAndAdvanceAsync)
    private async UniTask DoWinFXAndRewardsAsync()
    {
        // Time-based bump + LP
        streakTier = StreakSystem.BumpAndSetDeadline(); // 1..5 (session-only)
        int award = (lpRewardByTier != null && streakTier < lpRewardByTier.Length) ? lpRewardByTier[streakTier] : 0;

        // 1) Always show CURRENT-ANSWER FX first (one consistent look/sound)
        await _iqwerLevelUI.PlayCurrentAnswerFXAsync();

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
            await _iqwerLevelUI.PlayStreakTierFXAsync(streakTier, award);
    }

    private async UniTaskVoid HandleWrongGuessAsync()
    {
        // 1) Small wrong FX (shake rows + optional banner) – lives in LevelUI
        if (_iqwerLevelUI != null)
            await _iqwerLevelUI.PlayWrongGuessFXAsync(false);

        // 2) Restore all keys and clear all filled letter slots
        // Copy pairs first to avoid modifying the dictionary while iterating.
        var snapshot = new List<KeyValuePair<int, LetterButton>>(_slotToButton);
        foreach (var kv in snapshot)
        {
            int slotIndex = kv.Key;
            var btn = kv.Value;

            // clear slot
            _iqwerLevelUI?.ClearSlotAt(slotIndex);

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
        _iqwerLevelUI?.FocusFirstEmptySlot();

        StreakSystem.Reset();
        StopStreakTimerLoop();
    }

    /// Call this from your “Reveal 2 Letters” button
    public async UniTask RevealTwoLettersWithRouletteAsync(
        int lettersToReveal = 2,
        int scanCycles = 12,
        int scanStepMs = 80,
        int settleMs = 200,
        int betweenLettersMs = 180)
    {
        if (_revealBusy || _current == null || _iqwerLevelUI == null) return;

        _revealBusy = true;
        _iqwerLevelUI.SetInputEnabled(false);

        try
        {
            int totalPos = _iqwerLevelUI.GetLetterSlotCount();
            if (totalPos <= 0) return;

            // --- Build fillable candidates (left-to-right) ---
            var freeByLetter = new Dictionary<char, List<LetterButton>>();
            foreach (var kb in _iqwerLevelUI.GetKeyboardButtons())
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
                if (_iqwerLevelUI.IsLetterSlotEmptyAtPos(pos))
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
                char L = char.ToUpperInvariant(_iqwerLevelUI.GetTargetLetterAtPos(pos));
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
                _iqwerLevelUI.SetInputEnabled(true);
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
                int gi = _iqwerLevelUI.GetGlobalSlotIndexForPos(pos);
                if (gi >= 0 && _slotToButton.TryGetValue(gi, out var priorBtn) && priorBtn)
                {
                    _iqwerLevelUI.ClearSlotAt(gi);
                    priorBtn.SetSelected(false);
                    priorBtn.SetHidden(false);
                    _slotToButton.Remove(gi);
                    _buttonToSlot.Remove(priorBtn);
                }
                else
                {
                    // focus the slot we’re about to fill
                    _iqwerLevelUI.ClearLetterSlotAtPos(pos);
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
            _iqwerLevelUI.SetInputEnabled(true);
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

    private void StopStreakTimerLoop()
    {
        _streakCts?.Cancel();
        _streakCts?.Dispose();
        _streakCts = null;
        _iqwerLevelUI.HideStreakTimer();
    }

    private async UniTaskVoid StartStreakTimerLoop()
    {
        StopStreakTimerLoop(); // ensure clean

        int tier = StreakSystem.GetCurrentTier();
        if (tier <= 0 || tier >= StreakSystem.MaxStreak)
        {
            _iqwerLevelUI.HideStreakTimer();
            return;
        }

        float window = StreakSystem.GetWindowSecondsForCurrentTier();
        _iqwerLevelUI.ShowStreakTimer(window);

        _streakCts = new CancellationTokenSource();
        var token = _streakCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                float remaining = StreakSystem.GetRemainingSeconds();
                if (remaining <= 0f)
                {
                    StreakSystem.Reset();
                    _iqwerLevelUI.HideStreakTimer();
                    if (_iqwerLevelUI != null)
                        await _iqwerLevelUI.PlayWrongGuessFXAsync(true);
                    break;
                }

                _iqwerLevelUI.UpdateStreakTimer(remaining, window);
                await UniTask.Delay(100, cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
        }
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

        if (entry.skills is { Length: 4 })
            _iqwerLevelUI.SetSkillSprites(entry.skills[0], entry.skills[1], entry.skills[2], entry.skills[3]);
        else
            _iqwerLevelUI.SetSkillSprites(null, null, null, null);

        int mask = SkillRevealStore.EnsureHasAtLeastOneBit(entry.id, 4);

        _iqwerLevelUI.ApplySkillMask(mask);
        _iqwerLevelUI.BindUnlockButtons(OnUnlockClicked);
        _iqwerLevelUI.RefreshUnlockButtons(mask);
        RefreshUnlockButtonsWithGate(mask);

        _iqwerLevelUI.BuildSlotsForAnswer(entry.displayName);
        _iqwerLevelUI.SetNormalizedAnswer(entry.normalizedName);

        BuildKeyboardForAnswer(entry.normalizedName);
        ResetWindowsInput();
    }

    private void OnUnlockClicked(int skillIndex)
    {
        int mask = SkillRevealStore.GetMask(_current.id);
        if ((mask & (1 << skillIndex)) != 0)
        {
            Debug.LogError("Cant unlock this skill, already visible");
            _popupService.ShowAsync(PopupPresets.CantUnlock(), "Gameplay").Forget();
            return;
        }

        var gate = _unlockGate.GetGate(CurrencyStore.Keys);
        if (gate == UnlockGate.Keys)
        {
            // Pay keys
            if (!CurrencyStore.TrySpendKeys(unlockCost))
            {
                // Edge case: keys changed while clicking; fallback to ad
                TryUnlockWithRewarded(skillIndex).Forget();
                return;
            }

            CommitSkillUnlock(skillIndex);
            return;
        }

        // Gate by Rewarded
        TryUnlockWithRewarded(skillIndex).Forget();
    }

    private async UniTaskVoid TryUnlockWithRewarded(int skillIndex)
    {
        // (Optional) preload if not ready
        if (!_ads.IsRewardedReady)
            await _ads.PreloadRewardedAsync();

        bool finished = await _ads.ShowRewardedAsync(onReward: () =>
        {
            CommitSkillUnlock(skillIndex);
        });

        if (!finished)
        {
            // optional: show a toast/popup if ad couldn’t be shown
            Debug.LogWarning("[Unlock] Rewarded ad not completed.");
        }
    }

    private void CommitSkillUnlock(int skillIndex)
    {
        SkillRevealStore.RevealSkill(_current.id, skillIndex);
        _unlockGate.RecordUnlock(); // <-- advance milestone

        int newMask = SkillRevealStore.GetMask(_current.id);
        _iqwerLevelUI.ApplySkillMask(newMask);
        _iqwerLevelUI.RefreshUnlockButtons(newMask);
        RefreshUnlockButtonsWithGate(newMask);

        _audioService.PlaySfx(SfxId.UnlockButton);
    }

    private void OnUnlockClicked_Old(int skillIndex)
    {
        // Already visible? nothing to do
        int mask = SkillRevealStore.GetMask(_current.id);
        if ((mask & (1 << skillIndex)) != 0)
        {
            Debug.LogError("Cant unlock this skill, already visible");
            _popupService.ShowAsync(PopupPresets.CantUnlock(), "Gameplay").Forget();
            return;
        }

        // Try to pay
        if (!CurrencyStore.TrySpendKeys(unlockCost))
        {
            _popupService.ShowAsync(PopupPresets.NotEnoughKeysMsg(unlockCost, CurrencyStore.Keys), "Gameplay").Forget();
            // TODO: feedback (shake button, sfx)
            return;
        }

        SkillRevealStore.RevealSkill(_current.id, skillIndex); // persists
        int newMask = SkillRevealStore.GetMask(_current.id);

        _iqwerLevelUI.ApplySkillMask(newMask);        // show all unlocked
        _iqwerLevelUI.RefreshUnlockButtons(newMask);  // hide this button
        RefreshUnlockButtonsWithGate(newMask);

        _audioService.PlaySfx(SfxId.UnlockButton);
    }

    private void RefreshUnlockButtonsWithGate(int mask)
    {
        for (int i = 0; i < 4; i++)
        {
            var btn = _iqwerLevelUI.GetUnlockButton(i);
            bool locked = (mask & (1 << i)) == 0;

            if (!locked)
            {
                // Already visible → hide the button and reset visuals to key mode (doesn't show anyway)
                if (btn) btn.gameObject.SetActive(false);
                _iqwerLevelUI.SetUnlockVisual(i, useAdMode: false, keyCost: unlockCost);
                continue;
            }

            // Decide gate from policy
            var gate = _unlockGate.GetGate(CurrencyStore.Keys);
            bool adMode = (gate == UnlockGate.Rewarded);

            // Show the button and set its visuals
            if (btn) btn.gameObject.SetActive(true);
            _iqwerLevelUI.SetUnlockVisual(i, useAdMode: adMode, keyCost: unlockCost);

            // Interactable rules
            bool hasKeys = CurrencyStore.Keys >= skipKeyCost;
            bool adReady = _ads != null && _ads.IsRewardedReady;
            bool interactable = adMode ? adReady : hasKeys;

            // Hard block if neither keys nor ads are available (covers both modes)
            if (!hasKeys && !adReady) interactable = false;

            if (btn) btn.interactable = interactable;
        }
    }

    // ----- After you compute/refresh masks, call this to apply gating visuals:
    private void RefreshUnlockButtonsWithGate_Old(int mask)
    {
        // For each locked skill, decide visual: keys or ad.
        for (int i = 0; i < 4; i++)
        {
            bool locked = (mask & (1 << i)) == 0;
            if (!locked)
            {
                // Already visible → ensure button hidden
                _iqwerLevelUI.SetUnlockVisual(i, useAdMode: false, keyCost: unlockCost);
                var btn = _iqwerLevelUI.GetUnlockButton(i);
                if (btn) btn.gameObject.SetActive(false);
                continue;
            }

            var gate = _unlockGate.GetGate(CurrencyStore.Keys);
            bool adMode = (gate == UnlockGate.Rewarded);

            // show the button and set its mode
            var b = _iqwerLevelUI.GetUnlockButton(i);
            if (b) b.gameObject.SetActive(true);
            _iqwerLevelUI.SetUnlockVisual(i, useAdMode: adMode, keyCost: unlockCost);
        }
    }

    // REMOVED: FindNextUnsolvedIndexFrom and FindNextUnsolvedIndex - now using GetNextUnsolvedLevelId from service

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

        _iqwerLevelUI.BuildKeyboard(pool, OnKeyboardLetterPressed);
    }

    private void OnSlotClicked(int slotIndex)
    {
        // If this slot was filled by some key, restore that key
        if (_slotToButton.TryGetValue(slotIndex, out var btn))
        {
            // Clear the slot visually
            _iqwerLevelUI.ClearSlotAt(slotIndex);

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

    void OnBackClicked() => LoadMenu().Forget();

    private async UniTaskVoid LoadMenu()
    {
        backButton.interactable = false;
        await _sceneLoader.LoadSceneSingleAsync("scenes/MenuScene");
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

    private async UniTask ExecuteSkipAndRecord()
    {
        await ExecuteSkip();        // your existing skip implementation (move level, clear maps, load next…)
        _skipGate.RecordSkip(); // <- advance the alternating rule
        RefreshSkipButtonUI();  // reflect new state (next skip may be ad-gated)
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

    private async UniTask ExecuteSkip()
    {
        StreakSystem.Reset();
        StopStreakTimerLoop();

        // Unload current level
        if (_current != null)
        {
            var currentId = _current.id;
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

        // Prefetch next level ahead (if enabled)
        if (_levelsProviderService.prefetchAheadCount > 0)
        {
            _levelsProviderService.PrefetchLevelAsync(nextLevelId, modeKey).Forget();
        }

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

        // Setup UI
        SetupLevel(_current);
        UpdateLevelCounter();
        RefreshSkipButtonUI();
    }

    private void OnKeyboardLetterPressed(char c, LetterButton btn)
    {
        // NEW: no toggle. Always try to consume this key.
        _lastKeyRT = btn ? btn.GetComponent<RectTransform>() : null;

        var filledIndex = _iqwerLevelUI.FillNextEmpty(c);
        if (filledIndex >= 0)
        {
            // Hide the key
            btn.SetSelected(true);
            btn.SetHidden(true);

            // Remember mappings
            _buttonToSlot[btn] = filledIndex;
            _slotToButton[filledIndex] = btn;

            // Check answer
            var guessRaw = _iqwerLevelUI.ReadCurrentGuess();
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
                    else
                    {
                        Debug.Log("Not quite. Tap a slot to remove a letter and return its key.");
                    }
                }
            }

            _audioService.PlaySfx(SfxId.LetterClick);
        }
        // If no empty slot, you can give feedback (shake).
    }

    // === Debug helpers for Editor menu ===
    public void Debug_AutoSolve()
    {
        if (_current == null)
        {
            Debug.LogWarning("[LoG] No current level to auto-solve.");
            return;
        }

        GameProgress.MarkSolved(_current.id);
        HandleCorrectAndAdvanceAsync().Forget(); // run your fancy win+transition flow
    }

    public void Debug_UnlockAllSkillsCurrent()
    {
        if (_current == null)
        {
            Debug.LogWarning("[LoG] No current level to unlock.");
            return;
        }

        int mask = 0b1111;
        SkillRevealStore.SaveMask(_current.id, mask);
        _iqwerLevelUI.ApplySkillMask(mask);
        _iqwerLevelUI.RefreshUnlockButtons(mask);
        RefreshUnlockButtonsWithGate(mask);
    }

    public void Debug_SkipCurrent()
    {
        OnSkipClicked();
    }

    private void OnDestroy()
    {
        _iqwerLevelUI.SlotClicked -= OnSlotClicked;
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
            _iqwerLevelUI?.SetKeyboardVisible(true);
            return;
        }

        if (windowsInputRoot) windowsInputRoot.SetActive(true);
        _iqwerLevelUI?.SetKeyboardVisible(false);

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
        if (_current == null || _iqwerLevelUI == null) return;
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
}