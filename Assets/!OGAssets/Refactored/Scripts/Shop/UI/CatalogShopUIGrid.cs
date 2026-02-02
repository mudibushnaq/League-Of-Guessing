#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(CatalogShopUIGrid),
    gameObjectName: nameof(CatalogShopUIGrid))]
public sealed class CatalogShopUIGrid : MonoBehaviour, IPopupContent, IMenuInitializable
{
    int IMenuInitializable.Order => 250;
    
    [Header("Top Hero / Best Value")]
    [SerializeField] private bool allowCarousel;
    [SerializeField] private CarouselView carousel;
    [SerializeField] private ShopProductCard carouselCardPrefab;

    [Header("Sections")]
    [SerializeField] private RectTransform sectionsRoot; // vertical parent
    [SerializeField] private SectionHeaderView sectionHeaderPrefab;
    [SerializeField] private RectTransform gridContainerPrefab; // holds GridLayoutGroup + ResponsiveGridGroup
    [SerializeField] private ShopProductCard gridCardPrefab;

    [Header("Presentation")]
    [SerializeField] private ShopPresentation presentation; // icons, badges, featured, weight
    [SerializeField] private ShopTheme theme;

    [Header("UI")]
    [SerializeField] private TMP_Text headerLabel;
    [SerializeField] private GameObject loadingSpinner;

    [Header("Payout subtypes")]
    [SerializeField] private string subtypeLp   = "LP";
    [SerializeField] private string subtypeKeys = "Keys";
    
    [Header("Exchange Window")]
    [SerializeField] private Slider amountSlider;
    [SerializeField] private Button maxButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    
    [SerializeField] private TMP_Text pricePerKeyText;   // "10 IP / key"
    [SerializeField] private TMP_Text selectedKeysText;  // "x Keys"
    [SerializeField] private TMP_Text costIpText;        // "Cost: y IP"
    
    private int _pricePerKey;
    private UniTaskCompletionSource<PopupResult> _tcs;
    private bool _built;
    private bool _building;
    private UniTaskCompletionSource _buildTcs;

    private PopupRequest req;
    // Optional payload for passing config
    public sealed class Args { public int PricePerKey = 10; }
    
    [Zenject.Inject] private IRewardFX _fx;
    [Zenject.Inject] IIapService _iap;
    
    async UniTask IMenuInitializable.Initialize()
    {
        if (loadingSpinner) loadingSpinner.SetActive(true);
        if (headerLabel) headerLabel.text = "Shop";

        await EnsureBuiltAsync();
        if (loadingSpinner) loadingSpinner.SetActive(false);
    }
    
    public void Bind(PopupRequest request, UniTaskCompletionSource<PopupResult> tcs)
    {
        req = request;
        if (loadingSpinner) loadingSpinner.SetActive(true);
        if (headerLabel) headerLabel.text = "Shop";
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
        cancelButton.onClick.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();

        amountSlider.onValueChanged.AddListener(_ => RefreshUI());
        maxButton.onClick.AddListener(OnMaxClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);

        bool hasContent = sectionsRoot && sectionsRoot.childCount > 0;
        if (loadingSpinner) loadingSpinner.SetActive(!hasContent);
        EnsureBuiltAsync().Forget();
    }
    
    async UniTask EnsureBuiltAsync()
    {
        if (_building)
        {
            if (_buildTcs != null)
                await _buildTcs.Task;
            return;
        }

        bool hasContent = sectionsRoot && sectionsRoot.childCount > 0;
        if (_built && hasContent)
            return;

        _building = true;
        _buildTcs = new UniTaskCompletionSource();
        try
        {
            await BuildAll();
            _built = true;
            _buildTcs.TrySetResult();
        }
        catch (Exception ex)
        {
            _buildTcs.TrySetException(ex);
            Debug.LogError($"[Shop] Build failed: {ex}");
        }
        finally
        {
            _building = false;
            if (loadingSpinner) loadingSpinner.SetActive(false);
        }
    }

