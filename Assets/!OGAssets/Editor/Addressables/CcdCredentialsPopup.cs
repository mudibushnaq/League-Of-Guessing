#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public sealed class CcdCredentialsPopup : OdinEditorWindow
{
    // Returned data container
    public sealed class Creds
    {
        public string ProjectId;
        public string KeyId;
        public string Secret;
        public string AuthorizationHeader;
    }

    private Action<Creds> _onOk;
    
    private Texture2D _logoTexture;
    private const string LogoPath = "Assets/!OGAssets/Textures/UI/OGLogoSplash.png";
    
    private const string PrefKey_ProjectId = "CCD_ProjectId";
    private const string PrefKey_KeyId = "CCD_KeyId";
    private const string PrefKey_Secret = "CCD_Secret";
    private const string PrefKey_AuthorizationHeader = "CCD_AuthorizationHeader";
    private const string PrefKey_Window = "CCD_SA_TOKEN_WINDOW_V5";
    
    [Title("Unity Services / CCD Credentials")]
    [PropertyOrder(-10)]
    [LabelText("Project ID")]
    public string ProjectId;

    [LabelText("Service Account Key ID")]
    public string KeyId;

    [LabelText("Service Account Secret")]
    public string Secret;

    [LabelText("Authorization Header")]
    [InfoBox("Example: Basic <base64 keyId:secret>  OR  Bearer <JWT>", InfoMessageType.None)]
    public string AuthorizationHeader;

    public static void OpenForLaunch(Action<Creds> onOk)
    {
        var w = CreateInstance<CcdCredentialsPopup>();
        w._onOk = onOk;
        w.titleContent = new GUIContent("CCD Credentials");
        w.minSize = new Vector2(520, 190);

        // Prefill from saved prefs (optional)
        if (EditorPrefs.HasKey(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_Window)))
        {
            var temp = CreateInstance<CcdBuildAndPublish>();
            JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_Window)), temp);

            w.ProjectId           = temp.Auth.ProjectId?.Trim() ?? "";
            w.KeyId               = temp.Auth.KeyId?.Trim() ?? "";
            w.Secret              = temp.Auth.Secret?.Trim() ?? "";
            w.AuthorizationHeader = temp.Auth.AuthorizationHeader?.Trim() ?? "";

            DestroyImmediate(temp);
        }

        w.ShowUtility();
        w.Focus();          // <- keep this
    }

    private bool IsValid() =>
        !string.IsNullOrWhiteSpace(ProjectId) &&
        !string.IsNullOrWhiteSpace(KeyId) &&
        !string.IsNullOrWhiteSpace(Secret) &&
        !string.IsNullOrWhiteSpace(AuthorizationHeader);
    
    private void SaveCredentials()
    {
        EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_ProjectId), ProjectId.Trim());
        EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_KeyId), KeyId.Trim());
        EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_Secret), Secret.Trim());
        EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_AuthorizationHeader), AuthorizationHeader.Trim());
        Debug.Log("[CCD Credentials] ✅ Credentials saved successfully.");
    }
    
    private void LoadCredentials()
    {
        ProjectId = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_ProjectId), "");
        KeyId = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_KeyId), "");
        Secret = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_Secret), "");
        AuthorizationHeader = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_AuthorizationHeader), "");
    }
    
    private void OK()
    {
        if (!IsValid())
        {
            EditorUtility.DisplayDialog("Missing Data", "Please fill all credential fields.", "OK");
            return;
        }

        // Save credentials to EditorPrefs
        SaveCredentials();

        // Create credentials object
        var creds = new Creds
        {
            ProjectId = ProjectId.Trim(),
            KeyId = KeyId.Trim(),
            Secret = Secret.Trim(),
            AuthorizationHeader = AuthorizationHeader.Trim()
        };

        // Close credentials popup
        Close();

        // Open Environment/Bucket picker with credentials
        CcdEnvBucketPickerWindow.OpenWithCredentials(creds);
    }
    
    private void CloseAll()
    {
        // Just close the popup — main window hasn't been opened yet
        Close();
    }

    private void OnEnable()
    {
        // Load saved credentials when window opens
        LoadCredentials();
        
        // Load logo texture
        _logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
        if (_logoTexture == null)
        {
            Debug.LogWarning($"[CCD Credentials] Logo not found at: {LogoPath}");
        }
    }

    // ---- Footer UI ----
    protected override void OnImGUI()
    {
        // Draw logo at the top if available
        if (_logoTexture != null)
        {
            GUILayout.Space(10);
            
            // Center the logo horizontally
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                
                // Scale logo to fit window width (max 300px wide or window width - 20px)
                var maxWidth = Mathf.Min(300f, position.width - 20f);
                var logoWidth = Mathf.Min(_logoTexture.width, maxWidth);
                var logoHeight = (_logoTexture.height * logoWidth) / _logoTexture.width;
                
                var logoRect = GUILayoutUtility.GetRect(logoWidth, logoHeight, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(logoRect, _logoTexture, ScaleMode.ScaleToFit);
                
                GUILayout.FlexibleSpace();
            }
            
            GUILayout.Space(10);
        }
        
        // Draw Odin inspector content
        base.OnImGUI();

        GUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            // Green OK button (disabled if invalid)
            using (new EditorGUI.DisabledScope(!IsValid()))
            {
                var oldColor = GUI.color;
                GUI.color = new Color(0.7f, 1f, 0.7f); // green
                if (GUILayout.Button("OK", GUILayout.Height(28)))
                    OK();
                GUI.color = oldColor;
            }

            // Red Close button
            var oldColor2 = GUI.color;
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Close", GUILayout.Height(28)))
                CloseAll();
            GUI.color = oldColor2;
        }
    }
    
    public static bool HasValidCredentials()
    {
        var keyId = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_KeyId), "");
        var secret = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_Secret), "");
        var projectId = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_ProjectId), "");
        var authHeader = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(PrefKey_AuthorizationHeader), "");
        return !string.IsNullOrEmpty(keyId) &&
               !string.IsNullOrEmpty(secret) &&
               !string.IsNullOrEmpty(projectId) &&
               !string.IsNullOrEmpty(authHeader);
    }
}
#endif