using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

#if DOTWEEN_ENABLED
using DG.Tweening;
#endif

[DisallowMultipleComponent]
public sealed class ClickBlocker : MonoBehaviour, IUIBlocker
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;     // attach to a full-screen overlay GameObject
    [SerializeField] private Image overlayImage;          // full-screen transparent/semi-transparent Image

    [Header("Behavior")]
    [SerializeField] private bool startHidden = true;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 0.6f;
    [SerializeField] private float fadeDuration = 0.15f;
    public float FadeDuration => fadeDuration;
    [SerializeField] private bool blockOnAwake = false;
    private readonly Stack<string> _reasons = new(); // optional: track who’s blocking
    private int _blockRefCount;
    private bool _isAnimating;

    public bool IsBlocking => _blockRefCount > 0;
    
    private void Reset()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!overlayImage) overlayImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!overlayImage) overlayImage = GetComponent<Image>();

        if (startHidden && !blockOnAwake)
            SetHiddenImmediate();
        else if (blockOnAwake)
            SetShownImmediate();
    }

    public async UniTask<IDisposable> BlockScopeAsync(string reason = null, float? timeoutSeconds = null)
    {
        _blockRefCount++;
        if (!string.IsNullOrEmpty(reason)) _reasons.Push(reason);

        if (_blockRefCount == 1) // transition from 0 -> 1
            await ShowAsync();

        if (timeoutSeconds.HasValue && timeoutSeconds.Value > 0)
            _ = AutoTimeoutRelease(timeoutSeconds.Value);

        return new Scope(this, reason);
    }

    public void AllowAll()
    {
        _blockRefCount = 0;
        _reasons.Clear();
        _ = HideAsync();
    }

    // ---- Internal helpers ----

    private async UniTask AutoTimeoutRelease(float seconds)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(seconds));
        // If still blocking, release one scope (safety)
        TryRelease(null);
    }

    private void TryRelease(string reason)
    {
        if (_blockRefCount <= 0) return;

        _blockRefCount--;
        if (!string.IsNullOrEmpty(reason) && _reasons.Count > 0 && _reasons.Peek() == reason)
            _reasons.Pop();

        if (_blockRefCount <= 0)
            _ = HideAsync();
    }

    private async UniTask ShowAsync()
    {
        if (!canvasGroup) return;

        overlayImage.raycastTarget = true;

#if DOTWEEN_ENABLED
        if (_isAnimating) DOTween.Kill(canvasGroup);
        _isAnimating = true;
        canvasGroup.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        await canvasGroup.DOFade(visibleAlpha, fadeDuration).SetUpdate(true).ToUniTask();
        _isAnimating = false;
#else
        canvasGroup.gameObject.SetActive(true);
        await FadeAlpha(canvasGroup, 0f, visibleAlpha, fadeDuration);
#endif
    }

    private async UniTask HideAsync()
    {
        if (!canvasGroup) return;

#if DOTWEEN_ENABLED
        if (_isAnimating) DOTween.Kill(canvasGroup);
        _isAnimating = true;
        await canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).ToUniTask();
        canvasGroup.gameObject.SetActive(false);
        overlayImage.raycastTarget = false;
        _isAnimating = false;
#else
        await FadeAlpha(canvasGroup, canvasGroup.alpha, 0f, fadeDuration);
        canvasGroup.gameObject.SetActive(false);
        overlayImage.raycastTarget = false;
#endif
    }

    private void SetHiddenImmediate()
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(false);
        if (overlayImage) overlayImage.raycastTarget = false;
        _isAnimating = false;
    }

    private void SetShownImmediate()
    {
        if (!canvasGroup) return;
        canvasGroup.gameObject.SetActive(true);
        canvasGroup.alpha = visibleAlpha;
        if (overlayImage) overlayImage.raycastTarget = true;
        _isAnimating = false;
    }

    private static async UniTask FadeAlpha(CanvasGroup cg, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            cg.alpha = to;
            return;
        }
        cg.alpha = from;
        cg.gameObject.SetActive(true);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, k);
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        cg.alpha = to;
    }

    private sealed class Scope : IDisposable
    {
        private ClickBlocker _owner;
        private readonly string _reason;

        public Scope(ClickBlocker owner, string reason)
        {
            _owner = owner;
            _reason = reason;
        }

        public void Dispose()
        {
            if (_owner == null) return;
            _owner.TryRelease(_reason);
            _owner = null;
        }
    }
}
