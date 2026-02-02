using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PopupStyle { Info, Warning, Error, Confirm, Success }
public enum PopupResult { None, Primary, Secondary, Tertiary, Closed }
public enum PopupTemplateMode { DefaultChannelView, AddressableView, AddressableCanvas,GameObjectView }


[System.Serializable]
public struct PopupButton
{
    public string Label;           // Localized key or plain text
    public bool IsPrimary;         // Style hint
    public bool AutoClose;         // Close on click?
    public Action OnClick;         // Optional side-effect before resolve
}

public sealed class PopupRequest
{
    public string Title;
    public string Message;
    public Sprite Icon;
    public PopupStyle Style = PopupStyle.Info;
    public bool IsModal = true;
    public bool DismissOnBackground = false;
    public float? AutoDismissSeconds = null;
    public Button PressedToButton;
    public List<PopupButton> Buttons = new();
    public Dictionary<string, object> Tokens;

    // NEW: template selection
    public PopupTemplateMode TemplateMode = PopupTemplateMode.DefaultChannelView;
    public GameObject gm;
    public string AddressableViewKey;    // e.g. "ui/popup/purchase_view"
    public string AddressableCanvasKey;  // e.g. "ui/canvas/CanvasShopPopup"
    public bool CacheTemplate = true;    // keep loaded canvas instance for reuse?
    public object Payload;               // <— ADD THIS (optional data for content prefabs)
    public PopupRequest WithToken(string k, object v)
    {
        Tokens ??= new Dictionary<string, object>();
        Tokens[k] = v; return this;
    }
}