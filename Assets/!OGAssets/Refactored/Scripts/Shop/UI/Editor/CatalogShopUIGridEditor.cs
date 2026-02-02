// Assets/.../Editor/CatalogShopUIGridEditor.cs  (inside an Editor folder)
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(CatalogShopUIGrid), true)]
public class CatalogShopUIGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var t = (CatalogShopUIGrid)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Preview"))
            {
                Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Shop Rebuild Preview");
                t.RebuildPreview();               // ← direct call
                EditorUtility.SetDirty(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            }
            if (GUILayout.Button("Clear Preview"))
            {
                Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Shop Clear Preview");
                t.ClearPreview();                 // ← direct call
                EditorUtility.SetDirty(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            }
        }

        EditorGUILayout.HelpBox("Preview builds from Unity IAP Catalog without Play.", MessageType.Info);
    }
}
#endif