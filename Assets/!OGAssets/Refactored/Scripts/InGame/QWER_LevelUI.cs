using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[SingletonMonoBehaviourAttribute(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(QWER_LevelUI),
    context: AppContextType.DefaultScene,
    extraBindings: typeof(IQWER_LevelUI))]
public class QWER_LevelUI : MonoBehaviour, IQWER_LevelUI, IGameInitializable, ILegacyGameInitializable
{
    int ILegacyGameInitializable.Order => 0;
    int IGameInitializable.Order => 0;
    
    public TextMeshProUGUI levelText;
    public CanvasGroup streakTimerGroup; // on StreakTimer root (alpha=0 by default)
    public Image       streakTimerFill;  // Filled Horizontal
    public TMP_Text    streakTimerText;  // shows "34s"
    
    // LevelUI fields
    public RectTransform artContainerRC;     // parent that holds the 4 skill images
    public Image artContainerImage;     // parent that holds the 4 skill images

    
    // ==== Artwork grid (assign in Inspector) ====
    [Header("Skill Images (Q,W,E,R)")]
    public SkillCell skillCellQ;
    public SkillCell skillCellW;
    public SkillCell skillCellE;
    public SkillCell skillCellR;
    
    [Header("Slot Rows (set in Inspector)")]
    public RectTransform slotsRow1;
    public RectTransform slotsRow2;
    
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
    
    [Header("Keyboard")]
    public RectTransform keyboardContainer;    // GridLayoutGroup parent
    public LetterButton keyBoardButtonPrefab;
    public CanvasGroup   keyboardCG;
    private string _normalizedTarget = "";
    private readonly List<LetterButton> _keyboardButtons = new();
    
    private readonly List<SlotView> _slots = new();
    private readonly List<int> _letterSlotIndices = new(); // indices into _slots for Letter kinds

    // NEW: which global slot currently has the cursor glow
    private int _focusSlotIndex = -1;
    
    public event Action<int> SlotClicked;
    
    private float _lastTickTime;
    
    [Header("Current Answer FX")]
    public CanvasGroup currentBannerCG;     // alpha 0 at rest

    [Header("Streak FX")]
    public CanvasGroup streakBannerCG;      // alpha 0 at rest
    public Image       streakImage;
    public TMP_Text    streakLPText;
    public Sprite      spDouble, spTriple, spQuadra, spPenta;
    
    [Header("Wrong Answer FX")]
    public CanvasGroup wrongBannerCG;     // alpha 0 at rest
    public CanvasGroup shutDownBannerCG;     // alpha 0 at rest

    [Header("Audio References")]
    public float tickMinInterval = 0.03f; // throttle (seconds)
    public Vector2 pitchJitter = new Vector2(0.97f, 1.03f); // tiny randomization
    
    [Inject] private IAudioService _audioService;
    
    UniTask ILegacyGameInitializable.Initialize()
    {
        return UniTask.CompletedTask; 
    }
    
    UniTask IGameInitializable.Initialize()
    {
        return UniTask.CompletedTask; 
    }

