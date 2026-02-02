#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class CcdEnvBucketPickerWindow : OdinEditorWindow
{
    // ---------- Open ----------
    public static void Open(CcdBuildAndPublish owner)
    {
        var w = CreateInstance<CcdEnvBucketPickerWindow>();
        w.titleContent = new GUIContent("Select Environment & Bucket");
        w.minSize = new Vector2(560, 300);
        w._owner = owner;

        // Snapshot current project/env mode
        w._isProd   = owner.Environment == CcdBuildAndPublish.Env.Prod;
        w._projectId = owner.Auth.ProjectId;                            // <- single project id

        w._keyId  = owner.Auth.KeyId?.Trim() ?? "";
        w._secret = owner.Auth.Secret?.Trim() ?? "";
        w._accHeader = owner.Auth.AuthorizationHeader?.Trim() ?? "";
        w.ShowUtility();   // utility popup window
        w.LoadEnvs(); // kick async load
    }
    
    public static void OpenWithCallback(CcdBuildAndPublish owner, Action onCloseCallback)
    {
        var w = CreateInstance<CcdEnvBucketPickerWindow>();
        w.titleContent = new GUIContent("Select Environment & Bucket");
        w.minSize = new Vector2(560, 300);
        w._owner = owner;

        w._isProd = owner.Environment == CcdBuildAndPublish.Env.Prod;
        w._projectId = owner.Auth.ProjectId;
        w._keyId = owner.Auth.KeyId?.Trim() ?? "";
        w._secret = owner.Auth.Secret?.Trim() ?? "";
        w._accHeader = owner.Auth.AuthorizationHeader?.Trim() ?? "";

        w._onCloseCallback = onCloseCallback; // ✅ store the callback
        w.ShowUtility();
        w.LoadEnvs(); // load environments
    }
    
    // New method: Open picker with credentials (no owner window yet)
    public static void OpenWithCredentials(CcdCredentialsPopup.Creds creds)
    {
        var w = CreateInstance<CcdEnvBucketPickerWindow>();
        w.titleContent = new GUIContent("Select Environment & Bucket");
        w.minSize = new Vector2(560, 300);
        w._owner = null; // No owner yet
        w._credentials = creds; // Store credentials
        
        // Default to Dev mode when opening from credentials
        w._isProd = false;
        w._projectId = creds.ProjectId;
        w._keyId = creds.KeyId;
        w._secret = creds.Secret;
        w._accHeader = creds.AuthorizationHeader;
        
        w.ShowUtility();
        w.LoadEnvs(); // Load environments
    }

    // ---------- Backref ----------
    private CcdBuildAndPublish _owner;
    private Action _onCloseCallback;
    
    // ---------- Credentials storage (when opening without owner) ----------
    private CcdCredentialsPopup.Creds _credentials;
    
    // ---------- Inputs snapshot ----------
    private bool _isProd;

    [ShowInInspector, ReadOnly, LabelText("Project ID")]
    private string _projectId;

    [ShowInInspector, ReadOnly, LabelText("Service Account Key ID")]
    private string _keyId;
    private string _secret;
    private string _accHeader;

    // ---------- Internal state ----------
    [ShowInInspector, ReadOnly] private string _status;
    [ShowInInspector, ReadOnly] private string _error;
    
    private bool _isLoadingEnvs = false;
    private bool _isLoadingBuckets = false;

    [ShowInInspector, LabelText("Environment"), ValueDropdown(nameof(GetEnvDropdown))]
    [OnValueChanged(nameof(OnEnvChanged))]
    [EnableIf("@!_isLoadingEnvs")]
    public string SelectedEnvId; // GUID chosen in this popup (not yet committed)
    
    [ShowInInspector, ReadOnly, LabelText("Selected Environment")]
    [HideIf("@string.IsNullOrWhiteSpace(SelectedEnvId) || _isLoadingEnvs")]
    [PropertySpace(5)]
    public string SelectedEnvDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedEnvId) || _envDisplayTexts == null)
                return SelectedEnvId ?? "";
            return _envDisplayTexts.TryGetValue(SelectedEnvId, out var display) ? display : SelectedEnvId;
        }
    }

    [ShowInInspector, LabelText("Bucket"), ValueDropdown(nameof(GetBucketDropdown))]
    [OnValueChanged(nameof(OnBucketChanged))]
    [EnableIf("@!_isLoadingBuckets")]
    public string SelectedBucketId; // GUID chosen in this popup (not yet committed)
    
    [ShowInInspector, ReadOnly, LabelText("Selected Bucket")]
    [HideIf("@string.IsNullOrWhiteSpace(SelectedBucketId) || _isLoadingBuckets")]
    [PropertySpace(5)]
    public string SelectedBucketDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedBucketId) || _bucketDisplayTexts == null)
                return SelectedBucketId ?? "";
            return _bucketDisplayTexts.TryGetValue(SelectedBucketId, out var display) ? display : SelectedBucketId;
        }
    }
    
    [ShowInInspector, ReadOnly, LabelText("")]
    [ShowIf("@_isLoadingEnvs")]
    [PropertySpace(5)]
    public string LoadingEnvsStatus => $"🔄 Loading environments... {_status}";
    
    [ShowInInspector, ReadOnly, LabelText("")]
    [ShowIf("@_isLoadingBuckets")]
    [PropertySpace(5)]
    public string LoadingBucketsStatus => $"🔄 Loading buckets... {_status}";
    
    private string _selectedEnvName;
    
    private readonly List<ValueDropdownItem<string>> _envOptions    = new();
    private readonly List<ValueDropdownItem<string>> _bucketOptions = new();
    
    // Lookup dictionaries to get display text from ID
    private readonly Dictionary<string, string> _envDisplayTexts = new();
    private readonly Dictionary<string, string> _bucketDisplayTexts = new();

    private string _projectOnlyToken; // token without env
    private string _envScopedToken;   // token for selected env

    const string CCD_MGMT_BASE = "https://services.api.unity.com/ccd/management/v1";
    
    // ---------- EditorPrefs keys for saved selections ----------
    private const string PrefKey_SavedEnvId_Dev = "CCD_SavedEnvId_Dev";
    private const string PrefKey_SavedBucketId_Dev = "CCD_SavedBucketId_Dev";
    private const string PrefKey_SavedEnvId_Prod = "CCD_SavedEnvId_Prod";
    private const string PrefKey_SavedBucketId_Prod = "CCD_SavedBucketId_Prod";
    
    // ---------- UI data providers ----------
    private IEnumerable<ValueDropdownItem<string>> GetEnvDropdown()    => _envOptions;
    private IEnumerable<ValueDropdownItem<string>> GetBucketDropdown() => _bucketOptions;

    // ---------- Buttons ----------
    [HorizontalGroup("buttons"), GUIColor(0.7f, 1f, 0.7f), Button(ButtonSizes.Large)]
    [EnableIf("@!string.IsNullOrWhiteSpace(SelectedEnvId) && !string.IsNullOrWhiteSpace(SelectedBucketId) && !_isLoadingEnvs && !_isLoadingBuckets")]
    public void SaveAndClose()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedEnvId))    throw new Exception("Pick an Environment first.");
            if (string.IsNullOrWhiteSpace(SelectedBucketId)) throw new Exception("Pick a Bucket first.");

            // Save selections one more time before closing (in case they changed)
            var envPrefKey = _isProd ? PrefKey_SavedEnvId_Prod : PrefKey_SavedEnvId_Dev;
            var bucketPrefKey = _isProd ? PrefKey_SavedBucketId_Prod : PrefKey_SavedBucketId_Dev;
            EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(envPrefKey), SelectedEnvId);
            EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(bucketPrefKey), SelectedBucketId);

            // If we have credentials but no owner, create and open the main window
            if (_owner == null && _credentials != null)
            {
                // Create the main window
                var mainWindow = EditorWindow.GetWindow<CcdBuildAndPublish>();
                mainWindow.titleContent = new GUIContent("Build & Publish CCD");
                
                // Inject credentials
                mainWindow.Auth.ProjectId = _credentials.ProjectId;
                mainWindow.Auth.KeyId = _credentials.KeyId;
                mainWindow.Auth.Secret = _credentials.Secret;
                mainWindow.Auth.AuthorizationHeader = _credentials.AuthorizationHeader;
                
                // Set environment mode
                mainWindow.Environment = _isProd ? CcdBuildAndPublish.Env.Prod : CcdBuildAndPublish.Env.Dev;
                
                // Load existing prefs first (to get other settings)
                mainWindow.LoadPrefs();
                
                // Write selected env/bucket to main window (after loading prefs so they don't get overwritten)
                mainWindow.EnvironmentId = SelectedEnvId;
                mainWindow.EnvironmentName = _selectedEnvName;
                mainWindow.BucketId = SelectedBucketId;
                // Extract bucket name from the dropdown display text
                var selectedBucket = _bucketOptions.FirstOrDefault(b => b.Value == SelectedBucketId);
                mainWindow.BucketName = selectedBucket.Text?.Split('•').FirstOrDefault()?.Trim() ?? "";
                
                // Save the updated prefs with new env/bucket values
                var savePrefsMethod = mainWindow.GetType().GetMethod("SavePrefs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                savePrefsMethod?.Invoke(mainWindow, null);
                
                // Initialize main window
                mainWindow.Addr.OnProfileActivated = mainWindow.UpdateCustomRemoteVarsForProfile;
                mainWindow.Addr.RestoreProfileSelection();
                
                // Show main window
                mainWindow.Show();
                mainWindow.Focus();
                mainWindow.Repaint();
                
                Debug.Log($"[CCD Pipeline] ✅ Build & Publish window opened. Env={SelectedEnvId}, Bucket={SelectedBucketId}");
            }
            else if (_owner != null)
            {
                // Write back into the owner's read-only fields (existing behavior)
                _owner.EnvironmentId = SelectedEnvId;
                _owner.EnvironmentName = _selectedEnvName;
                _owner.BucketId = SelectedBucketId;
                // Extract bucket name from the dropdown display text
                var selectedBucket = _bucketOptions.FirstOrDefault(b => b.Value == SelectedBucketId);
                _owner.BucketName = selectedBucket.Text?.Split('•').FirstOrDefault()?.Trim() ?? "";

                // Persist & repaint owner
                _owner.Repaint();
                var savePrefs = _owner.GetType().GetMethod("SavePrefs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                savePrefs?.Invoke(_owner, null);
                
                // Trigger callback if exists
                _onCloseCallback?.Invoke();
            }
            else
            {
                throw new Exception("Cannot save: missing both owner window and credentials.");
            }
            
            Close();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            Debug.LogError(ex);
        }
    }

    private void OnBucketChanged()
    {
        // Save selected bucket
        if (!string.IsNullOrWhiteSpace(SelectedBucketId))
        {
            var prefKey = _isProd ? PrefKey_SavedBucketId_Prod : PrefKey_SavedBucketId_Dev;
            EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(prefKey), SelectedBucketId);
            Debug.Log($"[CCD Picker] Saved bucket: {SelectedBucketId} (mode: {(_isProd ? "Prod" : "Dev")})");
        }
    }
    
    // ---------- Async loaders ----------
    private async Task LoadEnvs()
    {
        try
        {
            _isLoadingEnvs = true;
            _status = "Exchanging project-only token…"; _error = null;
            EditorApplication.delayCall += Repaint;

            _envOptions.Clear();
            _bucketOptions.Clear();
            _envDisplayTexts.Clear();
            _bucketDisplayTexts.Clear();
            SelectedEnvId = null;
            SelectedBucketId = null;

            var projectId = CcdBuildAndPublish.EnsureGuidStatic(_projectId, nameof(CcdBuildAndPublish.Auth.ProjectId));
            
            _status = "Listing environments…";
            EditorApplication.delayCall += Repaint;
            var envs = await ListEnvironmentsWithBearerAsync(_accHeader, projectId);

            _envDisplayTexts.Clear();
            foreach (var e in envs)
            {
                var displayText = $"{e.name} • id={e.id}";
                _envDisplayTexts[e.id] = displayText;
                _envOptions.Add(new ValueDropdownItem<string>(displayText, e.id));
            }

            // Auto-select saved environment after loading
            var savedEnvPrefKey = _isProd ? PrefKey_SavedEnvId_Prod : PrefKey_SavedEnvId_Dev;
            var savedEnvId = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(savedEnvPrefKey), "");
            
            if (!string.IsNullOrWhiteSpace(savedEnvId) && _envOptions.Any(e => e.Value == savedEnvId))
            {
                SelectedEnvId = savedEnvId;
                // Trigger OnEnvChanged to load buckets and set env name
                OnEnvChanged();
                _status = $"Loaded {envs.Count} environment(s). Auto-selected saved environment."; 
            }
            else
            {
                _status = $"Loaded {envs.Count} environment(s). Pick one."; 
            }
            
            _isLoadingEnvs = false;
            EditorApplication.delayCall += Repaint;
        }
        catch (Exception ex)
        {
            _isLoadingEnvs = false;
            _error = ex.Message; _status = "Failed.";
            EditorApplication.delayCall += Repaint;
            Debug.LogError(ex);
        }
    }

    private void OnEnvChanged()
    {
        // store the visible name (e.g. "dev-editor")
        var selected = _envOptions.FirstOrDefault(e => e.Value == SelectedEnvId);
        _selectedEnvName = selected.Text?.Split('•').FirstOrDefault()?.Trim(); // extract before the •

        // Save selected environment
        if (!string.IsNullOrWhiteSpace(SelectedEnvId))
        {
            var prefKey = _isProd ? PrefKey_SavedEnvId_Prod : PrefKey_SavedEnvId_Dev;
            EditorPrefs.SetString(CcdBuildAndPublish.GetProjectScopedKey(prefKey), SelectedEnvId);
            Debug.Log($"[CCD Picker] Saved environment: {SelectedEnvId} (mode: {(_isProd ? "Prod" : "Dev")})");
        }

        // Delay the async bucket loading until after the current GUI frame
        EditorApplication.delayCall += () => _ = LoadBucketsForCurrentEnv();
    }

    private async Task LoadBucketsForCurrentEnv()
    {
        try
        {
            _isLoadingBuckets = true;
            _bucketOptions.Clear();
            _bucketDisplayTexts.Clear();
            SelectedBucketId = null;
            _error = null;

            if (string.IsNullOrWhiteSpace(SelectedEnvId))
            {
                _isLoadingBuckets = false;
                _status = "Pick an environment first.";
                EditorApplication.delayCall += Repaint;
                return;
            }

            var projectId = CcdBuildAndPublish.EnsureGuidStatic(_projectId, nameof(CcdBuildAndPublish.Auth.ProjectId));

            _status = "Exchanging env-scoped token…";
            EditorApplication.delayCall += Repaint;
            //_envScopedToken = await GetAccessTokenEnvScoped(_keyId, _secret, projectId, SelectedEnvId);

            _status = "Listing buckets for the selected environment…";
            EditorApplication.delayCall += Repaint;
            var buckets = await ListBucketsForEnvironmentManagementAsync(_accHeader, projectId, SelectedEnvId);

            _bucketDisplayTexts.Clear();
            foreach (var b in buckets)
            {
                var displayText = $"{b.name} • id={b.id}";
                _bucketDisplayTexts[b.id] = displayText;
                _bucketOptions.Add(new ValueDropdownItem<string>(displayText, b.id));
            }

            // Auto-select saved bucket after loading
            var savedBucketPrefKey = _isProd ? PrefKey_SavedBucketId_Prod : PrefKey_SavedBucketId_Dev;
            var savedBucketId = EditorPrefs.GetString(CcdBuildAndPublish.GetProjectScopedKey(savedBucketPrefKey), "");
            
            if (!string.IsNullOrWhiteSpace(savedBucketId) && _bucketOptions.Any(b => b.Value == savedBucketId))
            {
                SelectedBucketId = savedBucketId;
                OnBucketChanged(); // Save again (in case it changed)
                _status = $"Loaded {buckets.Count} bucket(s). Auto-selected saved bucket."; 
            }
            else
            {
                _status = $"Loaded {buckets.Count} bucket(s). Pick one, then Save."; 
            }
            
            _isLoadingBuckets = false;
            EditorApplication.delayCall += Repaint;
        }
        catch (Exception ex)
        {
            _isLoadingBuckets = false;
            _error = ex.Message; _status = "Failed.";
            EditorApplication.delayCall += Repaint;
            Debug.LogError(ex);
        }
    }

    private static async Task<List<CcdEnvInfo>> ListEnvironmentsWithBearerAsync(string bearerToken, string projectId, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", bearerToken);
        if (!http.DefaultRequestHeaders.Contains("Unity-Project-Id"))
            http.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);

        var url = $"{CCD_MGMT_BASE}/projects/{projectId}/environments?per_page=100";
        using var resp = await http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"[CCD] ListEnvironments (Bearer) failed: {resp.StatusCode} {body}");

        return JsonConvert.DeserializeObject<List<CcdEnvInfo>>(body) ?? new List<CcdEnvInfo>();
    }

    private static async Task<List<CcdBucketInfo>> ListBucketsForEnvironmentManagementAsync(
        string bearerToken,
        string projectId,
        string environmentId,
        CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", bearerToken);
        if (!http.DefaultRequestHeaders.Contains("Unity-Project-Id"))
            http.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);
        
        var url = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets?per_page=100";
        using var resp = await http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"[CCD] List buckets (mgmt env) failed: {resp.StatusCode} {body}");

        return JsonConvert.DeserializeObject<List<CcdBucketInfo>>(body) ?? new List<CcdBucketInfo>();
    }

    // ---------- Layout niceties ----------
    protected override void OnImGUI()
    {
        base.OnImGUI();
        
        // Show loading indicators with refresh icon
        if (_isLoadingEnvs || _isLoadingBuckets)
        {
            var refreshIcon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Refresh" : "Refresh");
            if (refreshIcon != null)
            {
                var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.ExpandWidth(false));
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.DrawTexture(rect, refreshIcon.image, ScaleMode.ScaleToFit);
                }
            }
        }
        
        // Only show status/error messages during Repaint to avoid layout issues
        if (Event.current.type == EventType.Repaint)
        {
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);
        }
        else if (Event.current.type == EventType.Layout)
        {
            // Reserve space for status/error messages during layout
            if (!string.IsNullOrEmpty(_status))
                GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 2);
            if (!string.IsNullOrEmpty(_error))
                GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 2);
        }
    }
}
#endif