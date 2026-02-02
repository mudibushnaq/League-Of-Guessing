#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;

public static class CcdPipelineLauncher
{
    [MenuItem("OG/Build/CCD Build & Publish")]
    public static void Launch()
    {
        // Always open credentials popup
        var popup = ScriptableObject.CreateInstance<CcdCredentialsPopup>();
        popup.titleContent = new GUIContent("CCD Credentials");
        popup.minSize = new Vector2(450, 300);
        popup.ShowUtility();
    }
}
#endif