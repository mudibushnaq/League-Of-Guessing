using System;
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

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(StreakSystemView),
    context: AppContextType.PortraitScene)]
public class StreakSystemView : MonoBehaviour, IPortraitGameInitializable
{
    int IPortraitGameInitializable.Order => 0;
    
    [Header("Streak System")]
    private int streakTier;
    private CancellationTokenSource _streakCts;
    public CanvasGroup streakTimerGroup; // on StreakTimer root (alpha=0 by default)
    public Image       streakTimerFill;  // Filled Horizontal
    public TMP_Text    streakTimerText;  // shows "34s"
    public int[] lpRewardByTier; // index 1..5 used

    [Header("Streak FX")]
    public CanvasGroup streakBannerCG;      // alpha 0 at rest
    public Image       streakImage;
    public TMP_Text    streakLPText;
    public Sprite      spDouble, spTriple, spQuadra, spPenta;

    [Inject] private AudioManager _audioService;
    
    UniTask IPortraitGameInitializable.Initialize()
    {
        return UniTask.CompletedTask;
    }
    
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
    
    public void StopStreakTimerLoop()
    {
        _streakCts?.Cancel();
        _streakCts?.Dispose();
        _streakCts = null;
        HideStreakTimer();
    }
    
    public async UniTaskVoid StartStreakTimerLoop()
    {
        StopStreakTimerLoop(); // ensure clean

        int tier = StreakSystem.GetCurrentTier();
        if (tier <= 0 || tier >= StreakSystem.MaxStreak)
        {
            HideStreakTimer();
            return;
        }

        float window = StreakSystem.GetWindowSecondsForCurrentTier();
        ShowStreakTimer(window);

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
                    HideStreakTimer();
                    //if (_iqwerLevelUI != null)
                        //await _iqwerLevelUI.PlayWrongGuessFXAsync(true);
                    break;
                }

                UpdateStreakTimer(remaining, window);
                await UniTask.Delay(100, cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
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
}
