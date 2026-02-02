using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="LoG/Shop/Presentation")]
public sealed class ShopPresentation : ScriptableObject
{
    [Serializable] public class Entry
    {
        public string catalogId;          // e.g. "100ip_pack"
        public Sprite icon;               // product icon
        public string badgeText;          // e.g. "BEST VALUE"
        public bool featured;             // can sort featured first
        public int sortWeight;            // lower = earlier
    }
    public List<Entry> entries = new();
    public bool TryGet(string id, out Entry e) { e = entries.Find(x => string.Equals(x.catalogId,id,StringComparison.OrdinalIgnoreCase)); return e!=null; }
}