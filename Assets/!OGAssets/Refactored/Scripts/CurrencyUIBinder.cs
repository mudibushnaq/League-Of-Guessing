using System;
using TMPro;
using UnityEngine;

public class CurrencyUIBinder : MonoBehaviour
{
    // Optional explicit refs (nice for prefab wiring). If left empty, we’ll look them up via HudRegistry.
    [SerializeField] private WalletHud lpHudOverride;
    [SerializeField] private WalletHud keysHudOverride;

    private IWalletHud _lpHud;
    private IWalletHud _keysHud;

    // keep delegates so we unsubscribe correctly
    private Action<int> _onLPChanged;
    private Action<int> _onKeysChanged;

    void Awake()
    {
        /*CurrencyStore.InitIfNeeded();

        ResolveHuds();
        RefreshAll();

        _onLPChanged   = v => { if (_lpHud?.AmountLabel)   _lpHud.AmountLabel.text   = v.ToString(); };
        _onKeysChanged = v => { if (_keysHud?.AmountLabel) _keysHud.AmountLabel.text = v.ToString(); };

        CurrencyStore.OnLPChanged   += _onLPChanged;
        CurrencyStore.OnKeysChanged += _onKeysChanged;*/
    }

    void OnDestroy()
    {
        if (_onLPChanged   != null) CurrencyStore.OnLPChanged   -= _onLPChanged;
        if (_onKeysChanged != null) CurrencyStore.OnKeysChanged -= _onKeysChanged;
    }

    private void ResolveHuds()
    {
        // Prefer explicit overrides (serialized), else pull from registry
        /*_lpHud   = lpHudOverride ? lpHudOverride : (HudRegistry.TryGet(WalletType.LP, out var lp) ? lp : null);
        _keysHud = keysHudOverride ? keysHudOverride : (HudRegistry.TryGet(WalletType.Keys, out var k) ? k : null);

        if (_lpHud == null)   Debug.LogWarning("[CurrencyUIBinder] LP HUD not found.");
        if (_keysHud == null) Debug.LogWarning("[CurrencyUIBinder] Keys HUD not found.");*/
    }

    private void RefreshAll()
    {
        if (_lpHud?.AmountLabel)   _lpHud.AmountLabel.text   = CurrencyStore.LP.ToString();
        if (_keysHud?.AmountLabel) _keysHud.AmountLabel.text = CurrencyStore.Keys.ToString();
    }
}