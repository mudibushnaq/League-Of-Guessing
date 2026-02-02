#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.Purchasing;                // v5 API surface
using Unity.Services.Core;                   // UGS init
using Unity.Services.Core.Environments;

/// One-file IAP service that can work in Mock mode (Editor/dev) or Real (IAP v5).
/// Switch via Inspector flags. Keeps the same IIapService API everywhere.
///
/// Usage:
/// - Put this on a bootstrap GameObject
/// - Assign IapCatalog
/// - (Optional) assign rewards sink / analytics / receipt validator MBs
/// - In Editor, enable "Use Mock In Editor" (default true)
/// - On device, disable "Force Mock" to use real stores
[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(UnityIapService),
    gameObjectName: nameof(UnityIapService),
    extraBindings: typeof(IIapService))]
public sealed class UnityIapService : MonoBehaviour, IIapService, IProjectInitializable
{
    int IProjectInitializable.Order => -100;
    
    [Header("Mode")]
    [SerializeField] private bool useMockInEditor = true;
    [SerializeField] private bool forceMock = false;

    bool UseMock => forceMock || (useMockInEditor && Application.isEditor);
    
    [Header("Grant timing")]
    [SerializeField] private bool grantImmediately = false; // false = save entitlement, grant later
    
    [Header("Config")]
    [SerializeField] private IapCatalog catalog;                 // ScriptableObject
    [SerializeField] private string ugsEnvironment = "production";
    [SerializeField] private bool debugLogs = true;

    [Header("Optional collaborators")]
    [Zenject.Inject] private IIapRewardsSink rewardsSink;        // injected by DI
    [SerializeField] private MonoBehaviour analyticsBehaviour;         // implements IIapAnalytics (optional)
    [SerializeField] private MonoBehaviour receiptValidatorBehaviour;  // implements IIapReceiptValidator (optional)

    // Mock knobs
    [Header("Mock Settings")]
    [SerializeField] private bool mockSimulateFailure = false;
    [SerializeField] private float mockDelaySeconds = 0.4f;
    [SerializeField] private string mockCurrency = "USD";
    [SerializeField] private decimal mockAmount = 0.99m;
    [SerializeField] private string mockLocalized = "$0.99";

    // Resolved interfaces
    private IIapAnalytics? analytics;
    private IIapReceiptValidator? receiptValidator;
    
    [Header("Payout mapping (Unity IAP Catalog)")]
    [SerializeField] private string payoutSubtypeLp   = "LP";    // matches Payout Subtype in Catalog
    [SerializeField] private string payoutSubtypeKeys = "Keys";  // matches Payout Subtype in Catalog
    // Cache Unity's IAP Catalog
    ProductCatalog _unityCatalog;
    
    // Real v5 objects
    private StoreController _store;             // UnityIAPServices.StoreController()
    private CatalogProvider _catalogProvider;   // optional but fine to keep
    IIapEntitlementsStore _entStore;
    // Shared cache (used by both Real/Mock to expose prices/titles)
    private readonly Dictionary<IapProduct, IapProductInfo> _cache = new();
    private readonly Dictionary<string, IapProductInfo> _cacheById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _canBuyById =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<IapProduct, bool> _canBuyByEnum =
        new();
    
    // track pending by string too (for analytics etc.)
    private string _pendingProductId = "";
    
    // Awaiters (real mode)
    private UniTaskCompletionSource<bool>? _initTcs;
    private UniTaskCompletionSource<bool>? _purchaseTcs;
    private bool _handlersWired;
    private IapProduct _pendingProduct = IapProduct.None;
    private PendingOrder _pendingOrder; // from OnPurchasePending

    public bool IsInitialized { get; private set; }

    UniTask IProjectInitializable.Initialize()
    {
        analytics        = analyticsBehaviour        as IIapAnalytics;
        receiptValidator = receiptValidatorBehaviour as IIapReceiptValidator;

        if (!catalog) catalog = Resources.Load<IapCatalog>("IapCatalog");
        if (!catalog) Debug.LogError("[IAP] Missing IapCatalog (Resources/IapCatalog.asset).");

        _catalogProvider = new CatalogProvider();
        _entStore = new PlayerPrefsEntitlementsStore();
        Debug.Log("[IProjectInitializable.Initialize] UnityIapService ready.");
        return UniTask.CompletedTask;
    }

