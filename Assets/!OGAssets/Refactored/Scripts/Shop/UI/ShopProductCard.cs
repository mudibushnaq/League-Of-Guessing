using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopProductCard : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] Image cardBg;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text subtitleLabel;
    [SerializeField] TMP_Text priceLabel;
    [SerializeField] Button buyButton;
    [SerializeField] Image  buyButtonBg;
    [SerializeField] TMP_Text buyButtonText;
    [SerializeField] GameObject ownedPill;
    [SerializeField] Image badgeBg;
    [SerializeField] TMP_Text badgeText;
    [SerializeField] CanvasGroup cg;

    string _catalogId;
    Func<string, UniTask<bool>> _onBuy;

    void Reset() { cg = GetComponent<CanvasGroup>(); }

    public void ApplyTheme(ShopTheme t)
    {
        if (!t) return;
        if (cardBg && t.cardSprite) cardBg.sprite = t.cardSprite;
        if (buyButtonBg && t.buttonSprite) buyButtonBg.sprite = t.buttonSprite;
        if (badgeBg && t.badgeSprite) badgeBg.sprite = t.badgeSprite;
        if (buyButtonBg) buyButtonBg.color = (Color)t.price;
        if (badgeBg) badgeBg.color = (Color)t.badge;
    }

    public void Bind(string catalogId, string title, string subtitle, string price,
                     Sprite iconSprite, string badge, bool owned,
                     Func<string, UniTask<bool>> onBuy)
    {
        _catalogId = catalogId;
        _onBuy = onBuy;

        if (titleLabel)    titleLabel.text = title ?? catalogId;
        if (subtitleLabel) subtitleLabel.text = subtitle ?? "";
        if (priceLabel)    priceLabel.text = price ?? "";
        if (icon)          icon.sprite = iconSprite;
        if (badgeBg)       badgeBg.gameObject.SetActive(!string.IsNullOrEmpty(badge));
        if (badgeText)     badgeText.text = badge ?? "";
        if (ownedPill)     ownedPill.SetActive(owned);
        if (buyButton)     buyButton.interactable = !owned;

        if (buyButton) buyButton.onClick.RemoveAllListeners();
        if (buyButton) buyButton?.onClick.AddListener(() => Buy().Forget());

        // spawn anim
        if (cg)
        {
            cg.alpha = 0f;
            transform.localScale = Vector3.one * 0.98f;
            DOTween.Sequence()
                .Append(cg.DOFade(1f, 0.18f))
                .Join(transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
        }

        if (buyButton)
        {
            // hover/press
            var tr = (RectTransform)transform;
            buyButton.transition = Selectable.Transition.None;
            buyButton.onClick.AddListener(() =>
            {
                tr.DOKill();
                DOTween.Sequence()
                    .Append(tr.DOScale(0.985f, 0.06f))
                    .Append(tr.DOScale(1f, 0.12f).SetEase(Ease.OutBack));
            });
        }
    }

    async UniTaskVoid Buy()
    {
        if (_onBuy == null) return;
        buyButton.interactable = false;
        try
        {
            var ok = await _onBuy.Invoke(_catalogId);
            // TODO toast on ok/fail
        }
        finally { buyButton.interactable = true; }
    }
}
