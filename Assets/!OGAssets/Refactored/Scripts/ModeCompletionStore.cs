using UnityEngine;

public static class ModeCompletionStore
{
    static string KDone(string mode)   => $"LoG.ModeCompleted.{mode}";
    static string KShown(string mode)  => $"LoG.ModeCongratsShown.{mode}";

    public static bool IsCompleted(string mode) =>
        !string.IsNullOrWhiteSpace(mode) && PlayerPrefs.GetInt(KDone(mode), 0) == 1;

    public static void MarkCompleted(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        PlayerPrefs.SetInt(KDone(mode), 1);
        PlayerPrefs.Save();
    }

    /// Show-once gate for the “Level Completed” window.
    public static bool TryConsumeCongratsOnce(string mode)
    {
        if (!IsCompleted(mode)) return false;
        var k = KShown(mode);
        if (PlayerPrefs.GetInt(k, 0) == 1) return false; // already shown once
        PlayerPrefs.SetInt(k, 1);
        PlayerPrefs.Save();
        return true;
    }

    // Optional: keep flags consistent if new content arrives later
    public static void ClearCompleted(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        PlayerPrefs.DeleteKey(KDone(mode));
        PlayerPrefs.DeleteKey(KShown(mode));
        PlayerPrefs.Save();
    }
}