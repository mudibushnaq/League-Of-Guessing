// Place this file under Assets/Editor/ (or any folder named "Editor").
// Unity 2020.3+ recommended.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ScreenshotToolWindow : EditorWindow
{
    private const string PrefDirKey = "ScreenshotTool.SaveDir";
    private const string PrefSuperKey = "ScreenshotTool.SuperSize";
    private const string PrefWidthKey = "ScreenshotTool.Width";
    private const string PrefHeightKey = "ScreenshotTool.Height";
    private const string PrefNameKey = "ScreenshotTool.Name";

    private string _saveDirectory;
    private int _superSize = 1;                 // Game View scale (1x, 2x, 4x)
    private int _width = 1920;                  // Camera/Scene capture width
    private int _height = 1080;                 // Camera/Scene capture height
    private string _baseFileName = "screenshot";// base name without extension
    private bool _openFolderAfterCapture = false;
    private bool _transparentBG = false;        // for Camera capture only

    [MenuItem("Tools/Screenshot/Open Screenshot Tool")] 
    public static void OpenWindow()
    {
        var win = GetWindow<ScreenshotToolWindow>(true, "Screenshot Tool");
        win.minSize = new Vector2(380, 280);
        win.LoadPrefs();
        win.Show();
    }

    [MenuItem("Tools/Screenshot/Capture Game View %#&s")] // Ctrl/Cmd+Shift+Alt+S
    private static void QuickCaptureGameView()
    {
        var dir = LoadString(PrefDirKey, DefaultDirectory());
        var super = LoadInt(PrefSuperKey, 1);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, BuildFileName(LoadString(PrefNameKey, "screenshot"), "png"));
        ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, super));
        Debug.Log($"[Screenshot] Saved Game View to: {path}");
    }

    private void OnGUI()
    {
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Save Location", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _saveDirectory = EditorGUILayout.TextField(_saveDirectory);
            if (GUILayout.Button("Choose…", GUILayout.Width(90)))
            {
                var picked = EditorUtility.OpenFolderPanel("Choose Screenshot Folder", _saveDirectory, "");
                if (!string.IsNullOrEmpty(picked)) _saveDirectory = picked;
            }
        }

        GUILayout.Space(6);
        EditorGUILayout.LabelField("File Name", EditorStyles.boldLabel);
        _baseFileName = EditorGUILayout.TextField("Base", _baseFileName);
        EditorGUILayout.LabelField("Preview", BuildFileName(_baseFileName, "png"));

        GUILayout.Space(6);
        EditorGUILayout.LabelField("Game View Capture", EditorStyles.boldLabel);
        _superSize = EditorGUILayout.IntPopup("Scale", _superSize, new[] { "1x", "2x", "4x" }, new[] { 1, 2, 4 });
        if (GUILayout.Button("Capture Game View"))
        {
            CaptureGameView();
        }

        GUILayout.Space(6);
        EditorGUILayout.LabelField("Scene / Camera Capture", EditorStyles.boldLabel);
        _width = Mathf.Max(8, EditorGUILayout.IntField("Width", _width));
        _height = Mathf.Max(8, EditorGUILayout.IntField("Height", _height));
        _transparentBG = EditorGUILayout.ToggleLeft("Transparent background (Camera only)", _transparentBG);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Scene View")) CaptureSceneView();
            if (GUILayout.Button("Capture Selected Camera")) CaptureSelectedCamera();
        }

        GUILayout.FlexibleSpace();
        _openFolderAfterCapture = EditorGUILayout.ToggleLeft("Open folder after capture", _openFolderAfterCapture);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Folder")) OpenFolder(_saveDirectory);
            if (GUILayout.Button("Save Settings")) SavePrefs();
        }
    }

    private void CaptureGameView()
    {
        EnsureDirectory();
        string path = Path.Combine(_saveDirectory, BuildFileName(_baseFileName, "png"));
        ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, _superSize));
        Debug.Log($"[Screenshot] Saved Game View to: {path}");
        if (_openFolderAfterCapture) OpenFolder(_saveDirectory);
    }

    private void CaptureSceneView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null)
        {
            Debug.LogWarning("[Screenshot] No active Scene View camera found.");
            return;
        }
        EnsureDirectory();
        string path = Path.Combine(_saveDirectory, BuildFileName(_baseFileName + "_scene", "png"));
        CaptureCameraToPng(sv.camera, path, _width, _height, transparent: false);
    }

    private void CaptureSelectedCamera()
    {
        Camera cam = null;
        if (Selection.activeGameObject != null)
            cam = Selection.activeGameObject.GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Screenshot] No Camera found. Select a Camera or set one as MainCamera.");
            return;
        }
        EnsureDirectory();
        string path = Path.Combine(_saveDirectory, BuildFileName(_baseFileName + "_cam", "png"));
        CaptureCameraToPng(cam, path, _width, _height, transparent: _transparentBG);
    }

    private static void CaptureCameraToPng(Camera cam, string path, int width, int height, bool transparent)
    {
        // Backup camera state
        var prevFlags = cam.clearFlags;
        var prevBG = cam.backgroundColor;
        var prevTarget = cam.targetTexture;

        if (transparent)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
        }

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            UnityEngine.Object.DestroyImmediate(tex);

            Debug.Log($"[Screenshot] Saved Camera capture to: {path}");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            cam.clearFlags = prevFlags;
            cam.backgroundColor = prevBG;
            rt.Release();
        }

        if (path.StartsWith(Application.dataPath))
            AssetDatabase.Refresh();

        // Note: True transparency depends on your pipeline and materials. In URP/HDRP, ensure shaders and output support alpha.
    }

    private void EnsureDirectory()
    {
        if (string.IsNullOrEmpty(_saveDirectory)) _saveDirectory = DefaultDirectory();
        if (!Directory.Exists(_saveDirectory)) Directory.CreateDirectory(_saveDirectory);
    }

    private static void OpenFolder(string dir)
    {
        if (Directory.Exists(dir))
            EditorUtility.RevealInFinder(dir);
        else
            Debug.LogWarning($"[Screenshot] Folder not found: {dir}");
    }

    private void LoadPrefs()
    {
        _saveDirectory = LoadString(PrefDirKey, DefaultDirectory());
        _superSize = LoadInt(PrefSuperKey, 1);
        _width = LoadInt(PrefWidthKey, 1920);
        _height = LoadInt(PrefHeightKey, 1080);
        _baseFileName = LoadString(PrefNameKey, "screenshot");
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(PrefDirKey, _saveDirectory);
        EditorPrefs.SetInt(PrefSuperKey, _superSize);
        EditorPrefs.SetInt(PrefWidthKey, _width);
        EditorPrefs.SetInt(PrefHeightKey, _height);
        EditorPrefs.SetString(PrefNameKey, _baseFileName);
        Debug.Log("[Screenshot] Settings saved.");
    }

    private static string DefaultDirectory()
    {
        // Default: Desktop/Screenshots (cross‑platform)
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(string.IsNullOrEmpty(desktop) ? Application.dataPath : desktop, "Screenshots");
    }

    private static string BuildFileName(string baseName, string extNoDot)
    {
        // Example: screenshot_2025-09-28_11-12-34.png
        return $"{Sanitize(baseName)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.{extNoDot}";
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }

    private static string LoadString(string key, string fallback) => EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : fallback;
    private static int LoadInt(string key, int fallback) => EditorPrefs.HasKey(key) ? EditorPrefs.GetInt(key) : fallback;
}