    // ---------- IIapService ----------
    public async UniTask InitializeAsync()
    {
        if (IsInitialized) return;
        if (_initTcs != null)
        {
            await _initTcs.Task;
            return;
        }

        analytics?.OnIapInitStarted();
        _initTcs = new UniTaskCompletionSource<bool>();
        var initTcs = _initTcs;

        if (UseMock)
        {
            // MOCK INIT
            BuildMockCache();
            await UniTask.DelayFrame(1);
            IsInitialized = true;
            analytics?.OnIapInitSucceeded();
            if (debugLogs) Debug.Log("[IAP] Mock initialized");
            _initTcs.TrySetResult(true);
            _initTcs = null;
            return;
        }

        // REAL INIT (IAP v5)
        try
        {
            var opts = new InitializationOptions()
                .SetEnvironmentName(string.IsNullOrEmpty(ugsEnvironment) ? "production" : ugsEnvironment);
            await UnityServices.InitializeAsync(opts);
        }
        catch (Exception ex)
        {
            analytics?.OnIapInitFailed("UGS Init: " + ex.Message);
            throw;
        }

        _store = UnityIAPServices.StoreController();
        if (!_handlersWired)
        {
            WireEventHandlers(_store);
            _handlersWired = true;
        }
        await _store.Connect();
        
        // --- Prefer Unity's built-in IAP Catalog (Window ▸ Services ▸ In-App Purchasing ▸ IAP Catalog)
        var unityCatalog = ProductCatalog.LoadDefaultCatalog();
        if (unityCatalog is { allProducts: { Count: > 0 } })
        {
            var (defs, storeMap) = BuildFromUnityIapCatalog(unityCatalog);

            // Register logical IDs + per-store overrides with the CatalogProvider
            _catalogProvider.AddProducts(defs, storeMap);

            // Resolve IDs for the active store, then fetch
            _catalogProvider.FetchProducts(resolvedDefs =>
            {
                if (resolvedDefs == null || resolvedDefs.Count == 0)
                {
                    if (debugLogs) Debug.LogWarning("[IAP] CatalogProvider resolved 0 products; falling back to logical IDs.");
                    _store.FetchProducts(defs); // fallback: logical IDs (must match store if no overrides)
                }
                else
                {
                    _store.FetchProducts(resolvedDefs);
                }
            });
        }
        else
        {
            // Fallback: your ScriptableObject catalog (kept for dev convenience)
            DefineProductsFromCatalog(catalog, _catalogProvider);
            var defs = BuildProductDefinitions(catalog);
            _store.FetchProducts(defs);
        }

        var ok = await initTcs.Task;
        IsInitialized = ok;

        if (IsInitialized) analytics?.OnIapInitSucceeded();
        else analytics?.OnIapInitFailed("Init sequence failed");
    }
    
    ProductCatalog GetUnityCatalog()
    {
        if (_unityCatalog == null) _unityCatalog = ProductCatalog.LoadDefaultCatalog();
        return _unityCatalog;
    }