    async UniTask BuildAll()
    {
        if (!_iap.IsInitialized)
            InitializeIapSilently().Forget();

        // Clear sections
        for (int i = sectionsRoot.childCount - 1; i >= 0; i--)
            Destroy(sectionsRoot.GetChild(i).gameObject);
        // Clear carousel
        if (allowCarousel && carousel)
        {
            carousel.gameObject.SetActive(false);
            carousel.Clear();
        }

        var pc = ProductCatalog.LoadDefaultCatalog();
        if (pc == null || pc.allProducts == null || pc.allProducts.Count == 0)
        {
            Debug.LogWarning("[Shop] IAP Catalog is empty.");
            return;
        }

        // 1) Featured → Carousel (presentation.featured == true)
        var featured = new List<ProductCatalogItem>();
        if (presentation != null)
        {
            foreach (var it in pc.allProducts)
                if (presentation.TryGet(it.id, out var e) && e.featured)
                    featured.Add(it);
        }
        else
        {
            // No presentation asset: pick top 3 by payout/weight (simple heuristic)
            int take = Mathf.Min(3, pc.allProducts.Count);
            int i = 0;
            foreach (var it in pc.allProducts)
            {
                featured.Add(it);
                if (++i >= take) break;
            }
        }

        if (allowCarousel && 
            carousel &&
            featured.Count > 0)
        {
            foreach (var item in featured)
            {
                var card = Instantiate(carouselCardPrefab).transform as RectTransform;
                BindCard(card.GetComponent<ShopProductCard>(), item, isWide:true).Forget();
                carousel.AddPage(card);
            }
            carousel.BuildDots();
            carousel.LayoutHorizontal();
            await carousel.SnapTo(0);
            carousel.gameObject.SetActive(true);
        }

        // 2) Sections (grouped by ProductType)
        var groups = new Dictionary<ProductType, List<ProductCatalogItem>>
        {
            { ProductType.Consumable,    new List<ProductCatalogItem>() },
            { ProductType.NonConsumable, new List<ProductCatalogItem>() },
            { ProductType.Subscription,  new List<ProductCatalogItem>() }
        };
        foreach (var it in pc.allProducts)
            groups[it.type].Add(it);

        // Optional sort by presentation sortWeight
        Comparison<ProductCatalogItem> sort = (a,b) =>
        {
            int wa = presentation != null && presentation.TryGet(a.id, out var ea) ? ea.sortWeight : 1000;
            int wb = presentation != null && presentation.TryGet(b.id, out var eb) ? eb.sortWeight : 1000;
            return wa.CompareTo(wb);
        };
        foreach (var kv in groups) kv.Value.Sort(sort);

        await BuildSection("Consumables",   groups[ProductType.Consumable]);
        await BuildSection("Unlocks",       groups[ProductType.NonConsumable]);
        await BuildSection("Subscriptions", groups[ProductType.Subscription]);
    }

