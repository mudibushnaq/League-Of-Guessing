#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public enum IapProduct
{
    None = 0,
    LP_100,
    LP_500,
    Keys_5,
    Keys_20,
}

public enum IapType { Consumable, NonConsumable, Subscription }

public readonly struct IapPrice
{
    public readonly string localized;   // e.g., "$1.99"
    public readonly string currency;    // e.g., "USD"
    public readonly decimal amount;     // e.g., 1.99m
    public IapPrice(string localized, string currency, decimal amount)
    { this.localized = localized; this.currency = currency; this.amount = amount; }
}

public readonly struct IapProductInfo
{
    public readonly IapProduct id;
    public readonly IapType    type;
    public readonly string     title;
    public readonly string     description;
    public readonly IapPrice   price;
    public readonly bool       availableToPurchase;
    public IapProductInfo(IapProduct id, IapType type, string title, string description, IapPrice price, bool avail)
    { this.id = id; this.type = type; this.title = title; this.description = description; this.price = price; this.availableToPurchase = avail; }
}

public interface IIapService
{
    bool IsInitialized { get; }
    UniTask InitializeAsync();
    IapProductInfo? GetInfo(IapProduct id);
    IEnumerable<IapProductInfo> AllProducts(); // only the ones configured & known to store

    /// Purchase and grant rewards (through injected sink) if verified.
    UniTask<bool> PurchaseAsync(IapProduct id);

    /// App Store restore (mainly iOS; no-op on Google except to re-query).
    UniTask RestoreTransactionsAsync();

    /// Helpful for UI.
    string GetLocalizedPrice(IapProduct id, string fallback = "");

    UniTask <int> ClaimAllPendingEntitlements(string reason = "IAP");
    
    // NEW: string-based (Unity IAP Catalog id)
    UniTask<bool> PurchaseByIdAsync(string catalogId);
    string GetLocalizedPriceById(string catalogId, string fallback = "");
}