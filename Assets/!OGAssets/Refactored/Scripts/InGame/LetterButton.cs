using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LetterButton : MonoBehaviour
{
    [Header("UI")] 
    public RectTransform thisRectTransform;
    public Button button;
    public TMP_Text text;
    public Image Glow;
    public Image background; // optional: assign for visual selected state
    public CanvasGroup cg;
    public char Letter => _letter;
    private char _letter;
    private UnityAction<char, LetterButton> _onPressed;
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>(); // ensure it exists
    }

    public void Init(char c, UnityAction<char, LetterButton> onPressed)
    {
        _letter = c;
        if (text) text.text = c.ToString().ToUpper();
        _onPressed = onPressed;

        if (!button) button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onPressed?.Invoke(_letter, this));
        button.interactable = true;
        
        // start visible
        SetHidden(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (background)
        {
            // simple visual cue: alpha tweak (adjust to taste)
            var c = background.color;
            c.a = selected ? 0.6f : 1f;
            background.color = c;
        }
        // keep button.interactable = true so it can be clicked again
        if (button) button.interactable = true;
    }
    
    // Hide visually but keep the GO active so GridLayout keeps the slot
    public void SetHidden(bool hidden)
    {
        if (cg)
        {
            cg.alpha = hidden ? 0f : 1f;
            cg.blocksRaycasts = !hidden;
        }
        if (button) button.interactable = !hidden;

        // also turn off graphics so it’s truly invisible (optional)
        if (background) background.enabled = !hidden;
        if (text) text.enabled = !hidden;
    }
    
    public void SetVisible(bool visible)
    {
        // Hides the whole key when used; shows again when returned
        gameObject.SetActive(visible);
    }

    public Image GetKeyGlow()
    {
        return Glow;
    }
    
    public void SetKeyGlow(bool on)
    {
        if (GetKeyGlow() != null) GetKeyGlow().enabled = on;
    }
    
    public void PulseKey(float dur, bool keepGlow)
    {
        thisRectTransform.DOKill();
        thisRectTransform.localScale = Vector3.one;

        SetKeyGlow(true);
        var t = thisRectTransform.DOScale(1.12f, dur).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
        if (!keepGlow) t.OnComplete(() => SetKeyGlow(false));
    }
    
    public char ExtractLetter()
    {
        if (this == null) return '\0';
        if (Letter != '\0') return char.ToUpperInvariant(Letter);
        var label = text;
        return (label != null && !string.IsNullOrEmpty(label.text))
            ? char.ToUpperInvariant(label.text[0]) : '\0';
    }
}