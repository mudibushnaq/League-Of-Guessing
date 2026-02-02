using System;
using UnityEngine;

/// <summary>
/// Global, persistent store for LP and Keys. 
/// Thread-safe enough for typical Unity usage (single-threaded).
/// </summary>
public static class CurrencyStore
{
    // --- PlayerPrefs keys ---
    private const string PP_LP   = "LoG_LP";
    private const string PP_KEYS = "LoG_Keys";

    // --- Defaults / tuning ---
    public static int DefaultLPOnFirstRun   = 0; // give some LP to try the flow
    public static int DefaultKeysOnFirstRun = 10;   // starter keys
    public static int LpPerKey              = 20;  // conversion rate (LP -> 1 Key)

    // --- Cached values (read on Init) ---
    public static int LP   { get; private set; }
    public static int Keys { get; private set; }

    // --- Events (UI can subscribe) ---
    public static event Action<int> OnLPChanged;
    public static event Action<int> OnKeysChanged;

    private static bool _initialized;

    /// <summary>Call once at boot (or anytime before first use).</summary>
    public static void InitIfNeeded()
    {
        if (_initialized) return;

        // First-run bootstrap
        bool hasLP   = PlayerPrefs.HasKey(PP_LP);
        bool hasKeys = PlayerPrefs.HasKey(PP_KEYS);

        LP   = hasLP   ? PlayerPrefs.GetInt(PP_LP,   0) : DefaultLPOnFirstRun;
        Keys = hasKeys ? PlayerPrefs.GetInt(PP_KEYS, 0) : DefaultKeysOnFirstRun;

        if (!hasLP)   PlayerPrefs.SetInt(PP_LP,   LP);
        if (!hasKeys) PlayerPrefs.SetInt(PP_KEYS, Keys);
        PlayerPrefs.Save();
        Debug.Log($"InitIfNeeded triggered, LP={LP}, Keys={Keys}");
        _initialized = true;
    }

    // ---------- LP ----------
    public static void AddLP(int amount)
    {
        InitIfNeeded();
        if (amount <= 0) return;
        LP += amount;
        PlayerPrefs.SetInt(PP_LP, LP);
        PlayerPrefs.Save();
        OnLPChanged?.Invoke(LP);
    }

    /// <summary>Tries to spend LP. Returns false if not enough.</summary>
    public static bool TrySpendLP(int amount)
    {
        InitIfNeeded();
        if (amount <= 0) return true;
        if (LP < amount) return false;
        LP -= amount;
        PlayerPrefs.SetInt(PP_LP, LP);
        PlayerPrefs.Save();
        OnLPChanged?.Invoke(LP);
        return true;
    }

    // ---------- Keys ----------
    public static void AddKeys(int amount)
    {
        InitIfNeeded();
        if (amount <= 0) return;
        Keys += amount;
        PlayerPrefs.SetInt(PP_KEYS, Keys);
        PlayerPrefs.Save();
        OnKeysChanged?.Invoke(Keys);
    }

    /// <summary>Tries to spend one key (or amount). Returns false if not enough.</summary>
    public static bool TrySpendKeys(int amount = 1)
    {
        InitIfNeeded();
        if (amount <= 0) return true;
        if (Keys < amount) return false;
        Keys -= amount;
        PlayerPrefs.SetInt(PP_KEYS, Keys);
        PlayerPrefs.Save();
        OnKeysChanged?.Invoke(Keys);
        return true;
    }

    // ---------- Conversion ----------
    /// <summary>Converts LP into keys at current rate. Returns how many keys obtained.</summary>
    public static int ConvertLPToKeys(int lpToSpend)
    {
        InitIfNeeded();
        if (lpToSpend < LpPerKey) return 0;

        int keysCanBuy = lpToSpend / LpPerKey;
        int lpCost     = keysCanBuy * LpPerKey;

        if (!TrySpendLP(lpCost)) return 0; // safety
        AddKeys(keysCanBuy);
        return keysCanBuy;
    }

    /// <summary>Convenience: buy exactly N keys with LP. Returns true on success.</summary>
    public static bool TryBuyKeysWithLP(int keysWanted)
    {
        InitIfNeeded();
        if (keysWanted <= 0) return true;
        int lpCost = keysWanted * LpPerKey;
        if (!TrySpendLP(lpCost)) return false;
        AddKeys(keysWanted);
        return true;
    }
}