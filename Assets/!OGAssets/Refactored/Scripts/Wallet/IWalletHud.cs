using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum WalletType { LP, Keys }

public interface IWalletHud
{
    WalletType   Type { get; }
    RectTransform IconRect { get; }     // where FX should land
    TMP_Text     AmountLabel { get; }   // the number to update
    RectTransform IconsLayer { get; }   // parent canvas/layer for flying icons
    
    // NEW: mark exactly which HUD should receive RewardFX
    bool RewardFXActivated { get; }      // only one (or the "best") per WalletType should be true
    int  Priority { get; }               // optional tie-breaker; higher = preferred
    
    // NEW: allow registry/binder to set the value cleanly
    void SetAmount(int value);
}

public static class HudRegistry
{
    // LP -> [hud, hud, ...], Keys -> [hud, hud, ...]
    private static readonly Dictionary<WalletType, List<IWalletHud>> _map = new();

    public static void Register(IWalletHud hud)
    {
        if (hud == null) return;
        if (!_map.TryGetValue(hud.Type, out var list))
        {
            list = new List<IWalletHud>();
            _map[hud.Type] = list;
        }
        if (!list.Contains(hud)) list.Add(hud);
    }
    public static void Unregister(IWalletHud hud)
    {
        if (hud == null) return;
        if (!_map.TryGetValue(hud.Type, out var list)) return;
        list.Remove(hud);
        if (list.Count == 0) _map.Remove(hud.Type);
    }
    public static IReadOnlyList<IWalletHud> GetAll(WalletType t)
        => _map.TryGetValue(t, out var list) ? (IReadOnlyList<IWalletHud>)list : Array.Empty<IWalletHud>();

    // Preferred target for RewardFX: RewardFXActivated=true, pick highest Priority; fallback to last registered
    public static bool TryGetActive(WalletType t, out IWalletHud hud)
    {
        hud = null;
        if (!_map.TryGetValue(t, out var list) || list.Count == 0) return false;

        IWalletHud best = null;
        foreach (var h in list)
            if (h is { RewardFXActivated: true })
                best = (best == null || h.Priority > best.Priority) ? h : best;

        hud = best ?? list[^1];
        return hud != null;
    }
}