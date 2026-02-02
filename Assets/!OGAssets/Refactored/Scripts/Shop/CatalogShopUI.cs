#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;   // ProductCatalog
using UnityEngine.UI;

public sealed class CatalogShopUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private ShopProductCard cardPrefab;
    [SerializeField] private SectionHeaderView sectionHeaderPrefab;

    [Header("Assets")]
    [SerializeField] private ShopTheme theme;
    [SerializeField] private ShopPresentation presentation;

    [Header("UI")]
    [SerializeField] private TMP_Text headerLabel;
    [SerializeField] private Button refreshButton;
    [SerializeField] private GameObject loadingSpinner;

    [Header("Payout subtypes")]
    [SerializeField] private string subtypeLp = "LP";
    [SerializeField] private string subtypeKeys = "Keys";

    [Zenject.Inject] IIapService _iap;

    async void Awake()
    {
        AutoWireIfMissing();

        if (loadingSpinner) loadingSpinner.SetActive(true);
        if (headerLabel) headerLabel.text = "Shop";

        if (refreshButton)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(() => BuildList().Forget());
        }

        await _iap.InitializeAsync();
        await BuildList();
        if (loadingSpinner) loadingSpinner.SetActive(false);
    }
    
    async UniTask BuildList()
    {
        // clear
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var pc = ProductCatalog.LoadDefaultCatalog();
        if (pc == null || pc.allProducts == null || pc.allProducts.Count == 0)
        {
            Debug.LogWarning("[Shop] Empty IAP Catalog.");
            return;
        }

        // Group by ProductType
        var groups = new Dictionary<ProductType, List<ProductCatalogItem>>
        {
            { ProductType.Consumable,    new List<ProductCatalogItem>() },
            { ProductType.NonConsumable, new List<ProductCatalogItem>() },
            { ProductType.Subscription,  new List<ProductCatalogItem>() }
        };

        foreach (var it in pc.allProducts)
            groups[it.type].Add(it);

        // Optional: sort featured / weight using presentation
        Comparison<ProductCatalogItem> sort = (a,b) =>
        {
            int wa = presentation != null && presentation.TryGet(a.id, out var ea) ? ea.sortWeight : 1000;
            int wb = presentation != null && presentation.TryGet(b.id, out var eb) ? eb.sortWeight : 1000;
            return wa.CompareTo(wb);
        };
        foreach (var kv in groups) kv.Value.Sort(sort);

        // Build sections
        await BuildSection("Consumables", groups[ProductType.Consumable]);
        await BuildSection("Unlocks", groups[ProductType.NonConsumable]);
        await BuildSection("Subscriptions", groups[ProductType.Subscription]);
        await UniTask.Yield();
    }
    
    async UniTask BuildSection(string header, List<ProductCatalogItem> items)
    {
        if (items == null || items.Count == 0) return;

        // header
        var sh = Instantiate(sectionHeaderPrefab, contentRoot, false);
        sh.gameObject.SetActive(true);
        sh.SetText(header);

        // cards
        foreach (var item in items)
        {
            var card = Instantiate(cardPrefab, contentRoot, false);
            card.gameObject.SetActive(true);
            card.ApplyTheme(theme);

            // presentational bits
            ShopPresentation.Entry entry = null;
            if (presentation != null)
                presentation.TryGet(item.id, out entry);

            var icon  = entry != null ? entry.icon      : null;
            var badge = entry != null ? entry.badgeText : null;

            // title/desc
            var title = item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Title)
                ? item.defaultDescription.Title
                : item.id;
            var subtitle = item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Description)
                ? item.defaultDescription.Description
                : FormatPayoutsShort(item.Payouts);

            // price (mock in editor / real on device)
            var price = _iap.GetLocalizedPriceById(item.id, "…");
            if (price == "…") price = await TryWaitForPrice(item.id, 1.5f, "…");

            // owned state (rudimentary: if not purchasable according to info, mark owned for non-consumables)
            bool owned = false;
            if (item.type == ProductType.NonConsumable)
            {
                var info = _iap.GetInfo(Enum.TryParse<IapProduct>(item.id, out var ep) ? ep : IapProduct.None);
                if (info.HasValue)
                {
                    // if your IapProductInfo has 'avail' bool, use that; else keep a separate availability map
                    // owned = !info.Value.avail;
                }
            }

            card.Bind(
                catalogId: item.id,
                title: title,
                subtitle: subtitle,
                price: price,
                iconSprite: icon,
                badge: badge,
                owned: owned,
                onBuy: async (id) =>
                {
                    var ok = await _iap.PurchaseByIdAsync(id);
                    if (ok) await _iap.ClaimAllPendingEntitlements("IAP");
                    return ok;
                });
        }
    }
    
    /*async UniTask BuildList_old()
    {
        // Clear existing
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var pc = ProductCatalog.LoadDefaultCatalog();
        if (pc == null || pc.allProducts == null || pc.allProducts.Count == 0)
        {
            Debug.LogWarning("[Shop] Unity IAP Catalog is empty.");
            return;
        }

        // Create a row per product
        foreach (var item in pc.allProducts)
        {
            var row = Instantiate(rowPrefab, contentRoot, worldPositionStays: false);
            row.gameObject.SetActive(true);

            // Title / description
            var title = (item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Title))
                ? item.defaultDescription.Title
                : item.id;

            var desc = (item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Description))
                ? item.defaultDescription.Description
                : FormatPayoutsShort(item.Payouts, subtypeLp, subtypeKeys); // Payouts is IList<>, that's fine now

            // Price: ask IAP (Editor=Mock; Device=real store after FetchProducts)
            var price = _iap.GetLocalizedPriceById(item.id, "…");
            // If price isn't ready yet (early frame), poll briefly (optional)
            if (price == "…")
                price = await TryWaitForPrice(item.id, 1.5f, "…");

            row.Bind(
                catalogId: item.id,
                title: title,
                subtitle: desc,
                priceText: price,
                onBuy: async (id) =>
                {
                    // Purchase and (if you use deferred grants) claim any saved entitlements
                    var ok = await _iap.PurchaseByIdAsync(id);
                    if (ok) _iap?.ClaimAllPendingEntitlements("IAP");
                    return ok;
                });
        }
    }*/
    
    string FormatPayoutsShort(IList<ProductCatalogPayout> pays)
    {
        if (pays == null || pays.Count == 0) return "";
        var parts = new List<string>(pays.Count);
        foreach (var p in pays)
        {
            var q = (int)Math.Round((double)p.quantity);
            if (q <= 0) continue;
            var sub = p.subtype ?? "";
            if (string.Equals(sub, subtypeLp, StringComparison.OrdinalIgnoreCase)) parts.Add($"{q} LP");
            else if (string.Equals(sub, subtypeKeys, StringComparison.OrdinalIgnoreCase)) parts.Add($"{q} Keys");
            else parts.Add($"{q} {sub}");
        }
        return string.Join(" • ", parts);
    }
    
    /*string FormatPayoutsShort(IList<ProductCatalogPayout> pays, string lpTag, string keysTag)
    {
        if (pays == null || pays.Count == 0) return "";
        var parts = new List<string>(pays.Count);
        foreach (var p in pays)
        {
            var q = (int)Math.Round((double)p.quantity);
            if (q <= 0) continue;
            var sub = p.subtype ?? "";
            if (string.Equals(sub, lpTag, StringComparison.OrdinalIgnoreCase))      parts.Add($"{q} LP");
            else if (string.Equals(sub, keysTag, StringComparison.OrdinalIgnoreCase)) parts.Add($"{q} Keys");
            else                                                                     parts.Add($"{q} {sub}");
        }
        return string.Join(" • ", parts);
    }*/

    async UniTask<string> TryWaitForPrice(string id, float seconds, string fallback)
    {
        float t = 0f;
        while (t < seconds)
        {
            var p = _iap.GetLocalizedPriceById(id, fallback);
            if (!string.IsNullOrEmpty(p) && p != fallback) return p;
            await UniTask.Yield(); t += Time.unscaledDeltaTime;
        }
        return _iap.GetLocalizedPriceById(id, fallback);
    }
    
    void AutoWireIfMissing()
    {
        if (!contentRoot)
        {
            var scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll && scroll.content) contentRoot = scroll.content;
        }
    }
}