    bool IdEquals(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // Find a catalog item by *either* logical ID (item.id) or store-specific override.
    ProductCatalogItem FindCatalogItemByAnyId(string anyId)
    {
        var pc = GetUnityCatalog();
        if (pc == null || pc.allProducts == null) return null;
        foreach (var item in pc.allProducts)
        {
            if (IdEquals(item.id, anyId)) return item;
            var gp = item.GetStoreID("GooglePlay");     if (!string.IsNullOrEmpty(gp) && IdEquals(gp, anyId)) return item;
            var ap = item.GetStoreID("AppleAppStore");  if (!string.IsNullOrEmpty(ap) && IdEquals(ap, anyId)) return item;
        }
        return null;
    }
    
    List<IapGrant> BuildGrantsFromUnityCatalog(string logicalOrStoreId)
    {
        var grants = new List<IapGrant>();
        var item = FindCatalogItemByAnyId(logicalOrStoreId);
        if (item == null || item.Payouts == null) return grants;

        foreach (var pay in item.Payouts)
        {
            int q = (int)Math.Round((double)pay.quantity);
            if (q <= 0) continue;

            grants.Add(new IapGrant {
                type     = pay.type.ToString(),
                subtype  = pay.subtype ?? "",
                quantity = q,
                data     = pay.data ?? ""
            });
        }
        return grants;
    }
    
    async UniTask ApplyGrants(List<IapGrant> grants, string reason)
    {
        if (rewardsSink == null) { Debug.LogWarning("[IAP] No rewards sink to apply grants."); return; }

        foreach (var g in grants)
        {
            // Map by subtype (case-insensitive). Extend as you add more types.
            if (IdEquals(g.subtype, payoutSubtypeLp) && g.quantity > 0)
                await rewardsSink.GrantLp(g.quantity, reason);
            else if (IdEquals(g.subtype, payoutSubtypeKeys) && g.quantity > 0)
                await rewardsSink.GrantKeys(g.quantity, reason);
            else
                Debug.Log($"[IAP] Unhandled grant: type={g.type} subtype={g.subtype} q={g.quantity} data={g.data}");
        }
    }

    // Grant using Unity IAP Catalog Payouts
    void GrantFromUnityCatalog(string logicalOrStoreId, string reason)
    {
        if (rewardsSink == null) { Debug.LogWarning("[IAP] No rewards sink to grant payouts."); return; }

        var item = FindCatalogItemByAnyId(logicalOrStoreId);
        if (item == null || item.Payouts == null || item.Payouts.Count == 0)
        {
            Debug.LogWarning($"[IAP] No catalog item/payouts for {logicalOrStoreId}.");
            return;
        }

        foreach (var pay in item.Payouts)
            TryGrantPayout(pay, reason);
    }

    // Interpret a single payout into your sink
    void TryGrantPayout(ProductCatalogPayout pay, string reason)
    {
        // Quantity in catalog is decimal/double; cast to int for your sink
        int q = (int)Math.Round((double)pay.quantity);

        // Use Type/Subtype to decide where to grant
        switch (pay.type)
        {
            case ProductCatalogPayout.ProductCatalogPayoutType.Currency:
            case ProductCatalogPayout.ProductCatalogPayoutType.Resource:
                if (!string.IsNullOrEmpty(pay.subtype))
                {
                    if (IdEquals(pay.subtype, payoutSubtypeLp) && q > 0)
                    {
                        rewardsSink?.GrantLp(q, reason);
                        return;
                    }
                    if (IdEquals(pay.subtype, payoutSubtypeKeys) && q > 0)
                    {
                        rewardsSink?.GrantKeys(q, reason);
                        return;
                    }
                }
                // Unknown subtype → just log (or extend mapping)
                Debug.Log($"[IAP] Unmapped payout subtype '{pay.subtype}' (q={q})");
                break;

            case ProductCatalogPayout.ProductCatalogPayoutType.Item:
            case ProductCatalogPayout.ProductCatalogPayoutType.Other:
                // If you store JSON or special flags in pay.data, parse here and route accordingly.
                Debug.Log($"[IAP] Payout ({pay.type}) subtype='{pay.subtype}' q={q} data='{pay.data}' (no handler)");
                break;
        }
    }
    
    public IapProductInfo? GetInfo(IapProduct id)
        => _cache.TryGetValue(id, out var info) ? info : (IapProductInfo?)null;

    public IEnumerable<IapProductInfo> AllProducts() => _cache.Values;
    
    // Build from Unity's IAP Catalog window (Window/Services/In-App Purchasing/IAP Catalog)
    private static (List<ProductDefinition> defs, Dictionary<string, StoreSpecificIds> map)
        BuildFromUnityIapCatalog(ProductCatalog pc)
    {
        var defs = new List<ProductDefinition>(pc.allProducts.Count);
        var map  = new Dictionary<string, StoreSpecificIds>();

        foreach (var item in pc.allProducts)
        {
            // Logical cross-store ID from the Unity IAP Catalog
            defs.Add(new ProductDefinition(item.id, item.type));

            // Collect store-specific overrides (if any)
            var storeIds = new StoreSpecificIds();
            bool hasAny = false;

            var gp = item.GetStoreID("GooglePlay");      // returns the override productId for Play
            if (!string.IsNullOrEmpty(gp))
            {
                storeIds.Add(gp, "GooglePlay");          // Add(productId, storeName)
                hasAny = true;
            }

            var ap = item.GetStoreID("AppleAppStore");   // returns the override productId for Apple
            if (!string.IsNullOrEmpty(ap))
            {
                storeIds.Add(ap, "AppleAppStore");       // Add(productId, storeName)
                hasAny = true;
            }

            if (hasAny)
                map[item.id] = storeIds;
        }

        return (defs, map);
    }
    
    public async UniTask<bool> PurchaseAsync(IapProduct id)
    {
        if (!IsInitialized) await InitializeAsync();

        if (!_cache.TryGetValue(id, out var info) ||
            (_canBuyByEnum.TryGetValue(id, out var availEnum) && !availEnum))
        {
            analytics?.OnIapPurchaseFailed(id, "NotAvailable", false);
            return false;
        }

        if (UseMock)
        {
            analytics?.OnIapPurchaseStarted(id);
            await UniTask.Delay(TimeSpan.FromSeconds(mockDelaySeconds));

            if (mockSimulateFailure)
            {
                analytics?.OnIapPurchaseFailed(id, "MockFailure", false);
                return false;
            }

            var grants = BuildGrantsFromUnityCatalog(id.ToString());
            var tx = "MOCK_TX_" + Guid.NewGuid().ToString("N");

            if (grantImmediately || _entStore == null)
            {
                // Grant right now
                await ApplyGrants(grants, "MOCK_IAP");
            }
            else
            {
                // Save for later claim
                _entStore.Add(new IapEntitlement
                {
                    productId     = id.ToString(),
                    transactionId = tx,
                    grants        = grants
                });
                if (debugLogs) Debug.Log($"[IAP] (MOCK) Saved entitlement {id} tx={tx} grants={grants.Count}");
            }

            analytics?.OnIapPurchaseSucceeded(id, "MockStore", info.price.currency, info.price.amount, tx);
            return true;
        }

        // REAL PURCHASE
        if (_purchaseTcs != null)
            throw new InvalidOperationException("Another purchase is already in progress.");

        _pendingProduct = id;
        _purchaseTcs = new UniTaskCompletionSource<bool>();
        analytics?.OnIapPurchaseStarted(id);

        _store.PurchaseProduct(id.ToString()); // Will trigger OnPurchasePending/Failed

        var ok = await _purchaseTcs.Task;
        _purchaseTcs = null;
        _pendingProduct = IapProduct.None;
        return ok;
    }

    public async UniTask RestoreTransactionsAsync()
    {
        if (!IsInitialized) await InitializeAsync();

        if (UseMock)
        {
            analytics?.OnIapRestoreCompleted(0);
            await UniTask.CompletedTask;
            return;
        }

        _store.RestoreTransactions((ok, err) =>
        {
            Debug.Log($"[IAP] RestoreTransactions completed: ok={ok}, err={err}");
        });
        analytics?.OnIapRestoreCompleted(0);
        await UniTask.CompletedTask;
    }

    public string GetLocalizedPrice(IapProduct id, string fallback = "")
        => _cache.TryGetValue(id, out var info)
            ? info.price.localized
            : fallback;

    // ---------- MOCK ----------
    void BuildMockCache()
    {
        _cache.Clear();
        _cacheById.Clear();
        _canBuyById.Clear();
        _canBuyByEnum.Clear();

        var pc = ProductCatalog.LoadDefaultCatalog();
        if (pc != null && pc.allProducts != null && pc.allProducts.Count > 0)
        {
            foreach (var item in pc.allProducts)
            {
                var title = (item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Title))
                    ? item.defaultDescription.Title
                    : item.id;

                var info = new IapProductInfo(
                    Enum.TryParse<IapProduct>(item.id, out var pid) ? pid : IapProduct.None,
                    Map(item.type),
                    (item.defaultDescription != null && !string.IsNullOrEmpty(item.defaultDescription.Title)) ? item.defaultDescription.Title : item.id,
                    item.defaultDescription?.Description ?? "",
                    new IapPrice(mockLocalized, mockCurrency, mockAmount),
                    /* avail: */ true
                );

                _cacheById[item.id] = info;
                _canBuyById[item.id] = true;

                if (Enum.TryParse<IapProduct>(item.id, out var enumId))
                {
                    _cache[enumId] = info;
                    _canBuyByEnum[enumId] = true;
                }
            }
            if (debugLogs) Debug.Log($"[IAP] Mock cache from Unity Catalog: {_cacheById.Count} items.");
            return;
        }

        // fallback to your ScriptableObject catalog if needed
        if (catalog != null)
        {
            foreach (var e in catalog.entries)
            {
                var info = new IapProductInfo(
                    e.product,
                    e.type,
                    e.product.ToString(),
                    "Mock product",
                    new IapPrice(mockLocalized, mockCurrency, mockAmount),
                    /* avail: */ true
                );

                _cache[e.product] = info;
                _canBuyByEnum[e.product] = true;

                var id = e.product.ToString();
                _cacheById[id] = info;
                _canBuyById[id] = true;
            }
            if (debugLogs) Debug.Log($"[IAP] Mock cache from SO: {_cacheById.Count} items.");
        }
    }

