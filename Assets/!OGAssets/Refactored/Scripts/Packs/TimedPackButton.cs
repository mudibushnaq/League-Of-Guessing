// Assets/!OGAssets/Refactored/Scripts/Packs/TimedPackButton.cs
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class TimedPackButton : MonoBehaviour
{
    [Header("Data")]
    public PackDefinition pack;

    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text sub;  // shows timer or status
    [SerializeField] private TMP_Text amountText;   // NEW: shows "x5" (keys amount)
    
    [Header("Texts")]
    [SerializeField] private string readyFmt   = "Get {0}";
    [SerializeField] private string waitingFmt = "{0}";
    [SerializeField] private string rewardNameForUi = "5 Keys"; // optional display label

    [Inject] private IPackService _packs;
    private IAdsService _ads;
    [Inject] private IRewardFX _fx;
    
    private bool _busy;
    private CancellationTokenSource _cts;
    
    void Awake()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            gameObject.SetActive(false);
            return;
        }

        if (amountText) amountText.text = BuildAmountLabel(pack);
        
        if (!button) button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        // If your scene-wide inject sweep isn’t guaranteed, do a local fallback:
        
        if (title && pack) title.text = string.IsNullOrEmpty(pack.displayName) ? pack.packId : pack.displayName;

        _cts = new CancellationTokenSource();
    }

    async void Start()
    {
        // Optional: warm ad
        if (pack && pack.gate == PackGateType.UnityAdsRewarded && _ads != null)
            await _ads.PreloadRewardedAsync();

        _ = TimerLoop(_cts.Token);
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(OnClick);
        _cts?.Cancel();
        _cts?.Dispose();
    }

    async UniTaskVoid TimerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UpdateUI();
            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
        }
    }

    void UpdateUI()
    {
        if (pack == null || _packs == null)
        {
            button.interactable = false;
            if (sub) sub.text = "Unavailable";
            return;
        }

        var can = _packs.CanClaim(pack, out var remaining, out _);
        var ready = can && !_busy;

        button.interactable = ready;

        if (sub)
        {
            if (ready)
            {
                sub.text = string.Format(readyFmt, string.IsNullOrEmpty(rewardNameForUi) ? PackRewardLabel(pack) : rewardNameForUi);
            }
            else
            {
                var t = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
                sub.text = string.Format(waitingFmt, Format(t));
            }
        }
    }

    async void OnClick()
    {
        if (_busy || _packs == null || pack == null) return;

        // Re-check cooldown at click time
        if (!_packs.CanClaim(pack, out _, out _)) return;

        _busy = true;
        UpdateUI();

        // Claim handles ad gate + reward
        bool ok = await _packs.ClaimAsync(pack, transform);

        // Warm next ad if gated
        if (ok && pack.gate == PackGateType.UnityAdsRewarded && _ads != null)
            await _ads.PreloadRewardedAsync();

        _busy = false;
        UpdateUI();
    }

    static string PackRewardLabel(PackDefinition def)
    {
        // Basic label (override with rewardNameForUi if you prefer)
        if (def.rewards == null || def.rewards.Count == 0) return "Reward";
        var r = def.rewards[0];
        return r.kind switch
        {
            RewardKind.Keys => $"{r.amount} Keys",
            RewardKind.LP => $"{r.amount} Coins",
            _ => "Reward"
        };
    }

    static string Format(TimeSpan t)
    {
        int total = Mathf.Clamp((int)Math.Ceiling(t.TotalSeconds), 0, 99*60 + 59);
        int m = total / 60;
        int s = total % 60;
        return $"{m:00}:{s:00}";
    }
    
    private static string BuildAmountLabel(PackDefinition def)
    {
        if (def == null || def.rewards == null || def.rewards.Count == 0)
            return string.Empty;

        // Prefer Keys; fall back to the first reward if no Keys exist.
        int keysTotal = 0;
        foreach (var r in def.rewards)
        {
            if (r.kind == RewardKind.Keys) keysTotal += Mathf.Max(0, r.amount);
        }
        if (keysTotal > 0) return $"x{keysTotal}";

        // Fallback: display the first reward generically
        var first = def.rewards[0];
        var name = first.kind switch
        {
            RewardKind.LP => "LP",
            RewardKind.Keys => "Keys",
            _ => "Reward"
        };
        return $"x{Mathf.Max(0, first.amount)}";
    }
}