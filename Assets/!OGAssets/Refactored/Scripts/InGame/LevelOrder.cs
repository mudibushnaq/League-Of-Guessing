using System;
using System.Collections.Generic;
using System.Linq;
using OG.Data;
using UnityEngine;

public static class LevelOrder
{
    private const string PP_OrderKey = "LoG_LevelOrder_v1";
    private const string PP_SeedKey  = "LoG_UserShuffleSeed";
    
    [Serializable]
    private class StringList { public List<string> values = new(); }

    // --------- PUBLIC API ---------

    /// <summary>
    /// Applies the user's persisted order to <paramref name="all"/>.
    /// First run: shuffles deterministically (per-user seed), saves, and returns that order.
    /// Later runs: merges new IDs into random positions, prunes missing IDs, and returns the ordered list.
    /// </summary>
    public static List<ChampionEntry> ApplyPersistentOrder(List<ChampionEntry> all)
    {
        var orderIds = LoadOrderIds();

        if (orderIds == null || orderIds.Count == 0)
        {
            // First time: deterministically shuffle and save
            var shuffled = all.Select(e => e.id).ToList();
            Shuffle(shuffled, GetOrCreateUserSeed());
            SaveOrder(shuffled);
            return Reorder(all, shuffled);
        }
        
        // Merge: insert any new IDs at random positions
        all = all.OrderBy(e => e.id, StringComparer.OrdinalIgnoreCase).ToList();
        var known   = new HashSet<string>(orderIds, StringComparer.OrdinalIgnoreCase);
        var newOnes = all.Select(e => e.id).Where(id => !known.Contains(id)).ToList();
        if (newOnes.Count > 0)
        {
            var rng = new System.Random(GetOrCreateUserSeed());
            foreach (var id in newOnes)
            {
                int pos = rng.Next(0, orderIds.Count + 1);
                orderIds.Insert(pos, id);
            }
        }

        // Prune IDs no longer present
        var present = new HashSet<string>(all.Select(e => e.id), StringComparer.OrdinalIgnoreCase);
        var pruned  = orderIds.Where(id => present.Contains(id)).ToList();

        if (newOnes.Count > 0 || pruned.Count != orderIds.Count)
            SaveOrder(pruned);

        return Reorder(all, pruned);
    }

