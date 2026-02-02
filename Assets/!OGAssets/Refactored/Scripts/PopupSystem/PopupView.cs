using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class PopupView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject iconRoot;
    [SerializeField] private Button backgroundDismissButton;

    [Header("Buttons")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TMP_Text secondaryLabel;
    [SerializeField] private Button tertiaryButton;
    [SerializeField] private TMP_Text tertiaryLabel;

    private UniTaskCompletionSource<PopupResult> _tcs;

    public void Bind(
        string title,
        string message,
        Sprite icon,
        PopupStyle style,
        List<PopupButton> buttons,
        UniTaskCompletionSource<PopupResult> tcs)
    {
        _tcs = tcs;

        if(titleText) titleText.text = title ?? "";
        if(messageText) messageText.text = message ?? "";

        if (icon != null)
        {
            iconRoot.SetActive(true);
            iconImage.sprite = icon;
        }
        else
        {
            if(iconRoot) iconRoot.SetActive(false);
        }

        // Style hook (colors, icons, sounds)
        ApplyStyle(style);

        // Wire buttons
        SetupButtons(buttons);

        // Background dismiss handler is set externally if needed
        if (backgroundDismissButton)
        {
            backgroundDismissButton.onClick.RemoveAllListeners();
            backgroundDismissButton.onClick.AddListener(() => Resolve(PopupResult.Closed));
        }

        // Optional: fade/scale in animation with DOTween if you like
        // transform.localScale = Vector3.one * 0.95f; transform.DOScale(1f, 0.15f);
    }

    private void ApplyStyle(PopupStyle style)
    {
        // Hook up theme changes here (header color, icon tint, etc.)
        // Kept minimal for brevity.
    }

    private void SetupButtons(List<PopupButton> src)
    {
        // Hide all first
        if(primaryButton) primaryButton.gameObject.SetActive(false);
        if(secondaryButton) secondaryButton.gameObject.SetActive(false);
        if(tertiaryButton) tertiaryButton.gameObject.SetActive(false);

        // We’ll map by priority: first IsPrimary goes primary slot; others fill next
        var list = src ?? new List<PopupButton>();
        var ordered = new List<PopupButton>(list.Count);
        ordered.AddRange(list.FindAll(b => b.IsPrimary));
        ordered.AddRange(list.FindAll(b => !b.IsPrimary));

        int slot = 0;
        foreach (var b in ordered)
        {
            if (slot == 0) BindButton(primaryButton, primaryLabel, b, PopupResult.Primary);
            else if (slot == 1) BindButton(secondaryButton, secondaryLabel, b, PopupResult.Secondary);
            else if (slot == 2) BindButton(tertiaryButton, tertiaryLabel, b, PopupResult.Tertiary);
            slot++;
            if (slot >= 3) break;
        }
    }

    private void BindButton(Button btn, TMP_Text label, PopupButton data, PopupResult result)
    {
        btn.gameObject.SetActive(true);
        label.text = data.Label ?? "";
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            try { data.OnClick?.Invoke(); } catch { /* swallow */ }
            if (data.AutoClose) Resolve(result);
        });
    }

    private void Resolve(PopupResult r)
    {
        if (_tcs == null) return;
        var t = _tcs;
        _tcs = null;
        t.TrySetResult(r);
    }
}
