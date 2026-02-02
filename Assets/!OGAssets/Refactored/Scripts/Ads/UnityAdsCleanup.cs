#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnityAdsCleanup
{
    static UnityAdsCleanup()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    static void OnState(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode ||
            s == PlayModeStateChange.EnteredEditMode)
        {
            CleanupNow();
            // Run again on next editor tick in case Ads creates it during teardown.
            EditorApplication.delayCall += CleanupNow;
        }
    }
    
    static void CleanupNow()
    {
        var go = GameObject.Find("UnityEngine_UnityAds_CoroutineExecutor");
        if (go) Object.DestroyImmediate(go);
        var go2 = GameObject.Find("UnityAds_CoroutineExecutor");
        if (go2) Object.DestroyImmediate(go2);
    }
}
#endif
