// Assets/Editor/BuildAddressablesThenAab.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class BuildAddressablesThenAab
{
    // Menu entry
    [MenuItem("Build/Addressables + AAB (reuse Addressables)")]
    public static void BuildMenu()
    {
        var aabPath = Path.Combine("Builds", "Android", "game.aab");
        BuildAddressablesAndAab(aabPath, cleanAddrFirst: false);
    }

    // CI entry: -executeMethod BuildAddressablesThenAab.CI
    public static void CI()
    {
        var outPath = Environment.GetEnvironmentVariable("AAB_OUT");
        if (string.IsNullOrEmpty(outPath))
            outPath = Path.Combine("Builds", "Android", "game.aab");

        BuildAddressablesAndAab(outPath, cleanAddrFirst: false);
    }

    public static void BuildAddressablesAndAab(string aabOutputPath, bool cleanAddrFirst)
    {
        EnsureAndroidTarget();

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found. Create Addressables via Window ▸ Asset Management ▸ Addressables.");
            return;
        }

        // 1) (Optional) clean, then build Addressables once
        try
        {
            if (cleanAddrFirst)
            {
                // Some versions don't have CleanPlayerContent as static; guard with reflection.
                var clean = typeof(AddressableAssetSettings).GetMethod("CleanPlayerContent", BindingFlags.Public | BindingFlags.Static);
                clean?.Invoke(null, null);
                Debug.Log("[Build] Addressables: Cleaned previous player content.");
            }

            Debug.Log("[Build] Addressables: Building player content…");
            // BuildPlayerContent() sometimes returns void, sometimes a result type with Error.
            var build = typeof(AddressableAssetSettings).GetMethod("BuildPlayerContent", BindingFlags.Public | BindingFlags.Static);
            if (build == null)
                throw new Exception("AddressableAssetSettings.BuildPlayerContent not found.");

            var result = build.Invoke(null, null); // may be null if return type = void

            // If there is a result object and it has an Error property, check it
            if (result != null)
            {
                var errorProp = result.GetType().GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);
                if (errorProp != null)
                {
                    var error = errorProp.GetValue(result) as string;
                    if (!string.IsNullOrEmpty(error))
                        throw new Exception("Addressables build failed: " + error);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Build] Addressables build failed:\n{e}");
            throw new UnityEditor.Build.BuildFailedException(e.Message);
        }

        // 2) Build AAB reusing those Addressables (do NOT rebuild with player)
        //    Handle both old (bool) and new (enum PlayerBuildOption) API shapes.
        var prop = typeof(AddressableAssetSettings).GetProperty("BuildAddressablesWithPlayerBuild",
            BindingFlags.Public | BindingFlags.Instance);
        object prev = null;
        try
        {
            if (prop != null)
            {
                prev = prop.GetValue(settings, null);

                if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(settings, false, null); // reuse, don't rebuild
                }
                else if (prop.PropertyType.IsEnum)
                {
                    // Prefer enum value named "DoNotBuildWithPlayer"
                    var enumVals = Enum.GetValues(prop.PropertyType);
                    object targetVal = enumVals.GetValue(0);
                    foreach (var v in enumVals)
                        if (v.ToString().IndexOf("DoNotBuildWithPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
                            targetVal = v;
                    prop.SetValue(settings, targetVal, null);
                }
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(aabOutputPath) ?? "Builds");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new UnityEditor.Build.BuildFailedException("No enabled scenes in Build Settings.");

            EditorUserBuildSettings.buildAppBundle = true; // produce AAB
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = aabOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log("[Build] Building Android App Bundle (reusing Addressables) …");
            var report = BuildPipeline.BuildPlayer(opts);

            if (report.summary.result != BuildResult.Succeeded)
            {
                var msg = $"AAB build failed: {report.summary.result}";
                Debug.LogError(msg);
                throw new UnityEditor.Build.BuildFailedException(msg);
            }

            Debug.Log($"[Build] AAB built at: {aabOutputPath}\nSize: {report.summary.totalSize / (1024f * 1024f):0.0} MB");
        }
        finally
        {
            // Restore the original Addressables setting if we changed it
            if (prop != null && prev != null)
            {
                try
                {
                    prop.SetValue(settings, prev, null);
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
                catch { /* ignore restore errors */ }
            }
        }
    }

    private static void EnsureAndroidTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[Build] Switching Active Build Target → Android…");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }
}
#endif