    async UniTask InitializeIapSilently()
    {
        try
        {
            await _iap.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Shop] IAP init failed: {ex.Message}");
        }
    }

    async UniTask BuildSection(string title, List<ProductCatalogItem> items)
    {
        if (items == null || items.Count == 0) return;

        var sh = Instantiate(sectionHeaderPrefab, sectionsRoot, false);
        sh.gameObject.SetActive(true);
        sh.SetText(title);

        var gridRoot = Instantiate(gridContainerPrefab, sectionsRoot, false);
        gridRoot.gameObject.SetActive(true);
        
        // force safe anchors/pivot so it grows DOWN from the top
        var grt = (RectTransform)gridRoot;
        grt.anchorMin = new Vector2(0f, 1f);
        grt.anchorMax = new Vector2(1f, 1f);
        grt.pivot     = new Vector2(0.5f, 1f);
        
        // Ensure it has GridLayoutGroup + ResponsiveGridGroup
        var grid = gridRoot.GetComponent<GridLayoutGroup>();
        var resp = gridRoot.GetComponent<ResponsiveGridGroup>();
        if (!grid) grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        if (!resp) resp = gridRoot.gameObject.AddComponent<ResponsiveGridGroup>();
        grid.childAlignment = TextAnchor.UpperCenter;
        
        // ✨ this is the key so the grid gets a height
        var fitter = gridRoot.GetComponent<ContentSizeFitter>() ?? gridRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        
        // your existing responsive columns (if used):
        resp.minColumnWidth = 280f;
        resp.gutter         = 16f;
        resp.squareCells    = false;
        
        foreach (var item in items)
        {
            var card = Instantiate(gridCardPrefab, gridRoot, false);
            card.gameObject.SetActive(true);
            await BindCard(card, item, isWide:false);
        }
        
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
        await UniTask.Yield();
        resp.Recalc();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
    }

    async UniTask BindCard(ShopProductCard card, ProductCatalogItem item, bool isWide)
    {
        if (!card) return;
        if (theme) card.ApplyTheme(theme);

        // presentation
        ShopPresentation.Entry entry = null;
        if (presentation) presentation.TryGet(item.id, out entry);
        var sprite = entry != null ? entry.icon : null;
        var badge  = entry != null ? entry.badgeText : null;

        // title/subtitle
        var title = item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Title)
            ? item.defaultDescription.Title
            : item.id;

        var subtitle = item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Description)
            ? item.defaultDescription.Description
            : FormatPayoutsShort(item.Payouts);

        // price (mock in editor, real on device)
        var price = _iap.GetLocalizedPriceById(item.id, "…");
        if (price == "…") price = await TryWaitForPrice(item.id, 1.5f, "…");

        // owned state (basic example for non-consumables)
        bool owned = false;
        if (item.type == ProductType.NonConsumable)
        {
            var info = _iap.GetInfo(Enum.TryParse<IapProduct>(item.id, out var ep) ? ep : IapProduct.None);
            if (info.HasValue)
            {
                // If your IapProductInfo has an availability flag, use it.
                // owned = !info.Value.avail;
            }
        }

        card.Bind(
            catalogId: item.id,
            title: title,
            subtitle: subtitle,
            price: price,
            iconSprite: sprite,
            badge: badge,
            owned: owned,
            onBuy: async (id) =>
            {
                var ok = await _iap.PurchaseByIdAsync(id);
                if (ok) await _iap.ClaimAllPendingEntitlements("IAP");
                amountSlider.wholeNumbers = true;
                amountSlider.minValue = 0;
                amountSlider.maxValue = MaxAffordableKeys();
                amountSlider.value = Mathf.Min(amountSlider.value, amountSlider.maxValue);

                RefreshUI();
                Debug.Log($"RefreshUI RefreshUI RefreshUI CurrencyStore.LP={CurrencyStore.LP}");
                return ok;
            });
    }

    string FormatPayoutsShort(IList<ProductCatalogPayout> pays)
    {
        if (pays == null || pays.Count == 0) return "";
        var parts = new List<string>(pays.Count);
        foreach (var p in pays)
        {
            var q = (int)Math.Round((double)p.quantity);
            if (q <= 0) continue;
            var sub = p.subtype ?? "";
            if (string.Equals(sub, subtypeLp,   StringComparison.OrdinalIgnoreCase)) parts.Add($"{q} LP");
            else if (string.Equals(sub, subtypeKeys, StringComparison.OrdinalIgnoreCase)) parts.Add($"{q} Keys");
            else parts.Add($"{q} {sub}");
        }
        return string.Join(" • ", parts);
    }

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
        req.PressedToButton.interactable = true;
        amountSlider.wholeNumbers = true;
        amountSlider.minValue = 0;
        amountSlider.maxValue = MaxAffordableKeys();
        amountSlider.value = 0;
        RefreshUI();
        _tcs?.TrySetResult(PopupResult.Secondary);
    }

    private void OnConfirmClicked()
    {
        req.PressedToButton.interactable = true;
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
        cancelButton.onClick.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();
    }
    
    #if UNITY_EDITOR
    [SerializeField] string editorMockPrice = "$0.99"; // shown in Edit Mode

    // Called from your custom inspector button
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;

        // 1) Resolve sectionsRoot (ScrollRect.content)
        if (!sectionsRoot)
        {
            var sr = GetComponentInChildren<ScrollRect>(true);
            if (sr) sectionsRoot = sr.content;
        }
        if (!sectionsRoot)
        {
            Debug.LogWarning("[Shop Preview] sectionsRoot (ScrollRect content) is not assigned.");
            return;
        }

        // Ensure viewport clips
        var sr2 = GetComponentInChildren<ScrollRect>(true);
        if (sr2 && sr2.viewport)
        {
            var mask = sr2.viewport.GetComponent<RectMask2D>() ?? sr2.viewport.gameObject.AddComponent<RectMask2D>();
            mask.enabled = true;
        }

        // 2) Clear existing preview
        if (carousel) carousel.Clear();
        for (int i = sectionsRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(sectionsRoot.GetChild(i).gameObject);

        // 3) Load Unity IAP catalog
        var pc = ProductCatalog.LoadDefaultCatalog();
        if (pc == null || pc.allProducts == null || pc.allProducts.Count == 0)
        {
            Debug.LogWarning("[Shop Preview] IAP Catalog is empty.");
            return;
        }

        // 4) Build featured carousel (up to 3)
        if (carousel && carouselCardPrefab)
        {
            int added = 0;
            foreach (var it in pc.allProducts)
            {
                if (presentation != null && presentation.TryGet(it.id, out var e) && e.featured)
                {
                    var page = Instantiate(carouselCardPrefab).transform as RectTransform;
                    BindCardPreview(page.GetComponent<ShopProductCard>(), it, isWide: true);
                    carousel.AddPage(page);
                    if (++added >= 3) break;
                }
            }
            if (added == 0)
            {
                int i = 0;
                foreach (var it in pc.allProducts)
                {
                    var page = Instantiate(carouselCardPrefab).transform as RectTransform;
                    BindCardPreview(page.GetComponent<ShopProductCard>(), it, isWide: true);
                    carousel.AddPage(page);
                    if (++i >= 3) break;
                }
            }
            carousel.BuildDots();
            carousel.LayoutHorizontal();
        }

        // 5) Group by ProductType and build sections
        var cons  = new List<ProductCatalogItem>();
        var ncons = new List<ProductCatalogItem>();
        var subs  = new List<ProductCatalogItem>();
        foreach (var it in pc.allProducts)
        {
            switch (it.type)
            {
                case ProductType.Consumable:    cons.Add(it);  break;
                case ProductType.NonConsumable: ncons.Add(it); break;
                case ProductType.Subscription:  subs.Add(it);  break;
            }
        }

        BuildSectionPreview("Consumables",   cons);
        BuildSectionPreview("Unlocks",       ncons);
        BuildSectionPreview("Subscriptions", subs);

        // Final layout pass
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
        sectionsRoot.anchoredPosition = Vector2.zero;
    }

    // Clear helper if you want the button in inspector too
    public void ClearPreview()
    {
        if (!sectionsRoot) return;
        for (int i = sectionsRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(sectionsRoot.GetChild(i).gameObject);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
        sectionsRoot.anchoredPosition = Vector2.zero;
    }

    // ---------- Editor-only builder helpers ----------
    void BuildSectionPreview(string title, List<ProductCatalogItem> items)
    {
        if (items == null || items.Count == 0) return;

        var header = Instantiate(sectionHeaderPrefab, sectionsRoot, false);
        header.gameObject.SetActive(true);
        header.SetText(title);

        var gridRoot = Instantiate(gridContainerPrefab, sectionsRoot, false);
        gridRoot.gameObject.SetActive(true);

        // Safe top-anchored rect
        var grt = (RectTransform)gridRoot;
        grt.anchorMin = new Vector2(0f, 1f);
        grt.anchorMax = new Vector2(1f, 1f);
        grt.pivot     = new Vector2(0.5f, 1f);

        // Grid + fitter
        var grid = gridRoot.GetComponent<GridLayoutGroup>() ?? gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.startCorner    = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis      = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fit  = gridRoot.GetComponent<ContentSizeFitter>() ?? gridRoot.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var resp = gridRoot.GetComponent<ResponsiveGridGroup>() ?? gridRoot.gameObject.AddComponent<ResponsiveGridGroup>();
        resp.minColumnWidth = 280f;
        resp.gutter         = 16f;
        resp.squareCells    = false;

        foreach (var it in items)
        {
            var card = Instantiate(gridCardPrefab, gridRoot, false);
            card.gameObject.SetActive(true);
            BindCardPreview(card, it, isWide: false);
        }
        resp.Recalc();
    }

    void BindCardPreview(ShopProductCard card, ProductCatalogItem item, bool isWide)
    {
        if (!card) return;
        if (theme) card.ApplyTheme(theme);

        ShopPresentation.Entry entry = null;
        if (presentation) presentation.TryGet(item.id, out entry);
        var sprite = entry != null ? entry.icon : null;
        var badge  = entry != null ? entry.badgeText : null;

        var title = (item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Title))
            ? item.defaultDescription.Title : item.id;

        var subtitle = (item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Description))
            ? item.defaultDescription.Description : FormatPayoutsShort(item.Payouts);

        var price = string.IsNullOrEmpty(editorMockPrice) ? "—" : editorMockPrice;

        // No purchases in Edit Mode—just log the click.
        card.Bind(
            catalogId: item.id,
            title: title,
            subtitle: subtitle,
            price: price,
            iconSprite: sprite,
            badge: badge,
            owned: false,
            onBuy: async (id) => { Debug.Log($"[Shop Preview] Click {id}"); return false; }
        );
    }
#endif
}
