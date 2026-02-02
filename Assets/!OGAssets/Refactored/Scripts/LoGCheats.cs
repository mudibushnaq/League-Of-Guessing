#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using OG.Data;

public static class LoGCheats
{
    // --- Entry points ---
    [MenuItem("LoG/Cheats/Solve All But One (Active Mode)")]
    public static void SolveAllButOne_Active() => SolveAllButOneInternal(null);
    
    [MenuItem("LoG/Cheats/Solve All (Active Mode)")]
    public static void SolveAll_Active() => SolveAllLevels(null);

    [MenuItem("LoG/Cheats/Solve All But One/Portrait")]
    public static void SolveAllButOne_Portrait() => SolveAllButOneInternal("Portrait");

    [MenuItem("LoG/Cheats/Solve All But One/Legacy")]
    public static void SolveAllButOne_Legacy() => SolveAllButOneInternal("Legacy");

    [MenuItem("LoG/Cheats/Solve All But One/Default (QWER)")]
    public static void SolveAllButOne_Default() => SolveAllButOneInternal("Default");

    // --- Validators: only enabled in Play Mode ---
    [MenuItem("LoG/Cheats/Solve All But One (Active Mode)", true)]
    [MenuItem("LoG/Cheats/Solve All But One/Portrait", true)]
    [MenuItem("LoG/Cheats/Solve All But One/Legacy", true)]
    [MenuItem("LoG/Cheats/Solve All But One/Default (QWER)", true)]
    public static bool ValidatePlayMode() => Application.isPlaying;

    // --- Core ---
    static void SolveAllButOneInternal(string modeKeyOrNull)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode first (levels/services are runtime-loaded).");
            return;
        }

        // Find your runtime service instance
        var svc = Object.FindFirstObjectByType<LevelsProviderService>();
        if (svc == null || !svc.Initialized)
        {
            Debug.LogWarning("LevelsProviderService not ready. Wait for load, then try again.");
            return;
        }

        // Pick target mode (null => use active mode)
        var modeKey = string.IsNullOrWhiteSpace(modeKeyOrNull) ? svc.ActiveModeKey : modeKeyOrNull;
        if (string.IsNullOrWhiteSpace(modeKey))
        {
            Debug.LogWarning("No active mode yet. Open a menu/scene that sets the active mode or pick a specific menu item.");
            return;
        }

        var levels = svc.GetLevels(modeKey);
        if (levels?.Entries == null || levels.Entries.Count == 0)
        {
            Debug.LogWarning($"No levels found for mode '{modeKey}'.");
            return;
        }

        // Choose one level to leave unsolved:
        // Prefer the last currently unsolved; otherwise just the last entry.
        var entries = levels.Entries;
        var leave = entries.LastOrDefault(e => !GameProgress.Solved.Contains(e.id)) ?? entries[entries.Count - 1];

        int solvedNow = 0;
        foreach (var e in entries)
        {
            if (e == null || e.id == leave.id) continue;
            if (!GameProgress.Solved.Contains(e.id))
            {
                GameProgress.MarkSolved(e.id);
                solvedNow++;
            }
        }

        // Put resume index on the one we left unsolved (handy for testing)
        var idx = entries.IndexOf(leave);
        if (idx >= 0) svc.SaveResumeIndex(idx, modeKey);

        // If you’re using the generic progression service, ping it (optional).
        // Try to find any object that has it injected and call Notify... if exposed.
        var maybeProgression = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Select(mb =>
            {
                var fi = mb.GetType().GetField("_progressionService",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                return fi?.GetValue(mb) as IProgressionService;
            })
            .FirstOrDefault(p => p != null);
        maybeProgression?.NotifyStateMaybeChanged();

        Debug.Log($"[LoG Cheats] Mode '{modeKey}': solved {solvedNow}/{entries.Count - 1}. Left unsolved: '{leave.displayName}' ({leave.id}).");
    }
    
    /// <summary>
    /// Marks ALL levels as solved for the given mode (or the ACTIVE mode if null/empty).
    /// Also moves the resume index to the last level and notifies progression.
    /// Returns how many levels were marked solved (including ones already solved).
    /// </summary>
    public static int SolveAllLevels(string modeKey = null)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LoG] Enter Play Mode before calling SolveAllLevels.");
            return 0;
        }

        var svc = Object.FindFirstObjectByType<LevelsProviderService>();
        if (svc == null || !svc.Initialized)
        {
            Debug.LogWarning("[LoG] LevelsProviderService not ready.");
            return 0;
        }

        var key = string.IsNullOrWhiteSpace(modeKey) ? svc.ActiveModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[LoG] No active mode. Pass a modeKey explicitly.");
            return 0;
        }

        var levels = svc.GetLevels(key);
        if (levels?.Entries == null || levels.Entries.Count == 0)
        {
            Debug.LogWarning($"[LoG] No levels found for mode '{key}'.");
            return 0;
        }

        int count = 0;
        foreach (var e in levels.Entries)
        {
            if (e == null) continue;
            GameProgress.MarkSolved(e.id);
            count++;
        }

        // Put resume index at the end (your UI clamps to show total/total)
        svc.SaveResumeIndex(Mathf.Max(0, levels.Entries.Count - 1), key);

        // Try to notify progression, if present (optional)
        var progression = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Select(mb =>
            {
                var fi = mb.GetType().GetField("_progressionService",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                return fi?.GetValue(mb) as IProgressionService;
            })
            .FirstOrDefault(p => p != null);
        progression?.NotifyStateMaybeChanged();

        Debug.Log($"[LoG] SolveAllLevels: mode '{key}' → solved {count} levels.");
        return count;
    }
}
#endif