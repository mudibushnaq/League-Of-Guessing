#nullable enable

public interface IPriceFormatter
{
    /// Convert raw store values into display text; fall back to store formatted if provided.
    string Format(string storeLocalizedPriceString, string currencyCode, decimal amount);
}

public sealed class DefaultPriceFormatter : IPriceFormatter
{
    public string Format(string storeLocalized, string currency, decimal amount)
    {
        // Prefer store's localized string (handles locale rules).
        if (!string.IsNullOrEmpty(storeLocalized)) return storeLocalized;
        // Fallback example (very simple).
        return $"{currency} {amount:0.00}";
    }
}