    public async UniTask<int> PlayButtonRouletteAndRevealOneAsync(int existingMask)
    {
        // Keep ALL skill images hidden during roulette
        ApplySkillMask(0);

        // Show all four buttons and disable user clicks during the roulette
        SetAllUnlockButtonsVisible(true);
        SetUnlockButtonsInteractable(false);

        // Tick through buttons
        int cycles = 8;
        int cur = -1;
        for (int c = 0; c < cycles; c++)
        {
            cur = (cur + 1) % 4;
            var btn = GetUnlockButton(cur);
            if (btn && btn.gameObject.activeInHierarchy)
            {
                var t = btn.transform;
                t.DOKill(true);
                t.localScale = Vector3.one;
                t.DOScale(1.15f, 0.08f).SetLoops(2, LoopType.Yoyo);
                _audioService.PlaySfx(SfxId.Tick);
            }
            await UniTask.Delay(TimeSpan.FromMilliseconds(Mathf.Lerp(110, 180, c / (float)cycles)));
        }

        // Choose which to reveal: prefer the bit(s) already set in existingMask (usually exactly one)
        List<int> allowed = new();
        for (int i = 0; i < 4; i++) if ((existingMask & (1<<i)) != 0) allowed.Add(i);
        if (allowed.Count == 0) { allowed.Add(UnityEngine.Random.Range(0, 4)); }

        int pick = allowed[UnityEngine.Random.Range(0, allowed.Count)];

        // Final emphasis on the chosen button
        var pickBtn = GetUnlockButton(pick);
        var img = GetSkillImage(pick);
        
        // Final emphasis on the chosen button
        if (pickBtn)
        {
            var t = pickBtn.transform;
            t.DOKill(true);
            t.localScale = Vector3.one;
            await t.DOScale(1.2f, 0.12f).SetEase(Ease.OutBack).AsyncWaitForCompletion();

            // === NEW: get the button out of the way BEFORE revealing the image ===
            var cg = pickBtn.GetComponent<CanvasGroup>();
            if (!cg) cg = pickBtn.gameObject.AddComponent<CanvasGroup>();
            cg.interactable   = false;
            cg.blocksRaycasts = false;

            // Put image above button in hierarchy (safety if they share the same parent)
            //if (img) img.transform.SetAsLastSibling();

            // Fast fade/scale-out of the button, then disable it
            var btnOut = DOTween.Sequence()
                .Join(t.DOScale(0.85f, 0.08f))
                .Join(cg.DOFade(0f, 0.08f));
            await btnOut.AsyncWaitForCompletion();

            pickBtn.gameObject.SetActive(false);
            cg.alpha = 1f; // reset for potential reuse later
            t.localScale = Vector3.one;
        }

        // Reveal ONLY the chosen image now (with a little pop)
        if (img)
        {
            img.enabled = true;

            // If you have DOTween UI module:
            // img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);

            img.transform.localScale = Vector3.one * 0.75f;
            _audioService.PlaySfx(SfxId.Reveal);

            // Scale (and optional fade) in
            var seq = DOTween.Sequence()
                .Join(img.transform.DOScale(1f, 0.20f).SetEase(Ease.OutBack));
            // If DOTween UI module is available, uncomment:
            // seq.Join(img.DOFade(1f, 0.20f));
            await seq.AsyncWaitForCompletion();
        }
        
        // Re-enable interactability for remaining buttons after roulette
        SetUnlockButtonsInteractable(true);

        // Mask itself doesn’t change here (it was already set by EnsureHasAtLeastOneBit)
        return existingMask;
    }
    
    public async UniTask PlayCurrentAnswerFXAsync()
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
    
    public async UniTask PlayWrongGuessFXAsync(bool isShutDown)
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
    
    public async UniTask PlayStreakTierFXAsync(int tier, int lpAward)
    {
        if (tier < 2 || !streakBannerCG) return; // never call for tier 1

        if (streakImage) streakImage.sprite = GetStreakSprite(tier);
        if (streakLPText) streakLPText.text = (lpAward > 0) ? $"+{lpAward} LP" : "";

        var clip = GetStreakClip(tier);
        _audioService.PlaySfx(clip);

        streakBannerCG.gameObject.SetActive(true);
        streakBannerCG.alpha = 0f;
        streakBannerCG.transform.localScale = Vector3.one * 0.85f;

        var seq = DG.Tweening.DOTween.Sequence()
            .Append(streakBannerCG.DOFade(1f, 0.15f))
            .Join(streakBannerCG.transform.DOScale(1.05f, 0.15f))
            .AppendInterval(0.4f)
            .Append(streakBannerCG.DOFade(0f, 0.2f))
            .Join(streakBannerCG.transform.DOScale(1f, 0.2f));

        await seq.AsyncWaitForCompletion();
        streakBannerCG.gameObject.SetActive(false);
    }
    
    private Sprite GetStreakSprite(int tier) =>
        tier switch { 2 => spDouble, 3 => spTriple, 4 => spQuadra, 5 => spPenta, _ => null };

    private SfxId GetStreakClip(int tier) =>
        tier switch { 2 => SfxId.DoubleKill, 3 => SfxId.TripleKill, 4 => SfxId.QuadraKill, 5 => SfxId.PentaKill, 6 => SfxId.HexaKill, _ => SfxId.CurrentAnswer };
    
