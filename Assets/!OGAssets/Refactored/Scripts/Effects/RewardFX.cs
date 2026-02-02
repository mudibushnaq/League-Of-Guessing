using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;


[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(RewardFX),
    gameObjectName: nameof(RewardFX),
    extraBindings: typeof(IRewardFX))]
public class RewardFX : MonoBehaviour, IRewardFX, IProjectInitializable
{
    int IProjectInitializable.Order => 0;
    
    [Header("FX Canvas Layer (top-most)")]
    [SerializeField] private RectTransform fxLayer; // assign the 'FXLayer' child here in Inspector
    [Header("Defaults (used if not overridden in request)")]
    [SerializeField] private TMP_Text defaultLabel;   // optional fallback
    [SerializeField] private int   maxIcons = 10;
    [SerializeField] private float flyTime = 0.55f;
    [SerializeField] private float perIconStagger = 0.05f;
    [SerializeField] private bool useUnscaledTime = true;

    // explosion tunables (same as you have now)...
    [SerializeField] private float burstTime = 0.22f;
    [SerializeField] private float burstMinRadius = 70f, burstMaxRadius = 150f, burstUpBias = 0.25f;
    [SerializeField] private float burstOvershoot = 1.22f;
    [SerializeField] private Vector2 spinRange = new(360f, 900f);
    
    [Header("Default Icon Prefabs (UI Rects)")]
    [SerializeField] private RectTransform keyIconPrefabRect;
    [SerializeField] private RectTransform lpIconPrefabRect;
    
    [Header("Floating +N text (under the wallet label)")]
    [SerializeField] private TMP_Text deltaTextPrefab;     // a simple TMP_Text prefab (center pivot, raycast off)
    [SerializeField] private Vector2  deltaUnderOffset = new Vector2(0f, -18f); // place under the label
    [SerializeField] private float    deltaRise        = 26f;   // rise distance in px
    [SerializeField] private float    deltaDuration    = 0.85f; // total life
    [SerializeField] private float    deltaIn          = 0.12f; // fade/scale in
    [SerializeField] private float    deltaOut         = 0.20f; // fade out at end
    [SerializeField] private float    deltaScaleIn     = 1.08f; // little pop
    [SerializeField] private float    deltaXJitter     = 8f;    // small horizontal random
    
    public Transform Transform => transform;  // forward Unity’s transform
    public GameObject GameObject => gameObject;
    
    [Inject] IAudioService _audio;
    
    UniTask IProjectInitializable.Initialize()
    {
        // Make sure our canvas is truly top-most
        var c = GetComponentInParent<Canvas>();
        if (c)
        {
            c.overrideSorting = true;
            if (c.sortingOrder < 5000) c.sortingOrder = 5000;
        }
        if (!fxLayer) fxLayer = (RectTransform)transform;
        return UniTask.CompletedTask;
    }
    
    // --- ONE-LINER: global static entry point ---
    public async UniTask PlayGainFXAsync(int qty, WalletType walletType, Action onCommitted = null,
        RectTransform source = null, RectTransform iconPrefabOverride = null)
    {
        await PlayGainFX(qty, walletType, onCommitted, source, iconPrefabOverride);
    }

    // Optional async overload if your follow-up is async:
    public void PlayGainFX(int qty, WalletType walletType, Func<UniTask> onCommittedAsync = null,
                                  RectTransform source = null, RectTransform iconPrefabOverride = null)
    {
        PlayGainFX(qty, walletType, null, source, iconPrefabOverride, onCommittedAsync).Forget();
    }

