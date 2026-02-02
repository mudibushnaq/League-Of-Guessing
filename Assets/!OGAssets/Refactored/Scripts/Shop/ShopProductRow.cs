#nullable enable
using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopProductRow : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text subtitleLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button   buyButton;

    string _catalogId;
    Func<string, UniTask<bool>> _onBuy;

    public void Bind(string catalogId, string title, string subtitle, string priceText,
        Func<string, UniTask<bool>> onBuy)
    {
        _catalogId = catalogId;
        _onBuy = onBuy;

        if (titleLabel)    titleLabel.text = title ?? catalogId;
        if (subtitleLabel) subtitleLabel.text = subtitle ?? "";
        if (priceLabel)    priceLabel.text = priceText ?? "";

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => Buy().Forget());
        buyButton.interactable = true;
    }

    async UniTaskVoid Buy()
    {
        buyButton.interactable = false;
        try
        {
            if (_onBuy != null)
            {
                var ok = await _onBuy.Invoke(_catalogId);
                // Optional UI feedback on ok/fail here
            }
        }
        finally
        {
            buyButton.interactable = true;
        }
    }
}