    void GrantFromCatalog(IapProduct product, string reason)
    {
        if (catalog != null && catalog.TryGet(product, out var entry) && rewardsSink != null)
        {
            if (entry.payoutLp   > 0) rewardsSink.GrantLp(entry.payoutLp,   reason);
            if (entry.payoutKeys > 0) rewardsSink.GrantKeys(entry.payoutKeys, reason);
        }
        else
        {
            Debug.LogWarning("[IAP] No rewards sink or catalog entry for " + product);
        }
    }
    
    /// Claim and grant all pending entitlements saved earlier. Returns number of entitlements claimed.
    public async UniTask<int> ClaimAllPendingEntitlements(string reason = "IAP")
    {
        if (_entStore == null)
        {
            Debug.Log("ClaimAllPendingEntitlements _entStore is null");
            return 0;
        }

        var list = _entStore.Load();
        if (list.Count == 0)
        {
            Debug.Log($"ClaimAllPendingEntitlements list.Count={list.Count}");
            return 0;
        }

        foreach (var e in list)
            await ApplyGrants(e.grants, reason);

        // Clear after applying
        foreach (var e in new List<IapEntitlement>(list))
            _entStore.RemoveByTx(e.transactionId);

        if (debugLogs) Debug.Log($"[IAP] Claimed {list.Count} entitlement(s).");
        return list.Count;
    }

