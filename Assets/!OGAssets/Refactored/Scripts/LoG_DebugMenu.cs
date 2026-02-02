#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class LoG_DebugMenu
{
    // Utility: get the running GameManager (Play Mode)
    private static GameManager GM()
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (!gm) Debug.LogWarning("[LoG] GameManager not found in scene.");
        return gm;
    }
    
    private static PortraitGameManager PGM()
    {
        var pgm = Object.FindFirstObjectByType<PortraitGameManager>();
        if (!pgm) Debug.LogWarning("[LoG] PortraitGameManager not found in scene.");
        return pgm;
    }
    
    private static MenuController MC()
    {
        var mc = Object.FindFirstObjectByType<MenuController>();
        if (!mc) Debug.LogWarning("[LoG] MenuController not found in scene.");
        return mc;
    }

    // ---------- Auto Solve ----------
    [MenuItem("LoG/Auto Solve Current Level %#F6")] // Ctrl/Cmd+Shift+F6
    private static void AutoSolve()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[LoG] Enter Play Mode to use this."); return; }
        var gm = GM();
        var pgm = PGM();
        if (gm)
        {
            gm.Debug_AutoSolve();
        }

        if (pgm)
        {
            pgm.Debug_AutoSolve();
        }
    }

    [MenuItem("LoG/Auto Solve Current Level %#F6", true)]
    private static bool AutoSolve_Validate() => Application.isPlaying;

    // ---------- Unlock All Skills (current) ----------
    [MenuItem("LoG/Unlock All Skills (Current Level) %#F7")] // Ctrl/Cmd+Shift+F7
    private static void UnlockAllSkills()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[LoG] Enter Play Mode to use this."); return; }
        var gm = GM(); if (!gm) return;
        //gm.Debug_UnlockAllSkillsCurrent();
    }

    [MenuItem("LoG/Unlock All Skills (Current Level) %#F7", true)]
    private static bool UnlockAllSkills_Validate() => Application.isPlaying;

    // ---------- Skip Current Level ----------
    [MenuItem("LoG/Skip Current Level (Costs Keys) %#F8")] // Ctrl/Cmd+Shift+F8
    private static void SkipLevel()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[LoG] Enter Play Mode to use this."); return; }
        var gm = GM(); if (!gm) return;
        //gm.Debug_SkipCurrent();
    }

    [MenuItem("LoG/Skip Current Level (Costs Keys) %#F8", true)]
    private static bool SkipLevel_Validate() => Application.isPlaying;

    // ---------- Give 100 Keys ----------
    [MenuItem("LoG/Give 100 Keys %#K")] // Ctrl/Cmd+Shift+K
    private static void Give100Keys()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[LoG] Enter Play Mode to use this."); return; }
        Debug_GiveKeys(100);
    }

    [MenuItem("LoG/Give 100 Keys %#K", true)]
    private static bool Give100Keys_Validate() => Application.isPlaying;

    // ---------- Give 1000 LP ----------
    [MenuItem("LoG/Give 1000 LP %#L")] // Ctrl/Cmd+Shift+L
    private static void Give1000LP()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[LoG] Enter Play Mode to use this."); return; }
        Debug_GiveLP(1000);
    }

    [MenuItem("LoG/Give 1000 LP %#L", true)]
    private static bool Give1000LP_Validate() => Application.isPlaying;
    
    static void Debug_GiveKeys(int amount)
    {
        CurrencyStore.AddKeys(amount);
    }

    static void Debug_GiveLP(int amount)
    {
        CurrencyStore.AddLP(amount);
    }
}
#endif
