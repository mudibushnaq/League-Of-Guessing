using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SlotKind { Letter, FixedChar, Spacer }

public class SlotView : MonoBehaviour
{
    [Header("UI")]
    public SlotKind kind;
    public TMP_Text label;         // show entered letter or fixed char
    public Image bg;               // slot background (hide for Spacer)
    public Image glow;
    public Button button;          // we make letter slots clickable

    // Runtime
    public char fixedChar;         // '.' or '\'' when kind == FixedChar
    public char current;           // player-entered letter (Letter only)

    public void InitLetter(Action onClick)
    {
        kind = SlotKind.Letter;
        if (bg) bg.enabled = true;
        if (label) label.text = "";   // empty at start
        current = '\0';
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
            button.interactable = true;
        }
        SetGlow(false);
    }
    
    public void InitFixed(char c)
    {
        kind = SlotKind.FixedChar;
        fixedChar = c;
        current = '\0';
        if (bg) bg.enabled = true;
        if (label) label.text = c.ToString();
        if (button) button.interactable = false; // not clickable
        SetGlow(false);
    }
    
    public void InitSpacer(float width)
    {
        kind = SlotKind.Spacer;
        current = '\0';
        if (bg) bg.enabled = false;
        if (label) label.text = "";
        if (button) button.interactable = false;

        var le = GetComponent<LayoutElement>();
        if (!le) le = gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        le.flexibleWidth = 0;
        SetGlow(false);
    }

    public bool IsEmptyLetter => kind == SlotKind.Letter && current == '\0';

    public void SetLetter(char c)  // for Letter kind
    {
        current = c;
        if (label) label.text = c.ToString().ToUpper();
    }

    public void ClearLetter()
    {
        current = '\0';
        if (label) label.text = "";
    }
    
    public void SetGlow(bool on)
    {
        if (glow) glow.enabled = on;
    }
}