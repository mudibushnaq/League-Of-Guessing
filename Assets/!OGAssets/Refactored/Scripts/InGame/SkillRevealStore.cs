using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class SkillRevealStore
{
    // bitmask per levelId. Bits: 0=Q, 1=W, 2=E, 3=R. 1=visible, 0=hidden.
    private const string PP_Key = "LoG_SkillReveal_v1";
    private const string PP_CustomIdKey = "LoG_CustomPlayerId"; // optional override
    
    [Serializable] private class Entry { public string id; public int mask; }
    [Serializable] private class EntryList { public List<Entry> entries = new(); }

    private static Dictionary<string, int> _map;
    
    /// <summary>
    /// Optional: set a stable, account-based player ID (e.g., PlayFab EntityId).
    /// Call this once after login: SkillRevealStore.SetCustomPlayerId(playfabId);
    /// </summary>
    public static void SetCustomPlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return;
        PlayerPrefs.SetString(PP_CustomIdKey, playerId);
        PlayerPrefs.Save();
    }
    
    private static void Load()
    {
        if (_map != null) return;
        var json = PlayerPrefs.GetString(PP_Key, "");
        _map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var list = JsonUtility.FromJson<EntryList>(json);
            if (list?.entries == null) return;
            foreach (var e in list.entries) _map[e.id] = e.mask;
        }
        catch { /* ignore */ }
    }

    private static void Save()
    {
        var list = new EntryList { entries = new List<Entry>(_map.Count) };
        foreach (var kv in _map) list.entries.Add(new Entry { id = kv.Key, mask = kv.Value });
        PlayerPrefs.SetString(PP_Key, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    // Ensure one random skill is visible for this level; returns the mask (bits 0..3)
    static int GetOrCreateMask(string levelId, int skillsCount = 4)
    {
        Load();
        if (_map.TryGetValue(levelId, out var m)) return m;

        if (skillsCount <= 0) skillsCount = 4;
        var rng = new System.Random(Hash(levelId)); // deterministic per level, but fine to use Guid if you want true randomness
        int first = rng.Next(0, skillsCount);      // 0..3
        int mask  = (1 << first);                  // only one visible

        _map[levelId] = mask;
        Save();
        return mask;
    }

    public static int GetMask(string levelId)
    {
        Load();
        return _map.GetValueOrDefault(levelId, 0);
    }

    static void SetMask(string levelId, int mask)
    {
        Load();
        _map[levelId] = mask;
        Save();
    }
    
    // --- NEW: public save/set wrapper used by GameManager ---
    public static void SaveMask(string levelId, int mask)
    {
        SetMask(levelId, mask);
    }

    public static void RevealSkill(string levelId, int index)
    {
        Load();
        int mask = GetOrCreateMask(levelId);
        mask |= (1 << index);
        _map[levelId] = mask;
        Save();
    }

    public static bool IsSkillVisible(int mask, int index) => (mask & (1 << index)) != 0;

    private static int Hash(string s) => s?.GetHashCode() ?? 0;
    
    public static int EnsureHasAtLeastOneBit(string levelId, int skillsCount = 4)
    {
        var mask = GetMask(levelId);
        if (mask != 0) return mask; // already has 1+ unlocked bits

        // first time: pick deterministic first bit, save, and return
        var playerId = GetStablePlayerId();
        int firstIndex = DeterministicIndex(playerId, levelId, skillsCount);
        mask = 1 << firstIndex;
        SetMask(levelId, mask);
        return mask;
    }
    
    public static int NormalizeMask(string levelId, int skillsCount = 4)
    {
        var mask = GetOrCreateMask(levelId, skillsCount);

        int bits = 0;
        for (int i = 0; i < skillsCount; i++)
            if ((mask & (1 << i)) != 0) bits++;

        if (bits == 1) return mask;

        // Fix legacy/bad data: pick deterministic single bit and save it
        var playerId = GetStablePlayerId();
        int firstIndex = DeterministicIndex(playerId, levelId, skillsCount);
        mask = 1 << firstIndex;
        SetMask(levelId, mask);
        return mask;
    }
    
    private static string GetStablePlayerId()
    {
        // Prefer a custom, account-based ID if you have it (e.g., PlayFab ID)
        var custom = PlayerPrefs.GetString(PP_CustomIdKey, "");
        if (!string.IsNullOrEmpty(custom)) return custom;

        // Fallback: device-based ID (may change on some platforms)
        return SystemInfo.deviceUniqueIdentifier ?? "device-unknown";
    }

    private static int DeterministicIndex(string playerId, string levelId, int modulo)
    {
        // SHA256(playerId|levelId) -> take first 4 bytes as unsigned int, mod skillsCount
        using var sha = SHA256.Create();
        var data = Encoding.UTF8.GetBytes(playerId + "|" + levelId);
        var hash = sha.ComputeHash(data);
        uint val = BitConverter.ToUInt32(hash, 0);
        return (int)(val % (uint)modulo);
    }
    
    public static int AddBit(string levelId, int index)
    {
        var m = GetOrCreateMask(levelId);
        m |= (1 << index);
        SetMask(levelId, m);
        return m;
    }
    
    public static int GetVisibleCount(string levelId, int skillsCount = 4)
    {
        var m = GetMask(levelId);
        int c = 0;
        for (int i = 0; i < skillsCount; i++) if ((m & (1 << i)) != 0) c++;
        return c;
    }
    
    public static void ResetLevel(string levelId)
    {
        Load();
        if (_map.Remove(levelId)) Save();
    }
}