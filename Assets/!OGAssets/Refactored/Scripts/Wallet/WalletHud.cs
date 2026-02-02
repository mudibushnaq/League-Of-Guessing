using System;
using TMPro;
using UnityEngine;

public class WalletHud : MonoBehaviour, IWalletHud
{
    [Header("Identity")]
    [SerializeField] private WalletType type;
    [Header("Visuals")]
    [SerializeField] private RectTransform iconRect;   // e.g., the little coin/key image
    [SerializeField] private TMP_Text amountLabel;     // the text next to it
    [SerializeField] private RectTransform iconsLayer; // usually your HUD canvas root
    
    [Header("RewardFX target selection")]
    [SerializeField] private bool rewardFXActivated = false; // set TRUE on the HUD you want FX to land to
    [SerializeField] private int  priority = 0;              // if multiple are true, highest wins
    
    public WalletType   Type        => type;
    public RectTransform IconRect   => iconRect;
    public TMP_Text     AmountLabel => amountLabel;
    public RectTransform IconsLayer => iconsLayer;
    public bool RewardFXActivated   => rewardFXActivated;
    public int  Priority            => priority;
    
    // cache delegates for proper unsubscribe
    private Action<int> _onChanged;
    
    void Awake()
    {
        HudRegistry.Register(this);
        // subscribe to the right currency stream
        if (type == WalletType.LP)
        {
            _onChanged = v => SetAmount(v);
            CurrencyStore.OnLPChanged += _onChanged;
            SetAmount(CurrencyStore.LP);
        }
        else
        {
            _onChanged = v => SetAmount(v);
            CurrencyStore.OnKeysChanged += _onChanged;
            SetAmount(CurrencyStore.Keys);
        }

        // Optional: ensure iconsLayer exists; otherwise RewardFX will fall back to its own fxLayer
        if (!iconsLayer)
            Debug.LogWarning($"{name}: IconsLayer not assigned; RewardFX will use its own fxLayer.");
    }
    
    void OnDestroy()
    {
        if (_onChanged != null)
        {
            if (type == WalletType.LP)    CurrencyStore.OnLPChanged   -= _onChanged;
            else                          CurrencyStore.OnKeysChanged -= _onChanged;
        }
        HudRegistry.Unregister(this);
    }

    public void SetAmount(int value)
    {
        if (amountLabel) amountLabel.text = value.ToString();
        // (Optional) bump animation here if you want on every change.
    }
    
}