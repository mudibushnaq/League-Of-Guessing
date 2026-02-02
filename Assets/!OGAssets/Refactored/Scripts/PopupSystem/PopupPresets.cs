public static class PopupPresets
{
    public static PopupRequest NotEnoughLP(int needed, int current) => new PopupRequest {
        Title = "Not enough LP",
        Message = "You need {needed} LP (you have {current}).",
        Style = PopupStyle.Error,
        Buttons = new() {
            new PopupButton { Label = "Get LP", IsPrimary = true, AutoClose = true },
            new PopupButton { Label = "Cancel", IsPrimary = false, AutoClose = true }
        }
    }.WithToken("needed", needed).WithToken("current", current);

    public static PopupRequest ShopUI() => new PopupRequest
    {
        Title = "Shop",
        Message = "Shop UI",
        Style = PopupStyle.Info,
        TemplateMode = PopupTemplateMode.AddressableCanvas,
        AddressableCanvasKey = "uishoppopup_View",
        Payload = new CatalogShopUIGrid.Args { PricePerKey = CurrencyStore.LpPerKey },
    };
    
    public static PopupRequest ShopUIGameObject() => new PopupRequest
    {
        Title = "Shop",
        Message = "Shop UI",
        Style = PopupStyle.Info,
        TemplateMode = PopupTemplateMode.GameObjectView,
        AddressableCanvasKey = "9999",
        Payload = new CatalogShopUIGrid.Args { PricePerKey = CurrencyStore.LpPerKey },
    };
    
    public static PopupRequest NotEnoughKeys(int needed, int current) => new PopupRequest {
        Title = "Not enough Keys",
        Message = "You need {needed} keys (you have {current}).",
        Style = PopupStyle.Warning,
        Buttons = new() {
            new PopupButton { Label = "Buy Keys", IsPrimary = true, AutoClose = true },
            new PopupButton { Label = "Cancel", IsPrimary = false, AutoClose = true }
        }
    }.WithToken("needed", needed).WithToken("current", current);

    public static PopupRequest NoLevels() => new PopupRequest
    {
        Title = "No levels",
        Message = "No levels loaded; returning to Menu.",
        Style = PopupStyle.Success,
        AutoDismissSeconds = 1.5f
    };
    
    public static PopupRequest Victory() => new PopupRequest
    {
        Title = "Victory",
        Message = "Victory",
        Style = PopupStyle.Confirm,
        AddressableCanvasKey = "uipopupVictory_View",
        TemplateMode = PopupTemplateMode.AddressableCanvas,
    };
    
    public static PopupRequest CantUnlock() => new PopupRequest
    {
        Title = "Cant Unlock",
        Message = "Cant unlock this skill, already visible",
        Style = PopupStyle.Error,
        AutoDismissSeconds = 1.5f
    };
    
    // Use a whole Addressable CANVAS prefab (with its own blocker + view inside)
    public static PopupRequest NotEnoughKeysMsg(int needed, int current) => new PopupRequest {
        Title = "Not enough Keys",
        Message = "You need {needed} keys (you have {current}).",
        Style = PopupStyle.Error,
        TemplateMode = PopupTemplateMode.AddressableCanvas,
        AddressableCanvasKey = "ui/popup/NotEnoughKeys_View", // canvas prefab key
        CacheTemplate = false,
        AutoDismissSeconds = 1.5f
    }.WithToken("needed", needed).WithToken("current", current);

    public static PopupRequest ConfirmSpendLP(int cost, string action) => new PopupRequest {
        Title = "Confirm",
        Message = "Spend {cost} LP to {action}?",
        Style = PopupStyle.Confirm,
        Buttons = new() {
            new PopupButton { Label = "Yes", IsPrimary = true, AutoClose = true },
            new PopupButton { Label = "No",  IsPrimary = false, AutoClose = true }
        }
    }.WithToken("cost", cost).WithToken("action", action);
}