    // --- Instance core (used by the static wrappers) ---
    async UniTask PlayGainFX(int qty,
                                              WalletType walletType,
                                              Action onCommitted,
                                              RectTransform source,
                                              RectTransform iconPrefabOverride,
                                              Func<UniTask> onCommittedAsync = null)
    {
        try
        {
            if (qty <= 0) { onCommitted?.Invoke(); if (onCommittedAsync != null) await onCommittedAsync(); return; }
            if (!HudRegistry.TryGetActive(walletType, out var hud))
            {
                Debug.LogWarning($"[RewardFX] Active HUD not found for {walletType}");
                onCommitted?.Invoke(); if (onCommittedAsync != null) await onCommittedAsync(); return;
            }
            
            var iconPrefab = iconPrefabOverride
                           ?? (walletType == WalletType.Keys ? keyIconPrefabRect : lpIconPrefabRect);

            if (!iconPrefab || !hud.IconsLayer || !hud.IconRect || !hud.AmountLabel)
            {
                Debug.LogWarning("[RewardFX] Missing iconPrefab or HUD refs; skipping FX.");
                onCommitted?.Invoke(); if (onCommittedAsync != null) await onCommittedAsync(); return;
            }
            
            // ✅ use our own top-most FX layer as the IconsParent
            var iconsParent = fxLayer ? fxLayer : (RectTransform)transform;
            
            // Pick a safe source RT: explicit source → fxLayer → HUD icon
            RectTransform srcRT =
                source != null ? source :
                    (fxLayer != null ? fxLayer : hud.IconRect);
            
            Vector3 srcWorld = WorldCenterHelper(srcRT);
            
            var req = new RewardFxRequest
            {
                IconPrefab  = iconPrefab,
                IconsParent = iconsParent,
                TargetIcon  = hud.IconRect,
                TargetLabel = hud.AmountLabel,
                Amount      = qty,
                Source      = source ?? fxLayer ?? hud.IconRect, // sensible fallback chain
                SourceWorld = srcWorld
                // (Optional) you can set Sfx / Tick / Spawn / Whoosh overrides here too
                // Sfx = sfx, TickClip = tickClip, SpawnClip = spawnClip, WhooshClip = whooshClip
            };

            await PlayAsync(req);

            onCommitted?.Invoke();
            if (onCommittedAsync != null) await onCommittedAsync();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
    
    async UniTask PlayAsync(RewardFxRequest req)
    {
        if (req.Amount <= 0 || req.IconPrefab == null || req.IconsParent == null || req.TargetIcon == null)
            return;

        var label = req.TargetLabel ? req.TargetLabel : defaultLabel;
        if (!label) return;

        var tClip = req.TickClip  != SfxId.None ? req.TickClip  : SfxId.MoneyTick;
        var spClip = req.SpawnClip!= SfxId.None ? req.SpawnClip : SfxId.MoneySpawn;
        var whClip = req.WhooshClip!= SfxId.None? req.WhooshClip: SfxId.Woosh;

        float flyT  = req.FlyTime.HasValue       ? req.FlyTime.Value       : flyTime;
        float stag  = req.PerIconStagger.HasValue? req.PerIconStagger.Value: perIconStagger;

        var srcRT  = req.Source ? req.Source : req.TargetIcon;
        var srcW   = req.SourceWorld.HasValue ? req.SourceWorld.Value : WorldCenterOf(srcRT);
        var dstW   = req.TargetWorld.HasValue ? req.TargetWorld.Value : WorldCenterOf(req.TargetIcon);

        Vector2 srcPos = ToLocalOn(req.IconsParent, srcW);
        Vector2 dstPos = ToLocalOn(req.IconsParent, dstW);
        
        var labelBottomWorld = WorldBottomCenterOf(req.TargetLabel ? (RectTransform)req.TargetLabel.rectTransform : req.TargetIcon);
        var deltaBaseLocal   = ToLocalOn(req.IconsParent, labelBottomWorld);
        
        // how many icons
        // --- chunking (icon count and per-arrival value) ---
        int iconCount;
        if (req.PreferredChunkSize.HasValue && req.PreferredChunkSize.Value > 0)
        {
            // e.g., amount=2000, preferred=500 => 4 arrivals (clamped by maxIcons)
            iconCount = Mathf.Clamp(Mathf.CeilToInt(req.Amount / (float)req.PreferredChunkSize.Value), 1, maxIcons);
        }
        else
        {
            // default: up to maxIcons arrivals
            iconCount = Mathf.Clamp(req.Amount, 1, maxIcons);
        }
        int perIcon = Mathf.CeilToInt(req.Amount / (float)iconCount);
        
        /*int iconCount = Mathf.Clamp(req.Amount, 1, maxIcons);
        int perIcon   = Mathf.CeilToInt(req.Amount / (float)iconCount);*/

        // PHASE 1: pop all
        var icons    = new List<RectTransform>(iconCount);
        var pops     = new List<UniTask>(iconCount);

        for (int i = 0; i < iconCount; i++)
        {
            var icon = Instantiate(req.IconPrefab, req.IconsParent, false);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot     = new Vector2(0.5f, 0.5f);
            icon.gameObject.SetActive(true);
            icon.anchoredPosition = srcPos;
            icon.localScale       = Vector3.one * 0.7f;
            icon.SetAsLastSibling();

            Vector2 dir = (Random.insideUnitCircle + Vector2.up * burstUpBias).normalized;
            float   R   = Random.Range(burstMinRadius, burstMaxRadius);
            Vector2 burstPos = srcPos + dir * R;

            var popSeq = DOTween.Sequence().SetUpdate(useUnscaledTime)
                .Join(icon.DOAnchorPos(burstPos, burstTime).SetEase(Ease.OutCubic))
                .Join(icon.DOScale(burstOvershoot, burstTime * 0.55f).SetEase(Ease.OutBack))
                .Append(icon.DOScale(1f, burstTime * 0.35f).SetEase(Ease.OutQuad));

            float spin = UnityEngine.Random.Range(spinRange.x, spinRange.y) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            icon.DORotate(new Vector3(0,0,spin), burstTime, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetUpdate(useUnscaledTime);

            pops.Add(popSeq.AsyncWaitForCompletion().AsUniTask());
            icons.Add(icon);
        }
        _audio.PlaySfx(spClip);
        await UniTask.WhenAll(pops);

        // PHASE 2: straight to wallet
        _audio.PlaySfx(whClip);

        int current = ParseIntSafe(label.text);
        int target  = current + req.Amount;

        var flights = new List<UniTask>(iconCount);
        var baseScale = req.TargetIcon.localScale;

        for (int i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            var done = new UniTaskCompletionSource();

            float delay      = stag * i;
            Vector2 jitter   = UnityEngine.Random.insideUnitCircle * 6f;

            var flyTween = icon.DOAnchorPos(dstPos + jitter, flyT)
                           .SetDelay(delay).SetEase(Ease.InCubic).SetUpdate(useUnscaledTime);

            icon.DOScale(0.88f, flyT).SetDelay(delay).SetEase(Ease.InOutSine).SetUpdate(useUnscaledTime);
            icon.DORotate(new Vector3(0,0,icon.eulerAngles.z + UnityEngine.Random.Range(180f,360f)),
                          flyT, RotateMode.FastBeyond360)
                .SetDelay(delay).SetEase(Ease.Linear).SetUpdate(useUnscaledTime);

            flyTween.OnComplete(() =>
            {
                // bump target icon safely
                req.TargetIcon.DOKill(true);
                req.TargetIcon.localScale = baseScale;
                DOTween.Sequence().SetUpdate(useUnscaledTime)
                    .Append(req.TargetIcon.DOScale(baseScale * 1.12f, 0.08f).SetEase(Ease.OutQuad))
                    .Append(req.TargetIcon.DOScale(baseScale,           0.14f).SetEase(Ease.OutQuad));

                current = Mathf.Min(current + perIcon, target);
                label.text = current.ToString();
                _audio.PlaySfx(tClip);
                
                // NEW: floating "+perIcon" just under the label
                SpawnDeltaText(perIcon, req, deltaBaseLocal);
                
                Destroy(icon.gameObject);
                done.TrySetResult();
            });

            flights.Add(done.Task);
        }

        await UniTask.WhenAll(flights);
        label.text = target.ToString();
    }

    public async UniTask PlayAdvanced(RewardFxRequest req, Action onCommitted, Func<UniTask> onCommittedAsync)
    {
        await PlayAsync(req);
        onCommitted?.Invoke();
        if (onCommittedAsync != null) await onCommittedAsync();
    }
    
    void SpawnDeltaText(int delta, RewardFxRequest req, Vector2 baseLocalPos)
    {
        if (!deltaTextPrefab || !req.IconsParent) return;

        // instance (center anchors/pivot recommended on the prefab)
        var t = Instantiate(deltaTextPrefab);
        var rt = t.GetComponent<RectTransform>();
        rt.SetParent(req.IconsParent, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 1f); // top-center so it sits "under" the label nicely

        // place slightly under label with a tiny horizontal jitter
        float jx = Random.Range(-deltaXJitter, deltaXJitter);
        rt.anchoredPosition = baseLocalPos + deltaUnderOffset + new Vector2(jx, 0f);

        // set text & start invisible
        t.text = $"+{delta}";
        var col = t.color; col.a = 0f; t.color = col;
        rt.localScale = Vector3.one * 0.92f;

        // animate: quick pop-in, drift up, fade out, destroy
        var s = DOTween.Sequence().SetUpdate(useUnscaledTime);
        s.Append(t.DOFade(1f, deltaIn))
            .Join(rt.DOScale(deltaScaleIn, deltaIn))
            .Append(rt.DOAnchorPosY(rt.anchoredPosition.y + deltaRise, deltaDuration - deltaIn - deltaOut).SetEase(Ease.OutSine))
            .Append(t.DOFade(0f, deltaOut))
            .OnComplete(() => Destroy(t.gameObject));
    }

    static int ParseIntSafe(string s)
    {
        if (int.TryParse(System.Text.RegularExpressions.Regex.Replace(s ?? "0", "[^0-9]", ""), out var v))
            return v;
        return 0;
    }
    
    // Returns the root canvas and the correct camera for a RectTransform
    (Canvas canvas, Camera cam) GetCanvasAndCamera(RectTransform rt)
    {
        var c = rt ? rt.GetComponentInParent<Canvas>(true) : null;
        if (!c) return (null, null);
        var cam = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
        return (c, cam);
    }

    // Convert a RectTransform's world position to a local point in a *different* parent (cross-canvas safe)
    Vector2 ToLocalPointOn(RectTransform from, RectTransform toParent)
    {
        var (_, fromCam) = GetCanvasAndCamera(from);
        var (toCanvas, toCam) = GetCanvasAndCamera(toParent);

        // 1) world → screen using the *source* canvas camera
        var screen = RectTransformUtility.WorldToScreenPoint(fromCam, from.position);

        // 2) screen → local in the *destination* parent using the *destination* canvas camera
        RectTransformUtility.ScreenPointToLocalPointInRectangle(toParent, screen, toCam, out var local);
        return local;
    }
    
    // Center of a RectTransform in world space (robust against anchors/pivots)
    static Vector3 WorldCenterOf(RectTransform rt)
    {
        if (!rt) return Vector3.zero;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return (c[0] + c[2]) * 0.5f; // (min + max) / 2
    }

    // Convert a world point into a local point on the given RectTransform parent
    static Vector2 ToLocalOn(RectTransform parent, Vector3 worldPoint)
    {
        return (Vector2)parent.InverseTransformPoint(worldPoint);
    }
    
    static Vector3 WorldCenterHelper(RectTransform rt) => WorldCenterOf(rt);
    
    static Vector3 WorldBottomCenterOf(RectTransform rt)
    {
        if (!rt) return Vector3.zero;
        var c = new Vector3[4];
        rt.GetWorldCorners(c); // 0=BL,1=TL,2=TR,3=BR
        return (c[0] + c[3]) * 0.5f; // bottom middle
    }
}