    /// <summary>
    /// Moves the level with <paramref name="id"/> to the end of the player's order and persists it.
    /// Safe to call even if id is missing.
    /// </summary>
    public static void MoveToEnd(List<ChampionEntry> all, string id)
    {
        if (all == null || all.Count == 0 || string.IsNullOrEmpty(id)) return;

        int idx = all.FindIndex(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        var item = all[idx];
        all.RemoveAt(idx);
        all.Add(item);

        SaveOrder(all); // persist new order
    }

    /// <summary>Saves the order of the provided entries (by id) to PlayerPrefs.</summary>
    public static void SaveOrder(IEnumerable<ChampionEntry> entries)
        => SaveOrder(entries.Select(e => e.id));

    /// <summary>Saves the order of the provided ids to PlayerPrefs.</summary>
    public static void SaveOrder(IEnumerable<string> ids)
    {
        var list = ids?.ToList() ?? new List<string>();
        var data = new StringList { values = list };
        PlayerPrefs.SetString(PP_OrderKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// NEW: Applies persistent order to a list of level IDs (without needing full ChampionEntry objects).
    /// First run: shuffles deterministically and saves.
    /// Later runs: merges new IDs, prunes missing ones.
    /// </summary>
    public static List<string> ApplyPersistentOrderToIds(List<string> allIds)
    {
        var orderIds = LoadOrderIds();

        if (orderIds == null || orderIds.Count == 0)
        {
            // First time: deterministically shuffle and save
            var shuffled = new List<string>(allIds);
            Shuffle(shuffled, GetOrCreateUserSeed());
            SaveOrder(shuffled);
            return shuffled;
        }

        // Merge: insert any new IDs at random positions
        allIds = allIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        var known = new HashSet<string>(orderIds, StringComparer.OrdinalIgnoreCase);
        var newOnes = allIds.Where(id => !known.Contains(id)).ToList();
        if (newOnes.Count > 0)
        {
            var rng = new System.Random(GetOrCreateUserSeed());
            foreach (var id in newOnes)
            {
                int pos = rng.Next(0, orderIds.Count + 1);
                orderIds.Insert(pos, id);
            }
        }

        // Prune IDs no longer present
        var present = new HashSet<string>(allIds, StringComparer.OrdinalIgnoreCase);
        var pruned = orderIds.Where(id => present.Contains(id)).ToList();

        if (newOnes.Count > 0 || pruned.Count != orderIds.Count)
            SaveOrder(pruned);

        return pruned;
    }

    // --------- INTERNALS ---------

    private static List<ChampionEntry> Reorder(List<ChampionEntry> all, List<string> orderIds)
    {
        var byId  = all.ToDictionary(e => e.id, StringComparer.OrdinalIgnoreCase);
        var outL  = new List<ChampionEntry>(orderIds.Count);
        foreach (var id in orderIds)
            if (byId.TryGetValue(id, out var e)) outL.Add(e);

        // Include any that somehow weren’t listed (safety)
        if (outL.Count < all.Count)
            outL.AddRange(all.Where(e => !orderIds.Contains(e.id, StringComparer.OrdinalIgnoreCase)));

        return outL;
    }

    private static List<string> LoadOrderIds()
    {
        var json = PlayerPrefs.GetString(PP_OrderKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<StringList>(json)?.values ?? new List<string>();
    }

    private static int GetOrCreateUserSeed()
    {
        var seed = PlayerPrefs.GetInt(PP_SeedKey, int.MinValue);
        if (seed == int.MinValue)
        {
            seed = Guid.NewGuid().GetHashCode();
            PlayerPrefs.SetInt(PP_SeedKey, seed);
            PlayerPrefs.Save();
        }
        return seed;
    }

    private static void Shuffle<T>(IList<T> list, int seed)
    {
        var rng = new System.Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    public static int FindIndexById(List<ChampionEntry> levels, string id)
    {
        if (string.IsNullOrEmpty(id) || levels == null) return -1;
        return levels.FindIndex(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static int FindNextUnsolvedIndex(List<ChampionEntry> levels, HashSet<string> solved)
    {
        if (levels == null || levels.Count == 0) return -1;
        for (int i = 0; i < levels.Count; i++)
            if (!solved.Contains(levels[i].id)) return i;
        return -1;
    }

    public static int FindNextUnsolvedIndexFrom(List<ChampionEntry> levels, HashSet<string> solved, int startExclusive)
    {
        if (levels == null || levels.Count == 0) return -1;
        int n = levels.Count;

        for (int i = startExclusive + 1; i < n; i++)
            if (!solved.Contains(levels[i].id)) return i;

        for (int i = 0; i <= startExclusive && i < n; i++)
            if (!solved.Contains(levels[i].id)) return i;

        return -1;
    }

    /// <summary>
    /// Decide which index to start/resume at, given last current id (if any).
    /// - If last id exists and is unsolved → resume it
    /// - If last id solved → pick next unsolved after it
    /// - Else → first unsolved
    /// </summary>
    public static int ComputeResumeIndex(List<ChampionEntry> levels, HashSet<string> solved, string lastCurrentId)
    {
        if (levels == null || levels.Count == 0) return -1;

        // ✅ Always resume exactly the last opened level if found
        if (!string.IsNullOrEmpty(lastCurrentId))
        {
            int idx = FindIndexById(levels, lastCurrentId);
            if (idx >= 0) return idx;
        }

        // Fallback: first unsolved; if all solved, start at 0
        int first = FindNextUnsolvedIndex(levels, solved);
        return first >= 0 ? first : 0;
    }
    
    public static int ComputeResumeIndex_old(List<ChampionEntry> levels, HashSet<string> solved, string lastCurrentId)
    {
        if (levels == null || levels.Count == 0) return -1;

        if (!string.IsNullOrEmpty(lastCurrentId))
        {
            int idx = FindIndexById(levels, lastCurrentId);
            if (idx >= 0)
            {
                if (!solved.Contains(lastCurrentId))
                    return idx; // resume exactly there
                // else go to the next unsolved after it
                int next = FindNextUnsolvedIndexFrom(levels, solved, idx);
                if (next >= 0) return next;
            }
        }

        return FindNextUnsolvedIndex(levels, solved);
    }
}
