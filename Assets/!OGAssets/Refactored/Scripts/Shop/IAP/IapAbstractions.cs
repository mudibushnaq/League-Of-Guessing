#nullable enable
using Cysharp.Threading.Tasks;

/// Grants the purchased rewards into your economy (LP, Keys, etc.).
public interface IIapRewardsSink
{
    UniTask GrantLp(int amount, string reason);
    UniTask GrantKeys(int amount, string reason);
}

/// Optional: send analytics for IAP lifecycle.
public interface IIapAnalytics
{
    void OnIapInitStarted();
    void OnIapInitSucceeded();
    void OnIapInitFailed(string reason);

    void OnIapPurchaseStarted(IapProduct product);
    void OnIapPurchaseSucceeded(IapProduct product, string store, string currency, decimal amount, string txnId);
    void OnIapPurchaseFailed(IapProduct product, string reason, bool userCancelled);
    void OnIapRestoreCompleted(int restoredCount);
}

/// Optional: server-side receipt verification (highly recommended in production).
public interface IIapReceiptValidator
{
    /// Return true if server verified (or you decided to trust for now).
    UniTask<bool> ValidateAsync(string store, string productStoreId, string receiptPayload);
}