    // ---------- REAL (v5) Event Handlers ----------
    void WireEventHandlers(StoreController s)
    {
        s.OnProductsFetched      += OnProductsFetched;
        s.OnProductsFetchFailed  += OnProductsFetchFailed;
        s.OnPurchasesFetched     += OnPurchasesFetched;
        s.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

        s.OnPurchasePending      += OnPurchasePending;        // PendingOrder
        s.OnPurchaseFailed       += OnPurchaseFailed;         // FailedOrder

        s.OnStoreDisconnected    += OnStoreDisconnected;
    }

    void OnProductsFetched(List<Product> products)
    {
        if (debugLogs) Debug.Log($"[IAP] Products fetched: {products.Count}");

        _cache.Clear();
        _cacheById.Clear();
        _canBuyById.Clear();
        _canBuyByEnum.Clear();

        foreach (var p in products)
        {
            // Map to your info (no available flag here)
            var title = string.IsNullOrEmpty(p.metadata.localizedTitle)
                ? p.definition.id
                : p.metadata.localizedTitle;

            var info = new IapProductInfo(
                Enum.TryParse<IapProduct>(p.definition.id, out var pid) ? pid : IapProduct.None,
                Map(p.definition.type),
                string.IsNullOrEmpty(p.metadata.localizedTitle) ? p.definition.id : p.metadata.localizedTitle,
                p.metadata.localizedDescription,
                new IapPrice(p.metadata.localizedPriceString, p.metadata.isoCurrencyCode, p.metadata.localizedPrice),
                /* avail: */ p.availableToPurchase
            );

            // cache by string id
            _cacheById[p.definition.id] = info;
            _canBuyById[p.definition.id] = p.availableToPurchase;

            // cache by enum if it parses
            if (Enum.TryParse<IapProduct>(p.definition.id, out var enumId))
            {
                _cache[enumId] = info;
                _canBuyByEnum[enumId] = p.availableToPurchase;
            }
        }

        _store.FetchPurchases();
    }

    void OnProductsFetchFailed(ProductFetchFailed fail)
    {
        Debug.LogError("[IAP] FetchProducts failed: " + fail);
        _initTcs?.TrySetResult(false);
    }