    public void ShowStreakTimer(float windowSeconds)
    {
        if (!streakTimerGroup || !streakTimerFill) return;
        streakTimerFill.fillAmount = 1f;
        if (streakTimerText) streakTimerText.text = Mathf.CeilToInt(windowSeconds).ToString() + "s";
        streakTimerGroup.alpha = 1f;
        streakTimerGroup.gameObject.SetActive(true);
    }

    public void UpdateStreakTimer(float remainingSeconds, float windowSeconds)
    {
        if (!streakTimerGroup || !streakTimerFill) return;
        float t = (windowSeconds <= 0f) ? 0f : Mathf.Clamp01(remainingSeconds / windowSeconds);
        streakTimerFill.fillAmount = t;
        if (streakTimerText) streakTimerText.text = Mathf.CeilToInt(Mathf.Max(remainingSeconds, 0f)).ToString() + "s";
    }

    public void HideStreakTimer()
    {
        if (!streakTimerGroup) return;
        streakTimerGroup.alpha = 0f;
        streakTimerGroup.gameObject.SetActive(false);
    }
    
    public async UniTask<int> RevealRemainingSkillsSequentialAsync(int mask)
    {
        for (int i = 0; i < 4; i++)
        {
            int bit = 1 << i;
            if ((mask & bit) != 0) continue; // already unlocked
            
            // Hide that skill's unlock button
            SetUnlockButtonVisible(i, false);
            
            var img = GetSkillImage(i);
            if (img)
            {
                img.enabled = true;
                img.transform.localScale = Vector3.one * 0.8f;
                await img.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).AsyncWaitForCompletion();
            }
            mask |= bit;
            await UniTask.Delay(TimeSpan.FromMilliseconds(90));
        }
        return mask;
    }
    
    public async UniTask FlipOutAsync()
    {
        //if (sfx && sfxFlip) sfx.PlayOneShot(sfxFlip);
        if (slotsCG) slotsCG.alpha = 0f;
        if (keyboardCG) keyboardCG.alpha = 0f;

        artContainerRC.localRotation = Quaternion.identity;
        var t = artContainerRC.DOLocalRotate(new Vector3(0, 90, 0), 0.25f, RotateMode.Fast);
        await t.AsyncWaitForCompletion();
    }

    public async UniTask FlipInAsync()
    {
        _audioService.PlaySfx(SfxId.Flip);
        artContainerRC.localRotation = Quaternion.Euler(0, 90, 0);
        var t = artContainerRC.DOLocalRotate(Vector3.zero, 0.25f, RotateMode.Fast);
        await t.AsyncWaitForCompletion();
    }
    
    // Call this when a level loads
    public void SetSkillSprites(Sprite q, Sprite w, Sprite e, Sprite r)
    {
        if (skillCellQ) skillCellQ.thisImage.sprite = q;
        if (skillCellW) skillCellW.thisImage.sprite = w;
        if (skillCellE) skillCellE.thisImage.sprite = e;
        if (skillCellR) skillCellR.thisImage.sprite = r;

        // default all off; GameManager will show only the one from the mask
        SetSkillVisible(0, false);
        SetSkillVisible(1, false);
        SetSkillVisible(2, false);
        SetSkillVisible(3, false);
    }

    // show/hide a single cell by enabling the Image
    void SetSkillVisible(int index, bool visible)
    {
        switch (index)
        {
            case 0: if (skillCellQ) skillCellQ.thisImage.enabled = visible; break;
            case 1: if (skillCellW) skillCellW.thisImage.enabled = visible; break;
            case 2: if (skillCellE) skillCellE.thisImage.enabled = visible; break;
            case 3: if (skillCellR) skillCellR.thisImage.enabled = visible; break;
        }
    }
    
    public void ApplySkillMask(int mask)
    {
        SetSkillVisible(0, (mask & (1 << 0)) != 0);
        SetSkillVisible(1, (mask & (1 << 1)) != 0);
        SetSkillVisible(2, (mask & (1 << 2)) != 0);
        SetSkillVisible(3, (mask & (1 << 3)) != 0);
    }
    
    // exactly one visible (used at start)
    public void ApplySkillMaskStrictOne(int mask)
    {
        SetSkillVisible(0, false);
        SetSkillVisible(1, false);
        SetSkillVisible(2, false);
        SetSkillVisible(3, false);

        int chosen = -1;
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) { chosen = i; break; }
        if (chosen < 0) chosen = 0;
        SetSkillVisible(chosen, true);
    }
    
    public void SetUnlockVisual(int index, bool useAdMode, int keyCost)
    {
        var btn = GetSkillCell(index);
        if (!btn) return;

        var costGroup = btn.costGroup;
        var adGroup = btn.adGroup;

        if (costGroup) costGroup.gameObject.SetActive(!useAdMode);
        if (adGroup)   adGroup.gameObject.SetActive(useAdMode);

        // Optional: update cost text/icon
        if (!useAdMode && costGroup)
        {
            var amountText = costGroup.GetComponentInChildren<TMP_Text>(true);
            if (amountText) amountText.text = keyCost.ToString();
        }

        // Optional: swap ad icon if provided
        /*if (useAdMode && adGroup && adIcon)
        {
            var img = adGroup.GetComponentInChildren<Image>(true);
            if (img) img.sprite = adIcon;
        }*/
    }
    
    public void HideSlotsImmediate()
    {
        if (slotsCG) slotsCG.alpha = 0f;
    }
    
    public async UniTask ShowSlotsStaggerAsync()
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
    
    public void BuildKeyboardWithStagger(List<char> pool, Action<char, LetterButton> onKey)
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
    
    public void BuildSlotsForAnswer(string displayName)
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
    
    int CountLetters(List<Token> tks)
    {
        int n = 0;
        for (int i = 0; i < tks.Count; i++)
            if (tks[i].kind == SlotKind.Letter) n++;
        return n;
    }

    public void ClearSlots()
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
    
    // Wire the buttons once per level (listeners cleared)
    public void BindUnlockButtons(Action<int> onClick)
    {
        if (skillCellQ.unlockButton) { skillCellQ.unlockButton.onClick.RemoveAllListeners(); skillCellQ.unlockButton.onClick.AddListener(() => onClick?.Invoke(0)); }
        if (skillCellW.unlockButton) { skillCellW.unlockButton.onClick.RemoveAllListeners(); skillCellW.unlockButton.onClick.AddListener(() => onClick?.Invoke(1)); }
        if (skillCellE.unlockButton) { skillCellE.unlockButton.onClick.RemoveAllListeners(); skillCellE.unlockButton.onClick.AddListener(() => onClick?.Invoke(2)); }
        if (skillCellR.unlockButton) { skillCellR.unlockButton.onClick.RemoveAllListeners(); skillCellR.unlockButton.onClick.AddListener(() => onClick?.Invoke(3)); }
    }
    
    public SkillCell GetSkillCell(int i) =>
        i switch { 0 => skillCellQ, 1 => skillCellW, 2 => skillCellE, 3 => skillCellR, _ => null };
    
    public Button GetUnlockButton(int i) =>
        i switch { 0 => skillCellQ.unlockButton, 1 => skillCellW.unlockButton, 2 => skillCellE.unlockButton, 3 => skillCellR.unlockButton, _ => null };
    
    public void SetAllUnlockButtonsVisible(bool visible)
    {
        if (skillCellQ.unlockButton) skillCellQ.unlockButton.gameObject.SetActive(visible);
        if (skillCellW.unlockButton) skillCellW.unlockButton.gameObject.SetActive(visible);
        if (skillCellE.unlockButton) skillCellE.unlockButton.gameObject.SetActive(visible);
        if (skillCellR.unlockButton) skillCellR.unlockButton.gameObject.SetActive(visible);
    }
    
    public void SetUnlockButtonsInteractable(bool on)
    {
        if (skillCellQ.unlockButton) skillCellQ.unlockButton.interactable = on;
        if (skillCellW.unlockButton) skillCellW.unlockButton.interactable = on;
        if (skillCellE.unlockButton) skillCellE.unlockButton.interactable = on;
        if (skillCellR.unlockButton) skillCellR.unlockButton.interactable = on;
    }
    
    private void SetUnlockButtonVisible(int i, bool visible)
    {
        var b = GetUnlockButton(i);
        if (b) b.gameObject.SetActive(visible);
    }
    
    // Show unlock buttons only for locked skills
    public void RefreshUnlockButtons(int mask)
    {
        SetUnlockVisible(0, !Bit(mask, 0));
        SetUnlockVisible(1, !Bit(mask, 1));
        SetUnlockVisible(2, !Bit(mask, 2));
        SetUnlockVisible(3, !Bit(mask, 3));
    }
    
    private void SetUnlockVisible(int index, bool show)
    {
        switch (index)
        {
            case 0: if (skillCellQ.unlockButton) skillCellQ.unlockButton.gameObject.SetActive(show); break;
            case 1: if (skillCellW.unlockButton) skillCellW.unlockButton.gameObject.SetActive(show); break;
            case 2: if (skillCellE.unlockButton) skillCellE.unlockButton.gameObject.SetActive(show); break;
            case 3: if (skillCellR.unlockButton) skillCellR.unlockButton.gameObject.SetActive(show); break;
        }
    }

    public void BuildKeyboard(List<char> letters, Action<char, LetterButton> onPressed)
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

    public void ClearKeyboard()
    {
        _keyboardButtons.Clear();
        for (int i = keyboardContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(keyboardContainer.GetChild(i).gameObject);
    }

    public string ReadCurrentGuess()
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
    
    public int FillNextEmpty(char c)
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

    public void ClearSlotAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;
        var sv = _slots[slotIndex];
        if (sv.kind != SlotKind.Letter) return;

        sv.ClearLetter();
        _focusSlotIndex = slotIndex; // focus the just-cleared slot
        UpdateGlow();
    }

    private bool Bit(int mask, int i) => (mask & (1 << i)) != 0;
    
    // ---------------------- INTERNALS ----------------------

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
    
    public void FocusFirstEmptySlot()
    {
        // reuse the existing logic
        FocusFirstEmpty();
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
    
    private void UpdateGlow()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var sv = _slots[i];
            sv.SetGlow(i == _focusSlotIndex && sv.kind == SlotKind.Letter && sv.IsEmptyLetter);
        }
    }
    
    public void SetInputEnabled(bool on)
    {
        if (keyboardCG) { keyboardCG.blocksRaycasts = on; }
        if (slotsCG)    { slotsCG.blocksRaycasts    = on; }
    }
    
    public void SetKeyboardVisible(bool visible)
    {
        if (keyboardCG)
        {
            keyboardCG.gameObject.SetActive(visible);
            keyboardCG.alpha = visible ? 1f : 0f;
        }
        if (keyboardContainer)
            keyboardContainer.gameObject.SetActive(visible);
    }
    
    private Image GetSkillImage(int i) =>
        i switch { 0 => skillCellQ.thisImage, 1 => skillCellW.thisImage, 2 => skillCellE.thisImage, 3 => skillCellR.thisImage, _ => null };

    public void SetLevelText(string txt)
    {
        levelText.SetText(txt);
    }
    
    void WireSlots(List<SlotView> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var idx = i; // IMPORTANT: capture, avoids the classic loop-closure bug
            slots[i].InitLetter(() => SlotClicked?.Invoke(idx));
        }
    }
    
    public int GetLetterSlotCount() => _letterSlotIndices.Count;

    public bool IsLetterSlotEmptyAtPos(int pos)
    {
        if (pos < 0 || pos >= _letterSlotIndices.Count) return false;
        var gi = _letterSlotIndices[pos];
        return gi >= 0 && gi < _slots.Count && _slots[gi].IsEmptyLetter;
    }

    public void ClearLetterSlotAtPos(int pos)
    {
        var gi = GetGlobalSlotIndexForPos(pos);
        if (gi >= 0) ClearSlotAt(gi);
    }

    public int GetGlobalSlotIndexForPos(int pos)
    {
        if (pos < 0 || pos >= _letterSlotIndices.Count) return -1;
        return _letterSlotIndices[pos];
    }

    public char GetTargetLetterAtPos(int pos)
    {
        if (string.IsNullOrEmpty(_normalizedTarget) || pos < 0 || pos >= _normalizedTarget.Length) return '\0';
        return _normalizedTarget[pos];
    }

    public IReadOnlyList<LetterButton> GetKeyboardButtons() => _keyboardButtons;
    
    public void SetNormalizedAnswer(string normalized)
    {
        _normalizedTarget = string.IsNullOrWhiteSpace(normalized) ? "" : normalized.ToUpperInvariant();
    }
    
    public void SetArtContainerBG(Sprite bg)
    {
        artContainerImage.sprite = bg;
    }
}