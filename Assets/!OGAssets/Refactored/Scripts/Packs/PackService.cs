// Assets/!OGAssets/Refactored/Scripts/Packs/PackService.cs
#nullable enable
using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(IPackService))]
public sealed class PackService : IPackService, IProjectInitializable
{
    int IProjectInitializable.Order => 50;
    
    [Inject] private IAdsService _ads;
    [Inject] private IRewardGranter _granter;

    const string PP_LAST   = "PKG_LAST_";    // ticks
    const string PP_TALLY  = "PKG_TALLY_";   // yyyyMMdd:int

    UniTask IProjectInitializable.Initialize()
    {
        Debug.Log("[IProjectInitializable.Initialize] PackService ready.");
        return UniTask.CompletedTask;
    }
    
    public bool CanClaim(PackDefinition def, out TimeSpan remaining, out int claimedToday)
    {
        remaining    = GetRemaining(def);
        claimedToday = GetClaimsToday(def);
        if (remaining > TimeSpan.Zero) return false;
        if (def.maxClaimsPerDay > 0 && claimedToday >= def.maxClaimsPerDay) return false;
        return true;
    }

    public TimeSpan GetRemaining(PackDefinition def)
    {
        var last = LoadLast(def);
        if (last == DateTime.MinValue) return TimeSpan.Zero;
        var next = last + TimeSpan.FromSeconds(def.cooldownSeconds);
        var rem  = next - DateTime.UtcNow;
        return rem > TimeSpan.Zero ? rem : TimeSpan.Zero;
    }

    public int GetClaimsToday(PackDefinition def)
    {
        var key = PP_TALLY + def.packId;
        var today = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var packed = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(packed)) return 0;

        var parts = packed.Split(':');
        if (parts.Length != 2) return 0;
        if (parts[0] != today) return 0;
        return int.TryParse(parts[1], out var n) ? n : 0;
    }

    public async UniTask<bool> ClaimAsync(PackDefinition def, Transform source = null)
    {
        // Validate timing & daily cap
        if (!CanClaim(def, out _, out _)) return false;

        // Gate
        if (def.gate == PackGateType.UnityAdsRewarded)
        {
            //if (_ads == null) { Debug.LogWarning("[Packs] IAdsService missing."); return false; }

            if (!_ads.IsRewardedReady)
                await _ads.PreloadRewardedAsync();

            bool finished = await _ads.ShowRewardedAsync(onReward: async () =>
            {
                await _granter.GrantAsync(def.rewards, source);
            });

            if (!finished) return false; // didn’t show/complete
        }
        else
        {
            await _granter.GrantAsync(def.rewards, source);
        }

        // Persist cooldown + tally
        SaveLast(def, DateTime.UtcNow);
        IncToday(def);
        return true;
    }

    // -------- persistence --------
    static DateTime LoadLast(PackDefinition def)
    {
        var key = PP_LAST + def.packId;
        if (!PlayerPrefs.HasKey(key)) return DateTime.MinValue;
        var ticksStr = PlayerPrefs.GetString(key, "0");
        return long.TryParse(ticksStr, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
    }

    static void SaveLast(PackDefinition def, DateTime whenUtc)
    {
        PlayerPrefs.SetString(PP_LAST + def.packId, whenUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    static void IncToday(PackDefinition def)
    {
        var key = PP_TALLY + def.packId;
        var today = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var current = 0;
        var packed = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(packed))
        {
            var parts = packed.Split(':');
            if (parts.Length == 2 && parts[0] == today)
                int.TryParse(parts[1], out current);
        }
        current++;
        PlayerPrefs.SetString(key, $"{today}:{current}");
        PlayerPrefs.Save();
    }
}