    void OnPurchasesFetched(Orders orders)
    {
        if (debugLogs)
            Debug.Log($"[IAP] Purchases fetched. Pending:{orders.PendingOrders.Count} Completed:{orders.ConfirmedOrders.Count} Deferred:{orders.DeferredOrders.Count}");

        // (Optional) reconcile entitlements here
        ClaimAllPendingEntitlements().Forget();

        _initTcs?.TrySetResult(true);
        _initTcs = null;
    }

    void OnPurchasesFetchFailed(PurchasesFetchFailureDescription fail)
    {
        Debug.LogWarning("[IAP] FetchPurchases failed: " + fail.FailureReason);
        _initTcs?.TrySetResult(false);
        _initTcs = null;
    }

    void OnPurchasePending(PendingOrder order)
    {
        _pendingOrder = order;
        ValidateAndGrantThenConfirmAsync(order).Forget();
    }

    void OnPurchaseFailed(FailedOrder fail)
    {
        var userCancelled = fail.FailureReason == PurchaseFailureReason.UserCancelled;
        var msg = $"Purchase failed: {fail.FailureReason} (product: {GetFirstProductId(fail)})";
        if (!userCancelled) Debug.LogWarning(msg); else if (debugLogs) Debug.Log(msg);
        analytics?.OnIapPurchaseFailed(_pendingProduct, msg, userCancelled);
        _purchaseTcs?.TrySetResult(false);
    }

    void OnStoreDisconnected(StoreConnectionFailureDescription desc)
    {
        Debug.LogError($"Store disconnected: {desc.Message} (retryable: {desc.IsRetryable})");
    }

    // ---------- REAL Helpers ----------
    async UniTaskVoid ValidateAndGrantThenConfirmAsync(PendingOrder order)
    {
        bool validated = true;

        var productId = GetFirstProductId(order);   // e.g., "LP_100" or store override
        var storeName = GetStoreName(order.Info);   // "GooglePlay"/"AppleAppStore"/"Unknown"
        var receipt   = order.Info?.Receipt;
        var txId      = order.Info?.TransactionID;

        if (receiptValidator != null)
        {
            try
            {
                validated = await receiptValidator.ValidateAsync(storeName, productId, receipt ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[IAP] Receipt validation exception: " + ex);
                validated = false;
            }
        }

        if (!validated)
        {
            analytics?.OnIapPurchaseFailed(_pendingProduct, "ReceiptInvalid", false);
            _purchaseTcs?.TrySetResult(false);
            _store.ConfirmPurchase(order); // clear pending
            return;
        }

        // ----- Build grants from Unity IAP Catalog payouts -----
        var grants = BuildGrantsFromUnityCatalog(productId);

        // ----- Grant now or save for later -----
        if (grantImmediately || _entStore == null)
        {
            // immediate credit
            ApplyGrants(grants, "IAP");
        }
        else
        {
            // defer: persist entitlement to claim later
            var tx = string.IsNullOrEmpty(txId) ? Guid.NewGuid().ToString("N") : txId;
            _entStore.Add(new IapEntitlement
            {
                productId     = productId,
                transactionId = tx,
                grants        = grants
            });
            if (debugLogs) Debug.Log($"[IAP] Saved entitlement ({productId}) tx={tx} grants={grants.Count}");
        }

        // ----- Analytics -----
        if (_cache.TryGetValue(_pendingProduct, out var info))
        {
            analytics?.OnIapPurchaseSucceeded(
                _pendingProduct,
                storeName,
                info.price.currency,
                info.price.amount,
                txId ?? string.Empty
            );
        }

        // MUST confirm to finish the transaction
        _store.ConfirmPurchase(order);

        _purchaseTcs?.TrySetResult(true);
    }
    
    public string GetLocalizedPriceById(string catalogId, string fallback = "")
        => _cacheById.TryGetValue(catalogId, out var info) ? info.price.localized : fallback;

    public async UniTask<bool> PurchaseByIdAsync(string catalogId)
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            var grants = BuildGrantsFromUnityCatalog(catalogId);
            if (grants.Count == 0)
            {
                Debug.LogWarning($"[IAP] Windows purchase has no grants for '{catalogId}'.");
                return false;
            }

            ApplyGrants(grants, "WIN_IAP");

            if (_cacheById.TryGetValue(catalogId, out var infoWin))
                analytics?.OnIapPurchaseSucceeded(IapProduct.None, "Windows", infoWin.price.currency, infoWin.price.amount, "WIN_TX");
            else
                analytics?.OnIapPurchaseSucceeded(IapProduct.None, "Windows", "USD", 0m, "WIN_TX");

            return true;
        }

