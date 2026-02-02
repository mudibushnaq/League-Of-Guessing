using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class RewardFxTester : MonoBehaviour
{
    [Header("What to test")]
    public WalletType walletType = WalletType.Keys;
    [Min(1)] public int amount = 5;

    [Header("Optional source (where coins spawn from)")]
    public RectTransform source;                 // e.g., assign your Shop Confirm button here

    [Header("Preferred chunking (optional)")]
    public bool usePreferredChunkSize = false;
    [Min(1)] public int preferredChunkSize = 500;

    [Header("Icon override (optional)")]
    public RectTransform iconPrefabOverride;     // leave null to use RewardFX defaults

    [Header("Commit to CurrencyStore after FX ends?")]
    public bool commitToCurrency = true;

    [Header("Hotkeys (optional)")]
    public bool enableHotkey = false;
    public KeyCode triggerKey = KeyCode.F9;
    
    [Inject] private IRewardFX _fx;

    void Update()
    {
        if (enableHotkey && Input.GetKeyDown(triggerKey))
            PlayNow(); // quick hotkey trigger
    }

    // -------- Buttons / Context Menu --------
    [ContextMenu("Test: Play (no commit)")]
    public void PlayNoCommit()
    {
        commitToCurrency = false;
        PlayNow();
    }

    [ContextMenu("Test: Play (commit)")]
    public void PlayCommit()
    {
        commitToCurrency = true;
        PlayNow();
    }

    // Wire this to a UI Button
    public void PlayNow()
    {
        if (_fx == null)
        {
            Debug.LogWarning("RewardFX is missing in scene.");
            return;
        }

        // Find active HUD for the target wallet
        if (!HudRegistry.TryGetActive(walletType, out var hud))
        {
            // If your HudRegistry doesn’t have TryGetActive, replace with TryGet.
            Debug.LogWarning($"No active HUD for {walletType}. Make sure a WalletHud is registered and marked active.");
            return;
        }

        // Build the request
        var req = new RewardFxRequest
        {
            IconPrefab   = iconPrefabOverride,            // null => RewardFX will use its default icons by walletType
            IconsParent  = _fx != null ? _fx.Transform as RectTransform : hud.IconsLayer,
            TargetIcon   = hud.IconRect,
            TargetLabel  = hud.AmountLabel,
            Amount       = Mathf.Max(1, amount),
            Source       = source ? source : (hud.IconRect), // spawn from provided source or fallback to wallet icon
        };

        if (usePreferredChunkSize && preferredChunkSize > 0)
            req.PreferredChunkSize = preferredChunkSize;

        // Pick the correct commit action
        Action commit = null;
        if (commitToCurrency)
        {
            if (walletType == WalletType.Keys)
                commit = () => CurrencyStore.AddKeys(amount);
            else
                commit = () => CurrencyStore.AddLP(amount);
        }

        // If you want to ensure the top-most FX canvas parent is used:
        if (_fx != null && _fx.GameObject.TryGetComponent<RectTransform>(out var fxLayer))
            req.IconsParent = fxLayer;

        // Kick it!
        _fx.PlayAdvanced(req, commit, null);
    }

    // Helper for UI Button OnClick where you pass the button itself
    public void PlayFromButtonSource(Button btn)
    {
        source = btn ? btn.GetComponent<RectTransform>() : null;
        PlayNow();
    }
}
