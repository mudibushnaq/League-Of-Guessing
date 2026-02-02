#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class LoG_BatchSliceAndAddress
{
    // -------- config --------
    private const string GROUP_NAME = "LOG Levels";
    private const string LABEL_NAME = "LOGLevels";
    private const int COLUMNS = 2;
    private const int ROWS    = 2;

    [MenuItem("LoG/Batch: Slice 2x2 + Addressables (Selected)")]
    public static void SliceSelectedAndMakeAddressable()
    {
        var guids = Selection.assetGUIDs;
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("LoG", "Select 1+ textures (PNG) in the Project window.", "OK");
            return;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings ??
                       AddressableAssetSettingsDefaultObject.GetSettings(true);

        var group = settings.FindGroup(GROUP_NAME) ??
                    settings.CreateGroup(GROUP_NAME, false, false, false, null, typeof(BundledAssetGroupSchema));

        int processed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!tex) continue;

            if (!SliceTexture2x2_Serialized(path)) continue;

            // Mark the texture as addressable; sub-sprites are referenced via "{address}[SubName]"
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            entry.parentGroup = group;

            var baseId = Path.GetFileNameWithoutExtension(path); // e.g., "ahri"
            // decide prefix from the target group name
            var prefix = group.name.Contains("Legacy") ? "Legacy/" :
                group.name.Contains("Default") ? "Default/" : "";

            // baseId: file name without extension (e.g., "Aatrox")
            entry.SetAddress(prefix + baseId);

            if (!settings.GetLabels().Contains(LABEL_NAME))
                settings.AddLabel(LABEL_NAME);
            entry.SetLabel(LABEL_NAME, true, true);

            processed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        EditorUtility.DisplayDialog("LoG", $"Processed {processed} texture(s).", "Nice");
    }

    [MenuItem("LoG/Batch: Slice 2x2 + Addressables (Folder...)")]
    public static void SliceFolderAndMakeAddressable()
    {
        var start = "Assets";
        var dirAbs = EditorUtility.OpenFolderPanel("Pick a folder under Assets", start, "");
        if (string.IsNullOrEmpty(dirAbs)) return;

        // Convert absolute path to relative under Assets
        string dirRel;
        if (dirAbs.StartsWith(Application.dataPath))
            dirRel = "Assets" + dirAbs.Substring(Application.dataPath.Length);
        else
        {
            EditorUtility.DisplayDialog("LoG", "Folder must be under your project 'Assets' directory.", "OK");
            return;
        }

        var texPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { dirRel })
                                    .Select(AssetDatabase.GUIDToAssetPath)
                                    .ToArray();
        if (texPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("LoG", "No textures found in that folder.", "OK");
            return;
        }

        // Reuse the Selected flow
        Selection.objects = texPaths
            .Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p))
            .Where(t => t != null)
            .Cast<UnityEngine.Object>()
            .ToArray();

        SliceSelectedAndMakeAddressable();
    }

    /// <summary>
    /// Slices a texture into 2x2 sub-sprites (Q/W/E/R) by writing importer data via SerializedObject.
    /// Works on Unity 6.x without Sprite Editor provider APIs.
    /// </summary>
    private static bool SliceTexture2x2_Serialized(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;

        // Load the texture for dimensions
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (!tex)
        {
            Debug.LogWarning($"[LoG] Could not load texture at {assetPath}");
            return false;
        }

        int width  = tex.width;
        int height = tex.height;
        if (width <= 0 || height <= 0) return false;

        int cellW  = Mathf.Max(1, width  / COLUMNS);
        int cellH  = Mathf.Max(1, height / ROWS);

        // Prepare four rects: Unity's rect origin is bottom-left
        // Top row (y = height - cellH): Q (left), W (right)
        // Bottom row (y = 0):           E (left), R (right)
        var names = new[] { "Q", "W", "E", "R" };
        var rects = new[]
        {
            new Rect(0 * cellW, height - cellH, cellW, cellH), // Q
            new Rect(1 * cellW, height - cellH, cellW, cellH), // W
            new Rect(0 * cellW, 0,             cellW, cellH), // E
            new Rect(1 * cellW, 0,             cellW, cellH), // R
        };

        // Write importer spritesheet via SerializedObject (Unity 6 friendly)
        var so = new SerializedObject(importer);
        // Ensure correct type/mode serialized too
        so.FindProperty("m_TextureType").intValue = 8; // 8 == Sprite
        so.FindProperty("m_SpriteMode").intValue = 2; // 2 == Multiple

        var spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
        if (spritesProp == null)
        {
            Debug.LogError("[LoG] Could not access m_SpriteSheet.m_Sprites on importer (Unity internal changed?).");
            return false;
        }

        spritesProp.ClearArray();
        spritesProp.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            var elem = spritesProp.GetArrayElementAtIndex(i);

            // Name
            elem.FindPropertyRelative("m_Name").stringValue = names[i];

            // Rect (x,y,w,h)
            var rectProp = elem.FindPropertyRelative("m_Rect");
            if (rectProp != null)
            {
                rectProp.FindPropertyRelative("x")     .floatValue = (rects[i].x);
                rectProp.FindPropertyRelative("y")     .floatValue =(rects[i].y);
                rectProp.FindPropertyRelative("width") .floatValue =(rects[i].width);
                rectProp.FindPropertyRelative("height").floatValue =(rects[i].height);
            }

            // Alignment & pivot (center)
            elem.FindPropertyRelative("m_Alignment").intValue = ((int)SpriteAlignment.Center);
            var pivotProp = elem.FindPropertyRelative("m_Pivot");
            if (pivotProp != null)
            {
                pivotProp.FindPropertyRelative("x").floatValue =(0.5f);
                pivotProp.FindPropertyRelative("y").floatValue =(0.5f);
            }

            // Border (Vector4 zero)
            var borderProp = elem.FindPropertyRelative("m_Border");
            if (borderProp != null)
            {
                borderProp.FindPropertyRelative("x").floatValue = (0f);
                borderProp.FindPropertyRelative("y").floatValue =(0f);
                borderProp.FindPropertyRelative("z").floatValue =(0f);
                borderProp.FindPropertyRelative("w").floatValue =(0f);
            }

            // Sprite ID (GUID) if present (Unity stores GUIDs as string sometimes)
            var idProp = elem.FindPropertyRelative("m_SpriteID");
            if (idProp != null)
            {
                var guid = GUID.Generate().ToString();
                idProp.stringValue = guid;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            AssetDatabase.StartAssetEditing();
            importer.SaveAndReimport();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        return true;
    }
}
#endif
