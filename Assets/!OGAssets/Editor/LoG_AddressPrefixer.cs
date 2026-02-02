#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class LoG_AddressPrefixer
{
    // Change these to match your group names in Addressables window
    const string DefaultGroupName = "LOG Default Levels";
    const string LegacyGroupName  = "LOG Legacy Levels";
    const string PortraitGroupName  = "LOG Portrait Levels";

    [MenuItem("LoG/Addressables/Prefix addresses for Default group (→ Default/)")]
    public static void Prefix_Default() => PrefixGroup(DefaultGroupName, "Default/");

    [MenuItem("LoG/Addressables/Prefix addresses for Legacy group (→ Legacy/)")]
    public static void Prefix_Legacy()  => PrefixGroup(LegacyGroupName,  "Legacy/");
    
    [MenuItem("LoG/Addressables/Prefix addresses for Portrait group (→ Portrait/)")]
    public static void Prefix_Portrait()  => PrefixGroup(PortraitGroupName,  "Portrait/");

    /// Generic: prefix a group's entry addresses with the given prefix ("Default/" or "Legacy/")
    public static void PrefixGroup(string groupName, string prefix)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressables settings not found.");
            return;
        }

        var group = settings.FindGroup(groupName);
        if (group == null)
        {
            Debug.LogError($"Group '{groupName}' not found.");
            return;
        }

        int changed = 0;
        Undo.RecordObject(settings, $"Prefix Addresses ({groupName})");

        // NOTE: group.entries is available in the editor API.
        // If your Addressables version hides it, see the alternative loop below.
        foreach (var entry in group.entries)
        {
            if (entry == null) continue;

            // Start from the current address. If empty, fall back to file name (no extension).
            var addr = entry.address ?? string.Empty;
            if (string.IsNullOrEmpty(addr))
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                addr = Path.GetFileNameWithoutExtension(path);
            }

            // Normalize: strip extension and any known prefixes to avoid double prefixing.
            addr = StripExt(addr);
            addr = StripKnownPrefix(addr, "Default/");
            addr = StripKnownPrefix(addr, "Legacy/");

            var newAddr = prefix + addr;
            if (newAddr == entry.address) continue;   // already correct

            entry.SetAddress(newAddr);
            changed++;
        }

        // Mark dirty & save
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LoG] Prefixed {changed} address(es) in group '{groupName}' with '{prefix}'.");
    }

    static string StripExt(string s)
    {
        int i = s.LastIndexOf('.');
        return (i >= 0) ? s.Substring(0, i) : s;
    }

    static string StripKnownPrefix(string s, string p) => s.StartsWith(p) ? s.Substring(p.Length) : s;
}
#endif
