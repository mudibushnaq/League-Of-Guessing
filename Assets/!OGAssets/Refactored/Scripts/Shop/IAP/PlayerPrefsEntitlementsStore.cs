using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct IapGrant
{
    public string type;     // "Currency", "Resource", etc. (from catalog)
    public string subtype;  // e.g., "LP", "Keys"
    public int quantity;    // int-cast of catalog quantity
    public string data;     // optional catalog data
}

[Serializable]
public struct IapEntitlement
{
    public string productId;      // logical or store id
    public string transactionId;  // order.Info.TransactionID (if available)
    public List<IapGrant> grants; // payouts
}

public interface IIapEntitlementsStore
{
    List<IapEntitlement> Load();
    void Save(List<IapEntitlement> all);
    void Add(IapEntitlement e);
    void RemoveByTx(string txId);
}

public sealed class PlayerPrefsEntitlementsStore : IIapEntitlementsStore
{
    const string Key = "IAP_ENTITLEMENTS_V1";

    [Serializable] class Wrap<T> { public List<T> items = new(); }

    public List<IapEntitlement> Load()
    {
        var json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return new List<IapEntitlement>();
        try { return JsonUtility.FromJson<Wrap<IapEntitlement>>(json)?.items ?? new List<IapEntitlement>(); }
        catch { return new List<IapEntitlement>(); }
    }

    public void Save(List<IapEntitlement> all)
    {
        var w = new Wrap<IapEntitlement> { items = all };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(w));
        PlayerPrefs.Save();
    }

    public void Add(IapEntitlement e)
    {
        var list = Load();
        list.Add(e);
        Save(list);
    }

    public void RemoveByTx(string txId)
    {
        var list = Load();
        list.RemoveAll(x => x.transactionId == txId);
        Save(list);
    }
}