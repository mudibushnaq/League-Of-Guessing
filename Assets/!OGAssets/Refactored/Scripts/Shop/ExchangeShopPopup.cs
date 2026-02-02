using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using Zenject;

public class ExchangeShopPopup : MonoBehaviour, IPopupContent
{
    [SerializeField] private Slider amountSlider;
    [SerializeField] private Button maxButton;
    [SerializeField] private Button confirmButton;

    [SerializeField] private TMP_Text pricePerKeyText;   // "10 IP / key"
    [SerializeField] private TMP_Text selectedKeysText;  // "x Keys"
    [SerializeField] private TMP_Text costIpText;        // "Cost: y IP"
    
    private int _pricePerKey;
    private UniTaskCompletionSource<PopupResult> _tcs;
    
    [Inject] private IRewardFX _fx;
    
    // Optional payload for passing config
    public sealed class Args { public int PricePerKey = 10; }

    // ---- Public API ---------------------------------------------------------
    public void Bind(PopupRequest request, UniTaskCompletionSource<PopupResult> tcs)
    {
        _tcs = tcs;

        // Resolve price per key from payload (preferred) or tokens, else default
        _pricePerKey = CurrencyStore.LpPerKey;
        if (request?.Payload is Args a) _pricePerKey = Mathf.Max(1, a.PricePerKey);
        else if (request?.Tokens != null && request.Tokens.TryGetValue("PricePerKey", out var v) && v is int iv)
            _pricePerKey = Mathf.Max(1, iv);

        // Init UI
        if (pricePerKeyText) pricePerKeyText.SetText($"{_pricePerKey} LP / key");

        amountSlider.wholeNumbers = true;
        amountSlider.minValue = 0;
        amountSlider.maxValue = MaxAffordableKeys();
        amountSlider.value = Mathf.Min(amountSlider.value, amountSlider.maxValue);

        RefreshUI();

        // Wire events (clear first, in case prefab is cached)
        amountSlider.onValueChanged.RemoveAllListeners();
        maxButton.onClick.RemoveAllListeners();
        //cancelButton.onClick.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();

        amountSlider.onValueChanged.AddListener(_ => RefreshUI());
        maxButton.onClick.AddListener(OnMaxClicked);
        //cancelButton.onClick.AddListener(OnCancelClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private int MaxAffordableKeys() => Mathf.Max(0, CurrencyStore.LP / _pricePerKey);

    private void RefreshUI()
    {
        // Recompute in case balances changed while open
        int lp  = CurrencyStore.LP;
        int qty = Mathf.FloorToInt(amountSlider.value);
        int cost = qty * _pricePerKey;

        selectedKeysText?.SetText($"{qty}");
        costIpText?.SetText($"{cost}");

        confirmButton.interactable = qty > 0 && cost <= lp;
    }

    private void OnMaxClicked()
    {
        amountSlider.value = MaxAffordableKeys();
        RefreshUI();
    }

    private void OnCancelClicked()
    {
        // Reset UI, then close with 0 purchased
        amountSlider.value = 0;
        RefreshUI();
        _tcs?.TrySetResult(PopupResult.Secondary);
    }

    private void OnConfirmClicked()
    {
        int qty  = Mathf.FloorToInt(amountSlider.value);
        int cost = qty * CurrencyStore.LpPerKey;

        if (qty <= 0)
        {
            _tcs?.TrySetResult(PopupResult.Secondary);
            return;
        }
        
        // Balances might have changed while open
        if (cost > CurrencyStore.LP || !CurrencyStore.TrySpendLP(cost))
        {
            amountSlider.maxValue = MaxAffordableKeys();
            amountSlider.value = Mathf.Min(amountSlider.value, amountSlider.maxValue);
            RefreshUI();
            return;
        }
        
        var src = confirmButton ? confirmButton.GetComponent<RectTransform>() : null;
        
        _fx.PlayGainFX(
            qty,
            WalletType.Keys,
            onCommittedAsync: () => {
                CurrencyStore.AddKeys(qty);
                return UniTask.CompletedTask;   // or: return UniTask.Yield();
            },
            source: src
        );
        
        // If you want to play SFX/FX, do it here before resolving
        _tcs?.TrySetResult(PopupResult.Primary);
    }
    
    private void OnDisable()
    {
        // Clean up listeners in case this prefab is cached between uses
        amountSlider.onValueChanged.RemoveAllListeners();
        maxButton.onClick.RemoveAllListeners();
        //cancelButton.onClick.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();
    }
}