        if (!IsInitialized) await InitializeAsync();

        if (!_cacheById.TryGetValue(catalogId, out var info) ||
            (_canBuyById.TryGetValue(catalogId, out var avail) && !avail))
        {
            if (debugLogs) Debug.LogWarning($"[IAP] Product '{catalogId}' not available (mock={UseMock}).");
            analytics?.OnIapPurchaseFailed(_pendingProduct, "NotAvailable", false);
            return false;
        }

        if (UseMock)
        {
            analytics?.OnIapPurchaseStarted(IapProduct.None);
            await UniTask.Delay(TimeSpan.FromSeconds(mockDelaySeconds));
            if (mockSimulateFailure)
            {
                analytics?.OnIapPurchaseFailed(IapProduct.None, "MockFailure", false);
                return false;
            }

            // Build grants from the real catalog id
            var grants = BuildGrantsFromUnityCatalog(catalogId);
            var tx = "MOCK_TX_" + Guid.NewGuid().ToString("N");

            if (grantImmediately || _entStore == null) ApplyGrants(grants, "MOCK_IAP");
            else _entStore.Add(new IapEntitlement { productId = catalogId, transactionId = tx, grants = grants });

            analytics?.OnIapPurchaseSucceeded(IapProduct.None, "MockStore", info.price.currency, info.price.amount, tx);
            return true;
        }

        // REAL
        if (_purchaseTcs != null) throw new InvalidOperationException("Another purchase is already in progress.");

        _pendingProduct = IapProduct.None; // enum may be unknown
        _pendingProductId = catalogId;

        _purchaseTcs = new UniTaskCompletionSource<bool>();
        analytics?.OnIapPurchaseStarted(_pendingProduct);

        _store.PurchaseProduct(catalogId);  // <- uses catalog id
        var ok = await _purchaseTcs.Task;

        _purchaseTcs = null;
        _pendingProduct = IapProduct.None;
        _pendingProductId = "";
        return ok;
    }

    // Read product id from the order's cart (v5 uses carts)
    static string GetFirstProductId(Order order)
    {
        var cart = order?.CartOrdered;
        if (cart != null)
        {
            var items = cart.Items();
            if (items != null && items.Count > 0)
            {
#pragma warning disable CS0618 // definition is obsolete but usable for IDs
                return items[0].Product?.definition?.id ?? string.Empty;
#pragma warning restore CS0618
            }
        }
        return string.Empty;
    }

    static string GetStoreName(IOrderInfo? info)
    {
        if (info == null) return "Unknown";
        if (info.Apple  != null) return "AppleAppStore";
        if (info.Google != null) return "GooglePlay";
        return "Unknown";
    }

    static IapType Map(ProductType t) => t switch
    {
        ProductType.Consumable => IapType.Consumable,
        ProductType.NonConsumable => IapType.NonConsumable,
        ProductType.Subscription => IapType.Subscription,
        _ => IapType.Consumable
    };

    static void DefineProductsFromCatalog(IapCatalog src, CatalogProvider dest)
    {
        foreach (var e in src.entries)
        {
            dest.AddProduct(e.product.ToString(), e.type switch
            {
                IapType.Consumable => ProductType.Consumable,
                IapType.NonConsumable => ProductType.NonConsumable,
                IapType.Subscription => ProductType.Subscription,
                _ => ProductType.Consumable
            });
        }
    }

    static List<ProductDefinition> BuildProductDefinitions(IapCatalog src)
    {
        var list = new List<ProductDefinition>(src.entries.Count);
        foreach (var e in src.entries)
        {
            var pt = e.type switch
            {
                IapType.Consumable => ProductType.Consumable,
                IapType.NonConsumable => ProductType.NonConsumable,
                IapType.Subscription => ProductType.Subscription,
                _ => ProductType.Consumable
            };
            list.Add(new ProductDefinition(e.product.ToString(), pt));
        }
        return list;
    }
}
