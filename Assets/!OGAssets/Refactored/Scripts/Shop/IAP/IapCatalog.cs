using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IapCatalog", menuName = "IAP/Catalog")]
public sealed class IapCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public IapProduct product;
        public IapType    type = IapType.Consumable;

        [Header("Store IDs (must match App Store/Google Console):")]
        public string googlePlayId;
        public string appleAppStoreId;

        [Header("Payout (what the player gets)")]
        public int payoutLp;     // grant LP
        public int payoutKeys;   // grant keys

        [Header("Editor/Test")]
        public string debugTitleOverride;
        public string debugDescOverride;
    }

    public List<Entry> entries = new();

    public bool TryGet(IapProduct id, out Entry e)
    {
        e = null;
        foreach (var x in entries)
            if (x.product == id) { e = x; return true; }
        return false;
    }

#if UNITY_EDITOR
    // Quick helper to ensure no duplicates.
    private void OnValidate()
    {
        // (Optional) add simple duplicate checks
    }
#endif
}