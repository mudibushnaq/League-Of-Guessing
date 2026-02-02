#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Profile;

public class CcdBuildAndPublish : OdinEditorWindow
{
    private const string PrefKey = "CCD_SA_TOKEN_WINDOW_V5";

    // ---------- MENU ----------
    /*private static void Open()
    {
        CcdCredentialsPopup.OpenForLaunch(creds =>
        {
            // Step 1️⃣ – Create main window but keep it hidden for now
            var w = CreateInstance<CcdBuildAndPublish>();
            w.titleContent = new GUIContent("Build & Publish");

            // Inject credentials
            w.Auth.ProjectId = creds.ProjectId.Trim();
            w.Auth.KeyId = creds.KeyId.Trim();
            w.Auth.Secret = creds.Secret.Trim();
            w.Auth.AuthorizationHeader = creds.AuthorizationHeader.Trim();

            w.Addr.OnProfileActivated = w.UpdateCustomRemoteVarsForProfile;
            w.Addr.RestoreProfileSelection();
            w.LoadPrefs();

            // Step 2️⃣ – Immediately open the Environment/Bucket picker window
            CcdEnvBucketPickerWindow.OpenWithCallback(w, () =>
            {
                // Step 3️⃣ – Once picker saves, open the main window
                w.Show();
                w.Focus();
                w.Repaint();
            });
        });
    }*/
    
    public static string EnsureGuidStatic(string value, string fieldName) => EnsureGuid(value, fieldName);
    // ---------- ENUMS ----------
    public enum Env { Dev, Prod }
    public enum Artifact { APK, AAB, EXE }

    // ===== TABS =====
    [TabGroup("Tabs", "Build & Publish", Order = -300)]
    [ShowInInspector, HideLabel, DisplayAsString(false)] private string _tabBuildAnchor = "";
    [TabGroup("Tabs", "Authentication & CCD Credentials", Order = -300)]
    [ShowInInspector, HideLabel, DisplayAsString(false)] private string _tabAuthAnchor = "";
    [TabGroup("Tabs", "Addressables Settings", Order = -300)]
    [ShowInInspector, HideLabel, DisplayAsString(false)] private string _tabAddressAnchor = "";
    [TabGroup("Tabs", "Options", Order = -300)]
    [ShowInInspector, HideLabel, DisplayAsString(false)] private string _tabOptionsAnchor = "";

    // Put each section on its tab (NOTE: the TabGroup is on the FIELD)
    [TabGroup("Tabs", "Authentication & CCD Credentials")]
    [HideLabel] public AuthSection Auth = new ();

    [TabGroup("Tabs", "Addressables Settings")]
    [HideLabel] public AddressablesSection Addr = new ();

    [TabGroup("Tabs", "Options")]
    [HideLabel] public OptionsSection Opt = new ();
    
    // ---------- TOP ----------
    [TabGroup("Tabs", "Build & Publish")]
    [Title("Environment & Target")]
    [PropertyOrder(-100)]
    [EnumToggleButtons, HideLabel, PropertySpace]
    public Env Environment = Env.Dev;

    [ShowInInspector, ReadOnly, LabelText("Unity Player Version"), PropertyOrder(-99)]
    public static string CurrentBundleVersion => GetBundleVersionFromActiveBuildProfile();

    [InfoBox("Each build publishes Addressables to a versioned badge (e.g., prod-1.3.0) so older apps keep using their own catalog.", InfoMessageType.None)]
    [ShowInInspector, ReadOnly, LabelText("Computed Badge")]
    public string ComputedBadge { get; private set; }
    private string _lastBundleVersion;
    private string _lastUnityVersion;
    
    // ========================== ADDRESSABLES ==========================
    [HideInInspector] public string EnvironmentId = "";
    [HideInInspector] public string EnvironmentName = "";
    [HideInInspector] public string BucketId  = "";
    [HideInInspector] public string BucketName = "";
    
    // ==================================
    // NEW: runtime-populated dropdown data
    // ==================================
    [NonSerialized] private List<ValueDropdownItem<string>> _envOptions = new();
    [NonSerialized] private List<ValueDropdownItem<string>> _bucketOptions = new();

    // =========================== INTERNALS ===========================
    private string _jwtPrettyPayload = "";
    private string _jwtSub = "";
    private string _jwtApiKeyPublicIdentifier = "";
    private DateTimeOffset? _exp;
    private DateTimeOffset? _iat;
    private string _lastOk;
    private string _lastErr;
    
    // remember where we built addressables last time
    private string _lastAddressablesBuildDir;
    
    const string CCD_MGMT_BASE = "https://services.api.unity.com/ccd/management/v1";
    
    // NEW: the actual picked values (we'll save here)
    [TabGroup("Tabs", "Build & Publish")]
    [ShowInInspector, ReadOnly, LabelText("Selected Environment ID")]
    public string SelectedEnvironmentId
    {
        get => EnvironmentId;
        private set => EnvironmentId = value;
    }

    [TabGroup("Tabs", "Build & Publish")]
    [ShowInInspector, ReadOnly, LabelText("Selected Environment Name")]
    public string SelectedEnvironmentName
    {
        get => EnvironmentName;
        private set => EnvironmentName = value;
    }

    [TabGroup("Tabs", "Build & Publish")]
    [ShowInInspector, ReadOnly, LabelText("Selected Bucket ID")]
    public string SelectedBucketId
    {
        get => BucketId;
        private set => BucketId = value;
    }

    [TabGroup("Tabs", "Build & Publish")]
    [ShowInInspector, ReadOnly, LabelText("Selected Bucket Name")]
    public string SelectedBucketName
    {
        get => BucketName;
        private set => BucketName = value;
    }

    // NEW: the dropdowns user will interact with
    [TabGroup("Tabs", "Build & Publish")]
    [ValueDropdown(nameof(GetEnvDropdown)), LabelText("Pick Environment (from project)")]
    [PropertyOrder(-95)]
    [ShowIf("@_envOptions != null && _envOptions.Count > 0")]
    [OnValueChanged(nameof(OnEnvironmentPicked))]
    public string EnvDropdownSelection;

    [TabGroup("Tabs", "Build & Publish")]
    [ValueDropdown(nameof(GetBucketDropdown)), LabelText("Pick Bucket (from project)")]
    [PropertyOrder(-94)]
    [ShowIf("@_bucketOptions != null && _bucketOptions.Count > 0")]
    [OnValueChanged(nameof(OnBucketPicked))]
    public string BucketDropdownSelection;

    // Providers for Odin
    private IEnumerable<ValueDropdownItem<string>> GetEnvDropdown()    => _envOptions ?? Enumerable.Empty<ValueDropdownItem<string>>();
    private IEnumerable<ValueDropdownItem<string>> GetBucketDropdown() => _bucketOptions ?? Enumerable.Empty<ValueDropdownItem<string>>();
    
    // ===== Progress pump for main-thread-safe Editor progress bar =====
    bool   _uplPumpActive;
    string _uplTitle, _uplInfo;           // text lines
    float  _uplOverall01;                 // 0..1
    private string _lastActiveProfileGuid;
    
    // ---------- BUTTONS ----------
    //[Button(ButtonSizes.Large), GUIColor(0.85f, 0.85f, 1f)]
    void Validate()
    {
        try
        {
            // --- Addressables profile prep ---
            var s = AddressableAssetSettingsDefaultObject.Settings 
                    ?? throw new Exception("Addressables Settings missing.");

            // Use the profile picked in AddressablesSection; fallback to active profile.
            var pickedProfileId = Addr.SelectedProfileId;
            var activeProfileId = s.activeProfileId;
            var useProfileId    = string.IsNullOrEmpty(pickedProfileId) ? activeProfileId : pickedProfileId;

            if (string.IsNullOrEmpty(useProfileId))
                throw new Exception("No Addressables profile is active/selected.");

            // Helper to fetch raw/evaluated values from the selected profile
            string GetRaw(string varName) =>
                s.profileSettings.GetValueByName(useProfileId, varName) ?? string.Empty;

            string Eval(string raw) => EvaluateString(s, raw); // your reflection-safe helper

            // Read everything from the selected profile (read-only)
            var envNameRaw      = GetRaw("EnvironmentName");
            var bucketIdRaw    = GetRaw("BucketId");
            var badgeRaw       = GetRaw("ContentBadge");
            var remoteBuildRaw = GetRaw("RemoteBuildPath");
            var remoteLoadRaw  = GetRaw("RemoteLoadPath");

            // Evaluated (tokens expanded)
            var evalBuild = Eval(remoteBuildRaw);
            var evalLoad  = Eval(remoteLoadRaw);

            // Keep your safety toggles (these do not touch profile variables)
            if (Opt.Force_UniqueBundleIds) s.UniqueBundleIds = true;
            if (Opt.Force_RemoteCatalog)   s.BuildRemoteCatalog = true;
            if (Opt.Optimize_CatalogSize)  s.OptimizeCatalogSize = true;

            // Prefer profile’s ContentBadge if present; otherwise keep your computed one
            var badge = string.IsNullOrWhiteSpace(badgeRaw) ? ComputedBadge : badgeRaw;

            // If user didn’t pick env/bucket via your CCD picker, fall back to profile
            var envId   = SelectedEnvironmentId ?? string.Empty;
            var bucket  = string.IsNullOrWhiteSpace(SelectedBucketId) ? bucketIdRaw : SelectedBucketId;

            var profileName = s.profileSettings.GetProfileName(useProfileId) ?? "(unknown)";

            var validationSummary =
                $"Profile: {profileName}\n" +
                $"EnvName={envNameRaw}\n" +
                $"EnvId={(string.IsNullOrEmpty(envId) ? "(not selected)" : envId)}\n" +
                $"Bucket={(string.IsNullOrEmpty(bucket) ? "(not selected)" : bucket)}\n" +
                $"Badge={badge}\n" +
                $"RemoteBuild (raw)={remoteBuildRaw}\n" +
                $"RemoteLoad  (raw)={remoteLoadRaw}\n" +
                $"RemoteBuild (eval)={evalBuild}\n" +
                $"RemoteLoad  (eval)={evalLoad}\n" +
                $"UniqueBundleIds={s.UniqueBundleIds}, RemoteCatalog={s.BuildRemoteCatalog}, OptimizeCatalogSize={s.OptimizeCatalogSize}";

            Debug.Log("[Validate]\n" + validationSummary);

            //SavePrefs();

            // Open the picker (user will choose Env → Buckets → Save) if you still use it
            CcdEnvBucketPickerWindow.Open(this);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Validate] " + ex.Message);
            Repaint();
        }
    }
    
    async Task UploadFileToCcdWithProgressAsync(
    HttpClient httpBearer,   // MUST have Bearer <accessToken>
    string projectId,
    string environmentId,
    string bucketId,
    string baseDir,
    string fullPath,
    Action<long,long> onProgress,
    CancellationToken ct)
    {
        // 0) Build the logical CCD path (relative to your local root)
        var rel = BuildCcdRelativePath(baseDir, fullPath);

        // 1) Prepare ticket request
        var fi = new FileInfo(fullPath);
        var md5Hex = ComputeMd5Hex(fullPath); // helper below

        
        var ticketUrl =
            $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets/{bucketId}/entry_by_path" +
            $"?path={Uri.EscapeDataString(rel)}&updateIfExists=true";

        var ticketBodyObj = new
        {
            content_size = fi.Length,
            content_type = "application/octet-stream",
            content_hash = md5Hex,
            signed_url   = true
        };

        using var ticketReq = new HttpRequestMessage(HttpMethod.Post, ticketUrl);
        ticketReq.Content = new StringContent(JsonConvert.SerializeObject(ticketBodyObj), Encoding.UTF8, "application/json");
        if (!httpBearer.DefaultRequestHeaders.Contains("Unity-Project-Id"))
            httpBearer.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);

        using var ticketResp = await httpBearer.SendAsync(ticketReq, HttpCompletionOption.ResponseHeadersRead, ct);
        var ticketText = await ticketResp.Content.ReadAsStringAsync();
        if (!ticketResp.IsSuccessStatusCode)
            throw new Exception($"Ticket request failed for '{rel}': {ticketResp.StatusCode} {ticketText}");

        dynamic ticket = JsonConvert.DeserializeObject(ticketText);

        // 2) Extract upload destination from ticket
        string uploadUrl = null;
        var uploadHeaders = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

        // A) exact key used by CCD for presigned uploads (snake_case)
        if (ticket?.signed_url != null)
        {
            uploadUrl = (string)ticket.signed_url;
            // CCD sometimes also returns 'headers' but with GCS you usually only need Content-Type
            if (ticket.headers != null)
                foreach (var kv in ticket.headers)
                    uploadHeaders[(string)kv.Name] = (string)kv.Value;
        }
        // B) other shapes we already handled
        else if (ticket?.uploadUrl != null)
        {
            uploadUrl = (string)ticket.uploadUrl;
            if (ticket.headers != null)
                foreach (var kv in ticket.headers)
                    uploadHeaders[(string)kv.Name] = (string)kv.Value;
        }
        else if (ticket?.instructions != null && ticket.instructions.HasValues && ticket.instructions[0] != null)
        {
            uploadUrl = (string)ticket.instructions[0].url;
            if (ticket.instructions[0].headers != null)
                foreach (var kv in ticket.instructions[0].headers)
                    uploadHeaders[(string)kv.Name] = (string)kv.Value;
        }

        if (string.IsNullOrEmpty(uploadUrl))
            throw new Exception($"Ticket for '{rel}' did not include an upload URL. Body: {ticketText}");

        // 3) Upload BYTES to the presigned URL
        //    Do NOT add your Bearer token here unless ticket explicitly required it.
        using (var httpUpload = new HttpClient())
        {
            // GCS signed headers say: content-type;host → set Content-Type only
            // If the ticket provided an explicit Content-Type header, use it; otherwise octet-stream.
            var contentType = uploadHeaders.GetValueOrDefault("Content-Type", "application/octet-stream");

            await using var fs = File.OpenRead(fullPath);
            using var content = new StreamWithProgressContent(fs, fi.Length, contentType, 64 * 1024, onProgress);

            using var putReq  = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            putReq.Content = content;
            using var putResp = await httpUpload.SendAsync(putReq, HttpCompletionOption.ResponseHeadersRead, ct);
            var putText = await putResp.Content.ReadAsStringAsync();
            if (!putResp.IsSuccessStatusCode)
                throw new Exception($"Provider upload failed for '{rel}': {putResp.StatusCode} {putText}");
        }

        // 4) Finalize if the ticket returned a finalize URL
        string finalizeUrl = null;
        if (ticket.finalizeUrl  != null) finalizeUrl  = (string)ticket.finalizeUrl;
        if (ticket.finaliseUrl != null) finalizeUrl  = (string)ticket.finaliseUrl;

        if (!string.IsNullOrEmpty(finalizeUrl))
        {
            using var finalizeReq  = new HttpRequestMessage(HttpMethod.Post, finalizeUrl);
            finalizeReq.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var finalizeResp = await httpBearer.SendAsync(finalizeReq, HttpCompletionOption.ResponseHeadersRead, ct);
            var ftxt = await finalizeResp.Content.ReadAsStringAsync();
            if (!finalizeResp.IsSuccessStatusCode)
                throw new Exception($"Finalize failed for '{rel}': {finalizeResp.StatusCode} {ftxt}");
        }
    }
            
    string BuildCcdRelativePath(string baseDir, string fullPath)
    {
        // Local relative file path (e.g., "catalog_xxx.hash" / "bundles/ab/cd.bundle")
        var localRel = fullPath.Substring(baseDir.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace("\\", "/");

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var ps  = settings.profileSettings;
        var pid = settings.activeProfileId;

        // [BuildTarget] → "StandaloneWindows64", "Android", "iOS", …
        var buildTargetFolder = EditorUserBuildSettings.activeBuildTarget.ToString();

        // [ContentBadge] → from profile if set, else fallback to your computed one
        var badgeFromProfile = ps.GetValueByName(pid, "ContentBadge") ?? "";
        var badge = string.IsNullOrWhiteSpace(badgeFromProfile) ? ComputedBadge : badgeFromProfile;

        // If the localRel already starts with the intended prefix, don't double-prefix
        var prefix = $"{buildTargetFolder}/{badge}";
        return localRel.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ? localRel : $"{prefix}/{localRel}";
    }

    // helper for MD5 hex lowercase
    static string ComputeMd5Hex(string path)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        using var fs  = File.OpenRead(path);
        var hash = md5.ComputeHash(fs);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
    
    // Upload a directory with per-file progress + overall progress bar (BEARER!)
    async Task UploadDirectoryToCcdWithProgressAsync(
    string projectId,
    string environmentId,
    string bucketId,
    string localFolder,
    CancellationToken ct = default)
    {
        if (!Directory.Exists(localFolder)) throw new DirectoryNotFoundException(localFolder);

        var files = Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories)
                             .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                             .ToArray();
        var total = files.Length;
        if (total == 0) { EditorUtility.DisplayDialog("CCD Upload", "No files found to upload.", "OK"); return; }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Auth.AuthorizationHeader); // ✅ Bearer

        // Show initial progress
        _uplTitle = "CCD Upload";
        _uplInfo = $"Starting upload of {total} file(s)...";
        _uplOverall01 = 0f;

        // Start the progress pump to continuously update the progress bar from the main thread
        StartUploadProgressPump(_uplTitle);

        // Force show progress bar and wait for editor to update
        await ShowProgressAndWaitAsync(_uplTitle, _uplInfo, 0f);

        try
        {
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();

                var file = files[i];
                var fileName = Path.GetFileName(file);

                // Update progress before starting file upload
                _uplInfo = $"Uploading {i + 1}/{total} • {fileName}  (0%)";
                _uplOverall01 = (float)i / total;

                // Use a local copy of i for the closure
                int fileIndex = i;

                // Update shared fields from ANY thread – WaitWithProgressUpdatesAsync will paint on main thread
                void OnFileProgress(long sent, long size)
                {
                    var filePct = size > 0 ? (float)sent / size : 1f;
                    var overall = (fileIndex + filePct) / total;
                    _uplInfo = $"Uploading {fileIndex + 1}/{total} • {fileName}  ({Mathf.RoundToInt(filePct * 100)}%)";
                    _uplOverall01 = overall;
                }

                // Start the upload task
                var uploadTask = UploadFileToCcdWithProgressAsync(
                    http,
                    projectId,
                    environmentId,
                    bucketId,
                    localFolder,
                    file,
                    OnFileProgress,
                    ct);

                // Wait for upload while continuously updating the progress bar
                await WaitWithProgressUpdatesAsync(uploadTask);

                // Snap to end-of-file
                _uplInfo = $"Uploaded {i + 1}/{total} • {fileName}  (100%)";
                _uplOverall01 = (i + 1f) / total;

                // Log progress to console
                Debug.Log($"[CCD Upload] {_uplInfo}");

                // Update UI one more time
                await ShowProgressAndWaitAsync(_uplTitle, _uplInfo, _uplOverall01);
            }
        }
        finally
        {
            // Always stop the pump
            StopUploadProgressPump();
        }

        EditorUtility.ClearProgressBar();
        // Dialogs are OK from here (we're back on the calling context)
        EditorUtility.DisplayDialog("CCD Upload", $"Upload completed.\nUploaded: {total} file(s).", "OK");
    }
    
    protected override void OnImGUI()
    {
        // Now draw Odin's normal inspector
        base.OnImGUI();
    }
    
    // Save everything (you already had a SavePrefs; keep it)
    private void SavePrefs()
    {
        var root = new EditorWindowJsonRoot
        {
            Addr          = Addr,
            EnvironmentId = EnvironmentId,
            EnvironmentName = EnvironmentName,
            BucketId   = BucketId,
            BucketName  = BucketName,
            Environment   = Environment,
            DevProfiler   = Opt.Dev_Profiler,
            DevDebugging  = Opt.Dev_Debugging
        };

        var json = JsonUtility.ToJson(root);
        EditorPrefs.SetString(GetProjectScopedKey(PrefKey), json);
    }
    
    public void LoadPrefs()
    {
        if (!EditorPrefs.HasKey(GetProjectScopedKey(PrefKey)))
            return;

        var json = EditorPrefs.GetString(GetProjectScopedKey(PrefKey));
        if (string.IsNullOrEmpty(json))
            return;

        var root = JsonUtility.FromJson<EditorWindowJsonRoot>(json);
        if (root == null)
            return;

        // ---- restore all core fields ----
        EnvironmentId = root.EnvironmentId;
        EnvironmentName = root.EnvironmentName;
        BucketId = root.BucketId;
        BucketName = root.BucketName;
        Environment = root.Environment;

        // ---- Addressables ----
        if (root.Addr != null)
        {
            Addr.SelectedProfileName = root.Addr.SelectedProfileName;
            Addr.SelectedProfileId   = root.Addr.SelectedProfileId;
        }

        // ---- Dev toggles ----
        Opt.Dev_Profiler  = root.DevProfiler;
        Opt.Dev_Debugging = root.DevDebugging;

        Debug.Log($"[CcdBuildAndPublish] Prefs loaded. Profile={Addr.SelectedProfileName}, DevProfiler={Opt.Dev_Profiler}, DevDebugging={Opt.Dev_Debugging}");
    }

    public static string GetProjectScopedKey(string baseKey)
    {
        var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(projectPath));
        var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        return $"{baseKey}_{hash}";
    }
    
    [TabGroup("Tabs", "Build & Publish")]
    [Button(ButtonSizes.Large), GUIColor(0.65f, 0.9f, 1f)]
    [InfoBox("Runs: Build Addressables → Upload to CCD → Create Release → Assign Badge.")]
    public async void Build_Addressables_And_Publish_CCD()
    {
        try
        {
            // 1️⃣ Prep
            ApplyAddressablesProfileVars();
            var settings = AddressableAssetSettingsDefaultObject.Settings
                           ?? throw new Exception("Addressables Settings missing.");
            EnsureContentBadgeMatchesBundleVersion();

            var ps  = settings.profileSettings;
            var pid = settings.activeProfileId;

            var envName = ps.GetValueByName(pid, "EnvironmentName");
            if (string.IsNullOrEmpty(envName))
                throw new Exception("Profile variable 'EnvironmentName' is empty. Please select a valid environment.");

            var envId     = EnsureGuid(GetEnvId(Environment), "SelectedEnvironmentId");
            var bucketId  = EnsureGuid(SelectedBucketId, "SelectedBucketId");
            var projectId = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));

            var profileBadge = ps.GetValueByName(pid, "ContentBadge") ?? "";
            var rawBadge     = string.IsNullOrWhiteSpace(profileBadge) ? ComputedBadge : profileBadge;
            var badge        = SanitizeBadgeName(rawBadge);
            var buildTarget  = EditorUserBuildSettings.activeBuildTarget.ToString();

            // ✅ Check if badge already exists in CCD
            EditorUtility.DisplayProgressBar("CCD Check", $"Checking if badge '{badge}' already exists...", 0.1f);
            var (badgeExists, existingReleaseId) = await CheckIfBadgeExistsAsync(projectId, envId, bucketId, badge);
            EditorUtility.ClearProgressBar();

            if (badgeExists)
            {
                // Badge already exists - give user options
                var choice = EditorUtility.DisplayDialogComplex(
                    "Badge Already Exists",
                    $"Badge '{badge}' already exists in CCD with release ID: {existingReleaseId}\n\n" +
                    "This usually means the content for this version is already uploaded.\n\n" +
                    "What would you like to do?",
                    "Skip Upload (Use Existing)",  // Button 0
                    "Cancel",                       // Button 1
                    "Force Re-upload");             // Button 2

                switch (choice)
                {
                    case 0: // Skip Upload
                        Debug.Log($"[CCD] Badge '{badge}' already exists. Skipping build/upload and using existing release.");
                        EditorUtility.DisplayDialog("Using Existing Content",
                            $"✅ Badge '{badge}' is already available in CCD.\n\nNo build or upload needed. " +
                            $"Your app will use the existing content for this version.", "OK");
                        return;
                    case 1: // Cancel
                        Debug.Log($"[CCD] User cancelled build after seeing badge '{badge}' already exists.");
                        return;
                    case 2: // Force Re-upload
                        Debug.Log($"[CCD] User chose to force re-upload for badge '{badge}' even though it exists.");
                        break;
                }
            }

            if (!EditorUtility.DisplayDialog("Confirm Build + Publish",
                    $"This will:\n\n• Build Addressables\n• Upload to CCD\n• Create a Release\n• Assign badge '{badge}'\n\nEnv: {envId}\nBucket: {bucketId}\n\nProceed?",
                    "Yes, do it", "Cancel")) return;

            // 2️⃣ Update profile variables FIRST
            ps.SetValue(pid, "BucketId", SelectedBucketId);
            ps.SetValue(pid, "EnvironmentName", envName);
            ps.SetValue(pid, "ContentBadge", ComputedBadge);
            
            ps.SetValue(pid, "Remote.LoadPath",
                $"https://[ProjectId].client-api.unity3dusercontent.com/client_api/v1/environments/{envName}/buckets/[BucketId]/release_by_badge/[ContentBadge]/entry_by_path/content/?path=[BuildTarget]/[ContentBadge]");
            ps.SetValue(pid, "Remote.BuildPath", $"CCDBuildData/[BuildTarget]/{envName}/{SelectedBucketName}/[ContentBadge]");
            settings.profileSettings.SetValue(pid, "ProfileVersion", DateTime.Now.ToString("yyyyMMddHHmmss"));

            Debug.Log($"[Pre-Build] Environment={envName}, Bucket={SelectedBucketId}");
            Debug.Log($"[Pre-Build] Profile Remote.BuildPath={ps.GetValueByName(pid, "Remote.BuildPath")}");

            
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ForceProfileUpdate] BucketId={SelectedBucketId}, Env={envName}, Badge={ComputedBadge}");

            // 🧹 Delete previous build folder AFTER variables are applied
            DeletePreviousBadgeBuildIfExists(settings);

            // 3️⃣ Build Addressables
            EditorUtility.DisplayProgressBar("CCD Build & Publish", "Step 1/4 • Building Addressables...", 0.15f);
            using (new RemotePathOverrideScope(settings, SelectedBucketId, SelectedEnvironmentName))
            {
                AddressableAssetSettings.BuildPlayerContent();
            }

            // 4️⃣ Wait until the build folder actually appears (Unity sometimes lags)
            var localDir = GetEvaluatedRemoteBuildPath(settings);
            for (int i = 0; i < 20; i++) // wait up to ~10 seconds
            {
                if (Directory.Exists(localDir))
                    break;

                await UniTask.Delay(500);
            }

            if (!Directory.Exists(localDir))
                throw new DirectoryNotFoundException($"Addressables build folder not found even after build: {localDir}");

            Debug.Log($"[CCD] Addressables built to: {localDir}");

            // 5️⃣ Delete old remote files (if exist)
            await DeleteEntriesForCurrentBadgeAsync(projectId, envId, bucketId, buildTarget, badge);
            
            // 6️⃣ Upload new files
            EditorUtility.DisplayProgressBar("CCD Build & Publish", "Step 2/4 • Uploading to CCD...", 0.45f);
            await UploadDirectoryToCcdWithProgressAsync(projectId, envId, bucketId, localDir);

            // 7️⃣ Create release
            EditorUtility.DisplayProgressBar("CCD Build & Publish", "Step 3/4 • Creating Release...", 0.75f);
            var releaseId = await CreateCcdRelease_MgmtAsync(projectId, envId, bucketId, $"Auto release for {badge}");

            // 8️⃣ Assign badge
            EditorUtility.DisplayProgressBar("CCD Build & Publish", "Step 4/4 • Assigning Badge...", 0.90f);
            await AssignCcdBadge_MgmtAsync(projectId, envId, bucketId, badge, releaseId);

            // ✅ Done
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("CCD Publish Complete",
                $"✅ Addressables built and published!\n\nBadge: {badge}\nRelease ID: {releaseId}", "OK");
            Debug.Log($"✅ CCD Publish complete — Badge={badge}, ReleaseId={releaseId}");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Build_Addressables_And_Publish_CCD] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }

    [TabGroup("Tabs", "Build & Publish")]
    [Button(ButtonSizes.Large), GUIColor(0.7f, 0.9f, 0.7f)]
    [InfoBox("Runs: Build Addressables → Upload to CCD → Create Release → Assign existing badge (no version bump).")]
    public async void Rebuild_Addressables_Update_Current_Badge()
    {
        try
        {
            ApplyAddressablesProfileVars();
            var settings = AddressableAssetSettingsDefaultObject.Settings
                           ?? throw new Exception("Addressables Settings missing.");

            var ps  = settings.profileSettings;
            var pid = settings.activeProfileId;

            var envName = ps.GetValueByName(pid, "EnvironmentName");
            if (string.IsNullOrEmpty(envName))
                throw new Exception("Profile variable 'EnvironmentName' is empty. Please select a valid environment.");

            var envId     = EnsureGuid(GetEnvId(Environment), "SelectedEnvironmentId");
            var bucketId  = EnsureGuid(SelectedBucketId, "SelectedBucketId");
            var projectId = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));

            var profileBadge = ps.GetValueByName(pid, "ContentBadge") ?? "";
            var rawBadge     = string.IsNullOrWhiteSpace(profileBadge) ? ComputedBadge : profileBadge;
            var badge        = SanitizeBadgeName(rawBadge);
            var buildTarget  = EditorUserBuildSettings.activeBuildTarget.ToString();

            if (!EditorUtility.DisplayDialog("Confirm Rebuild + Update",
                    $"This will:\n\n• Rebuild Addressables\n• Upload to CCD\n• Create a Release\n• Assign existing badge '{badge}'\n\nEnv: {envId}\nBucket: {bucketId}\n\nProceed?",
                    "Yes, update", "Cancel")) return;

            // Update profile variables (keep existing badge)
            ps.SetValue(pid, "BucketId", SelectedBucketId);
            ps.SetValue(pid, "EnvironmentName", envName);
            ps.SetValue(pid, "ContentBadge", badge);

            ps.SetValue(pid, "Remote.LoadPath",
                $"https://[ProjectId].client-api.unity3dusercontent.com/client_api/v1/environments/{envName}/buckets/[BucketId]/release_by_badge/[ContentBadge]/entry_by_path/content/?path=[BuildTarget]/[ContentBadge]");
            ps.SetValue(pid, "Remote.BuildPath", $"CCDBuildData/[BuildTarget]/{envName}/{SelectedBucketName}/[ContentBadge]");
            settings.profileSettings.SetValue(pid, "ProfileVersion", DateTime.Now.ToString("yyyyMMddHHmmss"));

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Build Addressables
            EditorUtility.DisplayProgressBar("CCD Rebuild + Update", "Step 1/3 • Building Addressables...", 0.2f);
            using (new RemotePathOverrideScope(settings, SelectedBucketId, SelectedEnvironmentName))
            {
                AddressableAssetSettings.BuildPlayerContent();
            }

            var localDir = GetEvaluatedRemoteBuildPath(settings);
            for (int i = 0; i < 20; i++)
            {
                if (Directory.Exists(localDir))
                    break;
                await UniTask.Delay(500);
            }

            if (!Directory.Exists(localDir))
                throw new DirectoryNotFoundException($"Addressables build folder not found even after build: {localDir}");

            // Upload new files (no badge/version change)
            EditorUtility.DisplayProgressBar("CCD Rebuild + Update", "Step 2/3 • Uploading to CCD...", 0.6f);
            await UploadDirectoryToCcdWithProgressAsync(projectId, envId, bucketId, localDir);

            // Create release + reassign same badge
            EditorUtility.DisplayProgressBar("CCD Rebuild + Update", "Step 3/3 • Creating Release...", 0.85f);
            var releaseId = await CreateCcdRelease_MgmtAsync(projectId, envId, bucketId, $"Auto update for {badge}");
            await AssignCcdBadge_MgmtAsync(projectId, envId, bucketId, badge, releaseId);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("CCD Update Complete",
                $"✅ Addressables rebuilt and updated!\n\nBadge: {badge}\nRelease ID: {releaseId}", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Rebuild_Addressables_Update_Current_Badge] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    // Get the evaluated RemoteBuildPath from the active profile
    string GetEvaluatedRemoteBuildPath(AddressableAssetSettings settings)
    {
        var ps  = settings.profileSettings;
        var pid = settings.activeProfileId;

        var raw = ps.GetValueByName(pid, "Remote.BuildPath");
        if (string.IsNullOrEmpty(raw))
            throw new Exception("Remote.BuildPath is empty in the active profile.");

        var eval = settings.profileSettings.EvaluateString(pid, raw);
        var abs  = Path.IsPathRooted(eval) ? eval : Path.Combine(Directory.GetCurrentDirectory(), eval);
        return abs.Replace("\\", "/");
    }
    
    // === AUTH: token exchange using Service Account keyId:secret ===
    async Task<string> GetCcdAccessTokenAsync(
        string keyId, string secret,
        string projectId, string environmentId = null,
        CancellationToken ct = default)
    {
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{keyId}:{secret}"));
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);

        var qb = new StringBuilder($"https://services.api.unity.com/auth/v1/token-exchange?projectId={projectId}");
        if (!string.IsNullOrWhiteSpace(environmentId))
            qb.Append($"&environmentId={environmentId}");

        using var resp = await http.PostAsync(qb.ToString(),
            content: new StringContent("", Encoding.UTF8, "application/json"), ct);
        var payload = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Token exchange failed: {resp.StatusCode} {payload}");

        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(payload);
        if (dict != null && dict.TryGetValue("accessToken", out var tok) && tok != null)
            return tok.ToString();

        throw new Exception($"Token response did not contain accessToken. Raw: {payload}");
    }

    // === Create a release from current bucket content ===
    async Task<string> CreateCcdRelease_MgmtAsync(
        string projectId,
        string environmentId,
        string bucketId,
        string notes)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Auth.AuthorizationHeader);
        if (!http.DefaultRequestHeaders.Contains("Unity-Project-Id"))
            http.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);

        var url = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets/{bucketId}/releases";

        // Body schema: { entries?, metadata?, notes?, snapshot? } — body required, {} is valid
        var bodyObj = new { notes = string.IsNullOrWhiteSpace(notes) ? null : notes };
        var body    = new StringContent(JsonConvert.SerializeObject(bodyObj), Encoding.UTF8, "application/json");

        using var resp = await http.PostAsync(url, body);
        var txt = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Create release failed: {resp.StatusCode} {txt}");

        // Response may include "releaseid" (snake_case) or "id" depending on org/config.
        dynamic doc = JsonConvert.DeserializeObject(txt);
        string relId = null;
        if (doc.releaseid != null) relId = (string)doc.releaseid;
        else if (doc.id != null)   relId = (string)doc.id;
        else if (doc.link != null) // fallback: parse last segment
        {
            var link  = (string)doc.link;
            var parts = link.TrimEnd('/').Split('/');
            relId = parts.LastOrDefault();
        }

        if (string.IsNullOrEmpty(relId))
            throw new Exception($"Release id not found in response: {txt}");

        return relId;
    }

    // === Assign badge to a release ===
    async Task AssignCcdBadge_MgmtAsync(
        string projectId,
        string environmentId,
        string bucketId,
        string badgeName,
        string releaseId)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Auth.AuthorizationHeader);

        // Some orgs require this header for mgmt endpoints
        if (!http.DefaultRequestHeaders.Contains("Unity-Project-Id"))
            http.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);

        // Management API endpoint (note: no /{name} in the path)
        var url = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets/{bucketId}/badges";

        // Body schema requires snake_case keys and the badge "name" in the JSON
        // name must match regex ^[0-9a-zA-Z-_]+$ and be 1..50 chars
        var bodyObj = new
        {
            name = badgeName,
            releaseid = releaseId
            // or: releasenum = someLong
        };

        var body = new StringContent(JsonConvert.SerializeObject(bodyObj), Encoding.UTF8, "application/json");

        using var resp = await http.PutAsync(url, body);
        var txt = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Assign badge failed: {resp.StatusCode} {txt}");
    }

    // === Check if badge exists and get its release ID ===
    async Task<(bool exists, string releaseId)> CheckIfBadgeExistsAsync(
        string projectId,
        string environmentId,
        string bucketId,
        string badgeName)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Auth.AuthorizationHeader);

            if (!http.DefaultRequestHeaders.Contains("Unity-Project-Id"))
                http.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);

            // GET badges endpoint to list all badges
            var url = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets/{bucketId}/badges";

            using var resp = await http.GetAsync(url);
            var txt = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Debug.LogWarning($"[CCD] Failed to check badges: {resp.StatusCode} {txt}");
                return (false, null);
            }

            // Parse response as array of badge objects
            var badges = JsonConvert.DeserializeObject<JArray>(txt);
            if (badges == null || badges.Count == 0)
                return (false, null);

            // Find badge with matching name
            foreach (var badge in badges)
            {
                var name = badge["name"]?.ToString();
                if (string.Equals(name, badgeName, StringComparison.OrdinalIgnoreCase))
                {
                    var releaseId = badge["releaseid"]?.ToString();
                    Debug.Log($"[CCD] Badge '{badgeName}' already exists with release ID: {releaseId}");
                    return (true, releaseId);
                }
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CCD] Error checking if badge exists: {ex.Message}");
            return (false, null);
        }
    }
    
    static string SanitizeBadgeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "build";

        // Replace everything not [0-9a-zA-Z-_] with '-'
        var cleaned = System.Text.RegularExpressions.Regex.Replace(name, "[^0-9A-Za-z-_]", "-");

        // Collapse multiple '-'/_ in a row
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "[-_]{2,}", "-");

        // Trim leading/trailing separators
        cleaned = cleaned.Trim('-', '_');

        // Enforce 1..50 length
        if (cleaned.Length == 0) cleaned = "build";
        if (cleaned.Length > 50) cleaned = cleaned.Substring(0, 50);

        return cleaned;
    }

    // ---------- CORE HELPERS ----------
    void ApplyAddressablesProfileVars()
    {
        var s = AddressableAssetSettingsDefaultObject.Settings;
        if (!s) throw new Exception("Addressables Settings missing.");

        // Use the profile selected in AddressablesSection; fallback to the current active one.
        var useProfileId = string.IsNullOrEmpty(Addr.SelectedProfileId) 
            ? s.activeProfileId 
            : Addr.SelectedProfileId;

        if (string.IsNullOrEmpty(useProfileId))
            throw new Exception("No Addressables profile is active/selected.");

        // If you want builds to use the chosen profile, keep this line.
        s.activeProfileId = useProfileId;

        // Apply safety toggles (these are not profile variables)
        if (Opt.Force_UniqueBundleIds) s.UniqueBundleIds = true;
        if (Opt.Force_RemoteCatalog)   s.BuildRemoteCatalog = true;
        if (Opt.Optimize_CatalogSize)  s.OptimizeCatalogSize = true;
    }
    
    // FIX: return GUID environment IDs (not the human keys)
    string GetEnvId(Env env) => EnvironmentId;
    
    static string SanitizeGuid(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        // keep only hex and dashes (strips RTL marks/spaces)
        var cleaned = new string(value.Where(c =>
            c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F' or '-').ToArray()).Trim();
        return cleaned.ToLowerInvariant();
    }
    
    static string EnsureGuid(string value, string fieldName)
    {
        var cleaned = SanitizeGuid(value);
        if (string.IsNullOrWhiteSpace(cleaned) || !Guid.TryParse(cleaned, out _))
            throw new Exception($"{fieldName} must be a valid GUID (got: '{value ?? "null"}'). " +
                                "Open Unity Dashboard → Project Settings → Environments → copy Environment ID (GUID).");
        return cleaned;
    }
    
    // NEW: List buckets for a specific (project, environment) via MANAGEMENT API (Bearer)
    static async Task<List<CcdBucketInfo>> ListBucketsForEnvironmentManagementAsync(
        string accessToken, string projectId, string environmentId, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", accessToken);

        // Optional but often required by some Unity services setups:
        if (!http.DefaultRequestHeaders.Contains("Unity-Project-Id"))
            http.DefaultRequestHeaders.Add("Unity-Project-Id", projectId);

        var url = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets?per_page=100";
        using var resp = await http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"[CCD] List buckets (mgmt env) failed: {resp.StatusCode} {body}");

        return JsonConvert.DeserializeObject<List<CcdBucketInfo>>(body) ?? new List<CcdBucketInfo>();
    }
    
    void OnEnable()
    {
        // Load all saved data
        LoadPrefs();
        // Ensure Addressables system reflects restored profile
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null && !string.IsNullOrEmpty(Addr.SelectedProfileId))
            settings.activeProfileId = Addr.SelectedProfileId;

        Addr.OnProfileActivated = UpdateCustomRemoteVarsForProfile;

        // Delay UI rebuild to avoid Odin re-serialization clobber
        EditorApplication.delayCall += () =>
        {
            Addr.RestoreProfileSelection();   // apply to UI + Addressables
            AutoSelectPlatformFromActiveProfile();
            RefreshComputedFields();
            Repaint();
        };

        EditorApplication.update += AutoRefreshProfileWatcher;
    }

    void OnDisable()
    {
        SavePrefs();
        EditorApplication.update -= AutoRefreshProfileWatcher;
    }
    
    // Handlers
    private async void OnEnvironmentPicked()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EnvDropdownSelection))
                return;

            // Save the chosen env into the hidden per-env slot
            SelectedEnvironmentId = EnvDropdownSelection;

            // Clear buckets every time env changes
            _bucketOptions?.Clear();
            BucketDropdownSelection = null;
            
            var projectId = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));

            // Exchange ENV-SCOPED token
            var envScopedToken = await GetCcdAccessTokenAsync(
                Auth.ProjectId, Auth.ProjectId, projectId, SelectedEnvironmentId);

            // List buckets for (project, env)
            var buckets = await ListBucketsForEnvironmentManagementAsync(
                envScopedToken, projectId, SelectedEnvironmentId);

            foreach (var b in buckets)
                if (_bucketOptions != null)
                    _bucketOptions.Add(new ValueDropdownItem<string>($"{b.name}  • id={b.id}", b.id));

            // Keep bucket selection empty; user must pick
            BucketDropdownSelection = null;

            // Clear stored bucket for this env until user picks
            SelectedBucketId = "";

            Repaint();
            Debug.Log($"[Env Picked] Loaded {buckets.Count} buckets for env {SelectedEnvironmentId}. Waiting for bucket selection…");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    private void OnBucketPicked()
    {
        if (string.IsNullOrWhiteSpace(BucketDropdownSelection))
            return;

        // Store into hidden per-env slot
        SelectedBucketId = BucketDropdownSelection;

        // Extract and store bucket name from dropdown
        var selected = _bucketOptions.FirstOrDefault(b => b.Value == BucketDropdownSelection);
        SelectedBucketName = selected.Text?.Split('•').FirstOrDefault()?.Trim() ?? "";

        Debug.Log($"[Bucket Picked] Using bucket {SelectedBucketName} (ID: {SelectedBucketId})");
    }
    
    // Works on Addressables 2.7.4 and other 2.x
    static string EvaluateString(AddressableAssetSettings settings, string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;

        var ps = settings.profileSettings;
        if (ps == null) return raw;

        // 2.7.4 prefers (AddressableAssetSettings, string)
        var m2 = typeof(AddressableAssetProfileSettings).GetMethod(
            "EvaluateString",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(AddressableAssetSettings), typeof(string) },
            modifiers: null);

        if (m2 != null)
            return (string)m2.Invoke(ps, new object[] { settings, raw });

        // Some 2.x have (string) only
        var m1 = typeof(AddressableAssetProfileSettings).GetMethod(
            "EvaluateString",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);

        if (m1 != null)
            return (string)m1.Invoke(ps, new object[] { raw });

        // Fallback: no expansion available
        return raw;
    }
    
    public void UpdateCustomRemoteVarsForProfile(string profileId)
    {
        var s  = AddressableAssetSettingsDefaultObject.Settings 
                 ?? throw new Exception("Addressables Settings missing.");
        var ps = s.profileSettings;
        if (string.IsNullOrEmpty(profileId)) return;

        // --- Project & environment tokens ---
        var projectIdToken = !string.IsNullOrWhiteSpace(Auth.ProjectId) && Guid.TryParse(Auth.ProjectId, out _)
            ? Auth.ProjectId
            : "[ProjectId]";

        var bucketIdToken = !string.IsNullOrWhiteSpace(SelectedBucketId) && Guid.TryParse(SelectedBucketId, out _)
            ? SelectedBucketId
            : "[BucketId]";

        // ensure EnvironmentName is the real name (like dev-editor)
        var envName = SelectedEnvironmentName ?? (Environment == Env.Prod ? "production" : "development");

        // --- Ensure required vars exist ---
        EnsureVar(ps, "ProjectId", projectIdToken == "[ProjectId]" ? "" : projectIdToken);
        EnsureVar(ps, "EnvironmentName", envName);
        EnsureVar(ps, "BucketId", bucketIdToken == "[BucketId]" ? "" : bucketIdToken);
        EnsureVar(ps, "ContentBadge", ComputedBadge);

        // ✅ DO NOT override Remote paths — let built-in Remote.BuildPath / Remote.LoadPath handle it
        // The user’s Addressables profile defines them once; we only make sure core vars are up-to-date.

        AssetDatabase.SaveAssets();
    }

    static void EnsureVar(AddressableAssetProfileSettings ps, string name, string initialValue)
    {
        if (!ps.GetVariableNames().Contains(name))
            ps.CreateValue(name, initialValue);
    }

    static void SetVar(AddressableAssetProfileSettings ps, string profileId, string name, string value)
    {
        if (!ps.GetVariableNames().Contains(name)) ps.CreateValue(name, value);
        ps.SetValue(profileId, name, value);
    }
    
    void EnsureContentBadgeMatchesBundleVersion()
    {
        var s  = AddressableAssetSettingsDefaultObject.Settings;
        var ps = s.profileSettings;
        var pid = s.activeProfileId;

        var raw = (Environment == Env.Prod ? "prod-" : "dev-") + PlayerSettings.bundleVersion; // may contain dots
        var safe = SanitizeBadgeName(raw); // make it API-compliant

        SetVar(ps, pid, "ContentBadge", safe);
        AssetDatabase.SaveAssets();
    }
    
    void StartUploadProgressPump(string t)
    {
        _uplTitle = t;
        _uplInfo  = "Initializing...";
        _uplOverall01 = 0f;
        if (_uplPumpActive)
        {
            Debug.Log("[CCD Progress] Pump already active, skipping subscribe.");
            return;
        }
        _uplPumpActive = true;
        EditorApplication.update += UploadProgressPumpTick;
        Debug.Log($"[CCD Progress] Started progress pump: {t}");

        // Force an immediate display
        EditorUtility.DisplayProgressBar(_uplTitle, _uplInfo, 0f);
    }

    void UploadProgressPumpTick()
    {
        if (!_uplPumpActive) {
            EditorApplication.update -= UploadProgressPumpTick;
            EditorUtility.ClearProgressBar();
            Debug.Log("[CCD Progress] Pump stopped and progress bar cleared.");
            return;
        }
        // Always on main thread here
        EditorUtility.DisplayProgressBar(_uplTitle, _uplInfo, Mathf.Clamp01(_uplOverall01));
    }

    void StopUploadProgressPump()
    {
        Debug.Log("[CCD Progress] Stopping progress pump...");
        _uplPumpActive = false; // tick unsubscribes & clears
    }

    /// <summary>
    /// Shows progress bar and waits for Unity Editor to actually render it.
    /// Uses a TaskCompletionSource with EditorApplication.delayCall to ensure the UI updates.
    /// </summary>
    private Task ShowProgressAndWaitAsync(string title, string info, float progress)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Display the progress bar
        EditorUtility.DisplayProgressBar(title, info, progress);

        // Use delayCall to wait for next editor frame, then complete the task
        EditorApplication.delayCall += () =>
        {
            // Use current values (they may have been updated by background thread)
            EditorUtility.DisplayProgressBar(_uplTitle, _uplInfo, Mathf.Clamp01(_uplOverall01));
            tcs.TrySetResult(true);
        };

        return tcs.Task;
    }

    /// <summary>
    /// Waits and continuously updates the progress bar until the given task completes.
    /// This allows the progress bar to update during long-running async operations.
    /// </summary>
    private async Task WaitWithProgressUpdatesAsync(Task uploadTask)
    {
        while (!uploadTask.IsCompleted)
        {
            // Update progress bar with current values
            EditorUtility.DisplayProgressBar(_uplTitle, _uplInfo, Mathf.Clamp01(_uplOverall01));

            // Wait a short time, yielding to editor
            var delayTask = Task.Delay(100);
            var completedTask = await Task.WhenAny(uploadTask, delayTask);

            if (completedTask == uploadTask)
            {
                // Upload finished, check for exceptions
                await uploadTask;
                break;
            }

            // Yield to editor to process UI
            var tcs = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () => tcs.TrySetResult(true);
            await tcs.Task;
        }
    }

    private void DeletePreviousBadgeBuildIfExists(AddressableAssetSettings settings)
    {
        var ps  = settings.profileSettings;
        var pid = settings.activeProfileId;

        // Build the correct expected local folder path
        var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
        var envName      = ps.GetValueByName(pid, "EnvironmentName");
        var bucketName  = SelectedBucketName; // Use bucket name instead of ID
        var badge       = ps.GetValueByName(pid, "ContentBadge") ?? ComputedBadge;

        var absPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "CCDBuildData",
            buildTarget,
            envName,
            bucketName,
            badge);

        if (Directory.Exists(absPath))
        {
            if (EditorUtility.DisplayDialog(
                    "Confirm Local Cleanup",
                    $"A previous Addressables build exists at:\n\n{absPath}\n\nDo you want to delete it before building new content?",
                    "Yes, delete it", "Cancel"))
            {
                try
                {
                    Directory.Delete(absPath, true);
                    Debug.Log($"[LocalCleanup] Deleted old Addressables folder: {absPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LocalCleanup] Failed to delete: {absPath}\n{ex}");
                }
            }
            else
            {
                Debug.Log($"[LocalCleanup] Skipped deleting old folder: {absPath}");
            }
        }
        else
        {
            Debug.Log($"[LocalCleanup] No existing local folder found: {absPath}");
        }
    }
    
    async UniTask DeleteEntriesForCurrentBadgeAsync(
    string projectId, string environmentId, string bucketId, string buildTarget, string badge)
    {
        using var http = CreateHttpWithBasic();

        var prefixes = CandidatePrefixes(buildTarget, badge).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (prefixes.Length == 0)
        {
            Debug.LogWarning("[CCD Cleanup] No candidate prefixes computed; skip delete.");
            return;
        }

        Debug.Log("[CCD Cleanup] Candidate prefixes:\n  " + string.Join("\n  ", prefixes));

        // 1) Page through entries and collect IDs that start with any of the candidate prefixes
        var toDelete = new List<string>();
        var listUrl = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets/{bucketId}/entries?per_page=200";
        int page = 1;
        int matchedDebugCount = 0;

        while (!string.IsNullOrEmpty(listUrl))
        {
            using var listReq  = new HttpRequestMessage(HttpMethod.Get, listUrl);
            using var listResp = await http.SendAsync(listReq);
            var json = await listResp.Content.ReadAsStringAsync();

            if (!listResp.IsSuccessStatusCode)
                throw new Exception($"Failed to list CCD entries: {listResp.StatusCode} {json}");

            var arr = JsonConvert.DeserializeObject<JArray>(json);
            if (arr == null || arr.Count == 0)
                break;

            foreach (var e in arr)
            {
                var id   = (string?)e["entryid"];
                var path = (string?)e["path"];
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path))
                    continue;

                var normPath = path.Replace("\\", "/").Trim('/');
                // Match against any candidate prefix
                if (!prefixes.Any(p => normPath.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
                toDelete.Add(id);
                if (matchedDebugCount < 10)
                {
                    matchedDebugCount++;
                }
            }

            // Pagination via RFC5988 Link header
            if (listResp.Headers.TryGetValues("Link", out var links))
            {
                var linkHeader = links.FirstOrDefault();
                var m = System.Text.RegularExpressions.Regex.Match(linkHeader ?? "", @"<([^>]+)>\s*;\s*rel=""next""");
                listUrl = m.Success ? m.Groups[1].Value : null;
                page++;
            }
            else
            {
                listUrl = null;
            }
        }

        if (toDelete.Count == 0)
        {
            Debug.Log($"[CCD Cleanup] No entries found for badge '{badge}' (BT='{buildTarget}') in bucket {bucketId}.");
            return;
        }

        Debug.Log($"[CCD Cleanup] Found {toDelete.Count} entries for badge '{badge}' — starting deletion...");

        // 2) Batch delete (200 per call), using POST + X-HTTP-Method-Override to play nice in Unity
        const int batchSize = 200;
        int totalDeleted = 0;

        for (int i = 0; i < toDelete.Count; i += batchSize)
        {
            var batch = toDelete.Skip(i).Take(batchSize).Select(id => new { entryid = id }).ToArray();

            var deleteUrl = $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{environmentId}/buckets/{bucketId}/batch/delete/entries";
            var jsonBody  = JsonConvert.SerializeObject(batch);
            using var deleteReq = new HttpRequestMessage(HttpMethod.Post, deleteUrl);
            deleteReq.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            deleteReq.Headers.Add("X-HTTP-Method-Override", "DELETE");

            using var deleteResp = await http.SendAsync(deleteReq);
            var deleteTxt = await deleteResp.Content.ReadAsStringAsync();

            if (!deleteResp.IsSuccessStatusCode)
            {
                Debug.LogError($"[CCD Cleanup] Delete batch failed: {deleteResp.StatusCode}\n{deleteTxt}");
                throw new Exception($"Failed to delete CCD entries batch: {deleteResp.StatusCode}");
            }

            totalDeleted += batch.Length;
            Debug.Log($"[CCD Cleanup] 🗑️ Deleted {batch.Length} entries (total {totalDeleted}/{toDelete.Count})");
        }

        Debug.Log($"[CCD Cleanup] ✅ Successfully deleted {totalDeleted} entries for badge '{badge}' (BuildTarget='{buildTarget}')");
    }
    
    private HttpClient CreateHttpWithBasic()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Clear();
        // ✅ Use your existing CCD Management API base URL
        http.BaseAddress = new Uri(CCD_MGMT_BASE);

        // ✅ Use your existing Auth.AccessToken or however you store it
        // Replace 'Auth.AccessToken' if your class uses a different field
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Auth.AuthorizationHeader);

        return http;
    }
    
    private void RefreshComputedFields()
    {
        // 🧭 Use the correct bundle version from the active Build Profile
        var newBundle = GetBundleVersionFromActiveBuildProfile(); 
        var newUnity  = Application.unityVersion;
        var newBadge  = $"{(Environment == Env.Prod ? "prod" : "dev")}-{newBundle}";

        // Only update if something actually changed
        if (newBundle == _lastBundleVersion && newUnity == _lastUnityVersion) return;
        _lastBundleVersion = newBundle;
        _lastUnityVersion  = newUnity;

        // No assignment needed if CurrentBundleVersion is a computed property
        ComputedBadge = newBadge;

        Repaint(); // force Odin redraw
        Debug.Log($"[CCD Refresh] Updated badge → {ComputedBadge}, Unity → {newUnity}, Bundle → {newBundle}");
    }
    
    private void OnFocus()
    {
        RefreshComputedFields();
    }

    private void OnInspectorUpdate()
    {
        RefreshComputedFields();
    }
    
    private static string GetBundleVersionFromActiveBuildProfile()
    {
        try
        {
            // 🧩 Get the currently active build profile (public API in 6.2+)
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile == null)
            {
                Debug.LogWarning("[GetBundleVersionFromActiveBuildProfile] No active Build Profile found.");
                return PlayerSettings.bundleVersion;
            }

            // 🧩 Use reflection to reach its private m_PlayerSettings field
            var playerSettingsField = activeProfile.GetType()
                .GetField("m_PlayerSettings", BindingFlags.NonPublic | BindingFlags.Instance);

            var playerSettingsObj = playerSettingsField?.GetValue(activeProfile);
            if (playerSettingsObj == null)
            {
                Debug.LogWarning("[GetBundleVersionFromActiveBuildProfile] BuildProfile has no player settings override.");
                return PlayerSettings.bundleVersion;
            }

            // 🧩 Inside that object, the actual string field is m_BundleVersion
            var bundleField = playerSettingsObj.GetType()
                .GetField("m_BundleVersion", BindingFlags.NonPublic | BindingFlags.Instance);

            if (bundleField == null) return PlayerSettings.bundleVersion;
            var version = bundleField.GetValue(playerSettingsObj) as string;
            return !string.IsNullOrEmpty(version) ? version :
                // 🧩 If no override, fallback
                PlayerSettings.bundleVersion;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GetBundleVersionFromActiveBuildProfile] Reflection failed: {ex.Message}");
            return PlayerSettings.bundleVersion;
        }
    }
    
    private void AutoRefreshProfileWatcher()
    {
        var active = BuildProfile.GetActiveBuildProfile();
        var guid = active != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(active)) : "";
        if (guid == _lastActiveProfileGuid) return;
        _lastActiveProfileGuid = guid;
        RefreshComputedFields();
    }
    
    // Normalize path coming from CCD JSON
    static string NormalizeCcdPath(string s)
        => (s ?? "").Replace('\\','/').Trim().TrimStart('/');

    // Extracts badge segment: "<BuildTarget>/<Badge>/..."
    // returns "" if structure isn't as expected
    static string ExtractBadgeFromPath(string ccdPath)
    {
        var p = NormalizeCcdPath(ccdPath);
        var parts = p.Split('/');
        return parts.Length >= 2 ? parts[1] : "";
    }

    IEnumerable<string> CandidatePrefixes(string buildTarget, string badge)
    {
        buildTarget = (buildTarget ?? "").Replace("\\", "/").Trim('/');
        badge       = (badge ?? "").Trim();

        if (string.IsNullOrEmpty(buildTarget) || string.IsNullOrEmpty(badge))
            yield break;

        // Split only once at the first dash after "dev-"
        string prefix = badge;
        string versionPart = "";

        var dashIndex = badge.IndexOf('-');
        if (dashIndex > 0 && dashIndex < badge.Length - 1)
        {
            prefix = badge.Substring(0, dashIndex);           // "dev"
            versionPart = badge.Substring(dashIndex + 1);     // "8-7-18227"
        }

        // ✅ Real CCD pattern: dev-[version-with-dots]
        var badgeDots = versionPart.Length > 0
            ? $"{prefix}-{versionPart.Replace('-', '.')}"     // "dev-8.7.18227"
            : badge.Replace('-', '.');

        // Include all possible patterns just in case
        var badgeVariants = new[]
        {
            badge,         // "dev-8-7-18227"
            badgeDots,     // ✅ "dev-8.7.18227"
            badge.Replace('-', '.'), // "dev.8.7.18227"
        }.Distinct();

        foreach (var b in badgeVariants)
        {
            var norm = $"{buildTarget}/{b}".Replace("\\", "/").Trim('/');
            yield return norm + "/";
            yield return norm;
        }
    }
    
    private void AutoSelectPlatformFromActiveProfile()
    {
        try
        {
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;

            switch (activeTarget)
            {
                case BuildTarget.Android:
                    Opt.TargetPlatform = BuildTarget.Android;
                    Opt.TargetArtifact = Artifact.APK;
                    break;

                case BuildTarget.iOS:
                    Opt.TargetPlatform = BuildTarget.iOS;
                    break;

                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    Opt.TargetPlatform = BuildTarget.StandaloneWindows64;
                    Opt.TargetArtifact = Artifact.EXE;
                    break;

                default:
                    Opt.TargetPlatform = activeTarget;
                    Opt.TargetArtifact = Artifact.EXE;
                    break;
            }

            Debug.Log($"[AutoSelectPlatform] Active build target detected: {activeTarget}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AutoSelectPlatform] Failed to auto-detect platform: {ex.Message}");
        }
    }
    
    //-------------------------------// FOR TESTING BUTTONS:
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Medium), GUIColor(1f, 0.8f, 0.6f)]
    [InfoBox("Test local cleanup by removing the previous Addressables build folder if it exists.")]
    public void Delete_Previous_Local_Build()
    {
        try
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings 
                           ?? throw new Exception("Addressables Settings missing.");

            DeletePreviousBadgeBuildIfExists(settings);

            EditorUtility.DisplayDialog("Cleanup Complete", 
                "DeletePreviousBadgeBuildIfExists() executed.\nCheck Console for results.", 
                "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Test_Delete_Previous_Local_Build] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Large)]
    [GUIColor(0.7f, 1f, 0.7f)]
    [InfoBox("Builds Addressables locally only — does not upload or assign badge.")]
    public async void Build_Addressables_Only()
    {
        try
        {
            ApplyAddressablesProfileVars();
            var settings = AddressableAssetSettingsDefaultObject.Settings
                           ?? throw new Exception("Addressables Settings missing.");

            var ps  = settings.profileSettings;
            var pid = settings.activeProfileId;

            // 🧩 Always make sure the active profile is up to date
            EnsureContentBadgeMatchesBundleVersion();

            var envName = ps.GetValueByName(pid, "EnvironmentName");
            if (string.IsNullOrEmpty(envName))
                throw new Exception("Profile variable 'EnvironmentName' is empty. Please select a valid environment.");

            var bucketId  = SelectedBucketId;
            var projectId = Auth.ProjectId;
            var envId     = GetEnvId(Environment);
            var badge     = ps.GetValueByName(pid, "ContentBadge") ?? ComputedBadge;
            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();

            // ✅ Check if badge already exists in CCD
            if (!string.IsNullOrEmpty(projectId) && !string.IsNullOrEmpty(envId) && !string.IsNullOrEmpty(bucketId))
            {
                EditorUtility.DisplayProgressBar("CCD Check", $"Checking if badge '{badge}' already exists...", 0.1f);
                var (badgeExists, existingReleaseId) = await CheckIfBadgeExistsAsync(projectId, envId, bucketId, badge);
                EditorUtility.ClearProgressBar();

                if (badgeExists)
                {
                    // Badge already exists - give user options
                    var choice = EditorUtility.DisplayDialogComplex(
                        "Badge Already Exists",
                        $"Badge '{badge}' already exists in CCD with release ID: {existingReleaseId}\n\n" +
                        "This usually means the content for this version is already uploaded.\n\n" +
                        "What would you like to do?",
                        "Skip Build (Use Existing)",  // Button 0
                        "Cancel",                      // Button 1
                        "Force Rebuild");              // Button 2

                    switch (choice)
                    {
                        case 0: // Skip Build
                            Debug.Log($"[CCD] Badge '{badge}' already exists. Skipping local build.");
                            EditorUtility.DisplayDialog("Using Existing Content",
                                $"✅ Badge '{badge}' is already available in CCD.\n\nNo local build needed. " +
                                $"Your content is already built for this version.", "OK");
                            return;
                        case 1: // Cancel
                            Debug.Log($"[CCD] User cancelled build after seeing badge '{badge}' already exists.");
                            return;
                        case 2: // Force Rebuild
                            Debug.Log($"[CCD] User chose to force rebuild for badge '{badge}' even though it exists.");
                            break;
                    }
                }
            }

            // 🧱 Force-correct RemoteBuildPath & RemoteLoadPath before building
            ps.SetValue(pid, "BucketId", bucketId);
            ps.SetValue(pid, "EnvironmentName", envName);
            ps.SetValue(pid, "ContentBadge", badge);

            ps.SetValue(pid, "Remote.LoadPath",
                $"https://[ProjectId].client-api.unity3dusercontent.com/client_api/v1/environments/{envName}/buckets/[BucketId]/release_by_badge/[ContentBadge]/entry_by_path/content/?path=[BuildTarget]/[ContentBadge]");
            ps.SetValue(pid, "Remote.BuildPath", $"CCDBuildData/[BuildTarget]/{envName}/{SelectedBucketName}/[ContentBadge]");

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 🔹 Confirm before building
            if (!EditorUtility.DisplayDialog("Confirm Local Build",
                    $"This will build Addressables locally only.\n\nEnvironment: {envName}\nBadge: {badge}\nBuildTarget: {buildTarget}\n\nProceed?",
                    "Yes, Build", "Cancel"))
                return;

            // 🧹 Clean old build folder first
            DeletePreviousBadgeBuildIfExists(settings);

            Debug.Log("[Build_Addressables_Only] Starting Addressables build...");

            // Show progress bar (it will display briefly before build freezes Unity)
            EditorUtility.DisplayProgressBar("Build Addressables", "Building Addressables... (Unity will freeze during build)", 0.25f);

            // Build synchronously - this will freeze Unity, which is expected
            using (new RemotePathOverrideScope(settings, SelectedBucketId, envName))
            {
                AddressableAssetSettings.BuildPlayerContent();
            }

            Debug.Log("[Build_Addressables_Only] Addressables build completed.");

            // Wait until folder appears (Unity delay safety)
            var localDir = GetEvaluatedRemoteBuildPath(settings);
            var waitAttempts = 0;
            while (!Directory.Exists(localDir) && waitAttempts < 20)
            {
                await Task.Delay(500);
                waitAttempts++;
            }

            if (!Directory.Exists(localDir))
                throw new DirectoryNotFoundException($"Build folder not found: {localDir}");

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("✅ Addressables Build Complete",
                $"Built successfully!\n\nOutput Folder:\n{localDir}", "OK");

            Debug.Log($"[CCD Build Only] ✅ Addressables built successfully → {localDir}");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Build_Addressables_Only] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Medium), GUIColor(0.9f, 0.95f, 1f)]
    [InfoBox("Debug: List all entries and print derived badge + whether it matches current badge.")]
    public async void List_All_Remote_Entries_With_Badge_Info()
    {
        try
        {
            var settings  = AddressableAssetSettingsDefaultObject.Settings
                            ?? throw new Exception("Addressables Settings missing.");
            var ps  = settings.profileSettings;
            var pid = settings.activeProfileId;

            var envId     = EnsureGuid(GetEnvId(Environment), "SelectedEnvironmentId");
            var bucketId  = EnsureGuid(SelectedBucketId, "SelectedBucketId");
            var projectId = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));

            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            var badgeRaw    = ps.GetValueByName(pid, "ContentBadge");
            var badge       = string.IsNullOrWhiteSpace(badgeRaw) ? ComputedBadge : badgeRaw;

            using var http = CreateHttpWithBasic();
            var listUrl =
                $"{CCD_MGMT_BASE}/projects/{projectId}/environments/{envId}/buckets/{bucketId}/entries?per_page=200";

            var prefixes = CandidatePrefixes(buildTarget, badge).Select(p => p.Trim('/') + "/").ToArray();
            Debug.Log($"[CCD Debug] Current BuildTarget='{buildTarget}', Badge='{badge}'");

            int total = 0, matches = 0, page = 1;

            while (!string.IsNullOrEmpty(listUrl))
            {
                using var listReq  = new HttpRequestMessage(HttpMethod.Get, listUrl);
                using var listResp = await http.SendAsync(listReq);
                var json = await listResp.Content.ReadAsStringAsync();

                if (!listResp.IsSuccessStatusCode)
                {
                    Debug.LogError($"[CCD Debug] List failed: {listResp.StatusCode}\n{json}");
                    return;
                }

                var arr = JsonConvert.DeserializeObject<JArray>(json);
                if (arr == null || arr.Count == 0) break;

                foreach (var e in arr)
                {
                    string id   = (string?)e["entryid"] ?? "(null)";
                    string path = (string?)e["path"]    ?? "(no path)";
                    string badgeFromPath = ExtractBadgeFromPath(path);
                    string norm = NormalizeCcdPath(path);
                    bool match  = prefixes.Any(pfx => norm.StartsWith(pfx, StringComparison.OrdinalIgnoreCase));
                    
                    total++;
                    if (match) matches++;
                }

                if (listResp.Headers.TryGetValues("Link", out var links))
                {
                    var linkHeader = links.FirstOrDefault();
                    var m = System.Text.RegularExpressions.Regex.Match(linkHeader ?? "", @"<([^>]+)>\s*;\s*rel=""next""");
                    listUrl = m.Success ? m.Groups[1].Value : null;
                    page++;
                }
                else listUrl = null;
            }

            Debug.Log($"[CCD Debug] ✅ Total entries: {total}. Matching current badge: {matches}.");
            EditorUtility.DisplayDialog("CCD Entries Debug",
                $"Total entries: {total}\nMatching badge '{badge}': {matches}\nCheck Console for details.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Debug_List_All_Remote_Entries_With_Badge_Info] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Medium), GUIColor(1f, 0.6f, 0.6f)]
    [InfoBox("🧹 Debug: Deletes ONLY the current badge entries in CCD (the same version).")]
    public async void Delete_Current_Badge_Entries()
    {
        try
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings
                           ?? throw new Exception("Addressables Settings missing.");

            var ps  = settings.profileSettings;
            var pid = settings.activeProfileId;

            // --- Resolve context ---
            var envId      = EnsureGuid(GetEnvId(Environment), "SelectedEnvironmentId");
            var bucketId   = EnsureGuid(SelectedBucketId, "SelectedBucketId");
            var projectId  = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));
            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();

            var profileBadge = ps.GetValueByName(pid, "ContentBadge") ?? "";
            var rawBadge     = string.IsNullOrWhiteSpace(profileBadge) ? ComputedBadge : profileBadge;
            var badge        = SanitizeBadgeName(rawBadge);

            // ✅ Check if badge already exists in CCD
            EditorUtility.DisplayProgressBar("CCD Check", $"Checking if badge '{badge}' exists...", 0.1f);
            var (badgeExists, existingReleaseId) = await CheckIfBadgeExistsAsync(projectId, envId, bucketId, badge);
            EditorUtility.ClearProgressBar();

            if (!badgeExists)
            {
                Debug.Log($"[CCD] Badge '{badge}' does not exist in CCD. Nothing to delete.");
                EditorUtility.DisplayDialog("Badge Not Found",
                    $"Badge '{badge}' does not exist in CCD.\n\nThere are no entries to delete for this badge.", "OK");
                return;
            }

            // Confirm
            if (!EditorUtility.DisplayDialog(
                    "Confirm CCD Deletion",
                    $"This will DELETE ALL entries in CCD for:\n\n" +
                    $"Build Target: {buildTarget}\n" +
                    $"Badge: {badge}\n" +
                    $"Release ID: {existingReleaseId}\n" +
                    $"Bucket: {bucketId}\n\nProceed?",
                    "Yes, delete", "Cancel"))
                return;

            EditorUtility.DisplayProgressBar("CCD Cleanup", $"Deleting entries for badge {badge}...", 0.5f);

            await DeleteEntriesForCurrentBadgeAsync(projectId, envId, bucketId, buildTarget, badge);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("CCD Cleanup Complete",
                $"✅ CCD entries for badge '{badge}' deleted successfully.", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Test_Delete_Current_Badge_Entries] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Large)]
    [GUIColor(0.7f, 0.85f, 1f)]
    [InfoBox("Uploads the built Addressables to CCD only — no release or badge assignment.")]
    public async void Upload_Addressables_Only()
    {
        try
        {
            ApplyAddressablesProfileVars();

            var settings = AddressableAssetSettingsDefaultObject.Settings
                           ?? throw new Exception("Addressables Settings missing.");
            var ps        = settings.profileSettings;
            var pid       = settings.activeProfileId;
            var envName    = ps.GetValueByName(pid, "EnvironmentName");
            var projectId = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));
            var envId     = EnsureGuid(GetEnvId(Environment), "SelectedEnvironmentId");
            var bucketId  = EnsureGuid(SelectedBucketId, "SelectedBucketId");

            if (string.IsNullOrEmpty(envName))
                throw new Exception("Profile variable 'EnvironmentName' is empty. Please select a valid environment.");

            var badge       = ps.GetValueByName(pid, "ContentBadge") ?? ComputedBadge;
            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();

            // ✅ Update profile variables to use bucket name BEFORE evaluating
            ps.SetValue(pid, "BucketId", bucketId);
            ps.SetValue(pid, "EnvironmentName", envName);
            ps.SetValue(pid, "ContentBadge", badge);
            ps.SetValue(pid, "Remote.BuildPath", $"CCDBuildData/[BuildTarget]/{envName}/{SelectedBucketName}/[ContentBadge]");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var localDir = GetEvaluatedRemoteBuildPath(settings);
            if (!Directory.Exists(localDir))
                throw new DirectoryNotFoundException($"Local Addressables build folder not found:\n{localDir}");

            // ✅ Check if badge already exists in CCD
            EditorUtility.DisplayProgressBar("CCD Check", $"Checking if badge '{badge}' already exists...", 0.1f);
            var (badgeExists, existingReleaseId) = await CheckIfBadgeExistsAsync(projectId, envId, bucketId, badge);
            EditorUtility.ClearProgressBar();

            if (badgeExists)
            {
                // Badge already exists - give user options
                var choice = EditorUtility.DisplayDialogComplex(
                    "Badge Already Exists",
                    $"Badge '{badge}' already exists in CCD with release ID: {existingReleaseId}\n\n" +
                    "This usually means the content for this version is already uploaded.\n\n" +
                    "What would you like to do?",
                    "Skip Upload (Use Existing)",  // Button 0
                    "Cancel",                       // Button 1
                    "Force Re-upload");             // Button 2

                switch (choice)
                {
                    case 0: // Skip Upload
                        Debug.Log($"[CCD] Badge '{badge}' already exists. Skipping upload.");
                        EditorUtility.DisplayDialog("Using Existing Content",
                            $"✅ Badge '{badge}' is already available in CCD.\n\nNo upload needed. " +
                            $"Your content is already uploaded for this version.", "OK");
                        return;
                    case 1: // Cancel
                        Debug.Log($"[CCD] User cancelled upload after seeing badge '{badge}' already exists.");
                        return;
                    case 2: // Force Re-upload
                        Debug.Log($"[CCD] User chose to force re-upload for badge '{badge}' even though it exists.");
                        break;
                }
            }

            if (!EditorUtility.DisplayDialog("Confirm Upload to CCD",
                    $"This will upload the local Addressables build to CCD.\n\nEnvironment: {envName}\nBucket: {bucketId}\nBadge: {badge}\n\nSource Folder:\n{localDir}\n\nProceed?",
                    "Yes, Upload", "Cancel"))
                return;

            Debug.Log("[CCD Upload] Starting upload process...");

            // ✅ Upload everything from build folder (progress bar is handled inside)
            await UploadDirectoryToCcdWithProgressAsync(projectId, envId, bucketId, localDir);

            Debug.Log("[CCD Upload] Upload process completed.");

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("✅ Upload Complete",
                $"Addressables uploaded successfully to CCD!\n\nEnv: {envName}\nBucket: {bucketId}\nFolder:\n{localDir}", "OK");

            Debug.Log($"[CCD Upload Only] ✅ Uploaded Addressables from '{localDir}' → Env={envName}, Bucket={bucketId}");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Upload_Addressables_Only] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Large)]
    [GUIColor(1f, 0.9f, 0.6f)]
    [InfoBox("Creates a new CCD release from existing bucket entries and assigns the current badge. Does NOT build or upload.")]
    public async void Create_Release_And_Assign_Badge_Only()
    {
        try
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings
                           ?? throw new Exception("Addressables Settings missing.");

            var ps         = settings.profileSettings;
            var pid        = settings.activeProfileId;
            var projectId  = EnsureGuid(Auth.ProjectId, nameof(Auth.ProjectId));
            var envId      = EnsureGuid(GetEnvId(Environment), "SelectedEnvironmentId");
            var bucketId   = EnsureGuid(SelectedBucketId, "SelectedBucketId");
            var envName    = ps.GetValueByName(pid, "EnvironmentName") ?? SelectedEnvironmentName;
            var bucketName = SelectedBucketName;
            var profileBadge = ps.GetValueByName(pid, "ContentBadge") ?? "";
            var rawBadge         = string.IsNullOrWhiteSpace(profileBadge) ? ComputedBadge : profileBadge;
            var badge = SanitizeBadgeName(rawBadge);

            // ✅ Check if badge already exists in CCD
            EditorUtility.DisplayProgressBar("CCD Check", $"Checking if badge '{badge}' already exists...", 0.1f);
            var (badgeExists, existingReleaseId) = await CheckIfBadgeExistsAsync(projectId, envId, bucketId, badge);
            EditorUtility.ClearProgressBar();

            if (badgeExists)
            {
                // Badge already exists - ask if user wants to update it with a new release
                var choice = EditorUtility.DisplayDialogComplex(
                    "Badge Already Exists",
                    $"Badge '{badge}' is already assigned to release ID: {existingReleaseId}\n\n" +
                    $"Environment: {envName}\n" +
                    $"Bucket: {bucketName}\n\n" +
                    "What would you like to do?",
                    "Update Badge (New Release)",  // Button 0 - Create new release and reassign badge
                    "Cancel",                       // Button 1 - Do nothing
                    "Keep Existing");               // Button 2 - Don't create new release

                switch (choice)
                {
                    case 0: // Update Badge - Create new release and reassign
                        Debug.Log($"[CCD] User chose to update badge '{badge}' with a new release (previous: {existingReleaseId}).");
                        break;
                    case 1: // Cancel
                        Debug.Log($"[CCD] User cancelled release creation.");
                        return;
                    case 2: // Keep Existing
                        Debug.Log($"[CCD] Badge '{badge}' already exists. Keeping existing release {existingReleaseId}.");
                        EditorUtility.DisplayDialog("Using Existing Release",
                            $"✅ Badge '{badge}' will continue using existing release.\n\n" +
                            $"Release ID: {existingReleaseId}\n\n" +
                            "No changes made.", "OK");
                        return;
                }
            }

            if (!EditorUtility.DisplayDialog("Confirm Create Release + Badge",
                    $"This will:\n• Create a new release from current CCD bucket entries\n• Assign badge '{badge}' to that release\n\n" +
                    $"Environment: {envName} ({envId})\nBucket: {bucketName} ({bucketId})\n\n" +
                    (badgeExists ? $"⚠️ This will UPDATE the existing badge (old release: {existingReleaseId})\n\n" : "") +
                    "Proceed?",
                    "Yes, Continue", "Cancel"))
                return;

            EditorUtility.DisplayProgressBar("CCD Release + Badge", "Step 1/2 • Creating release...", 0.4f);

            // 🟡 Step 1: Create Release
            var releaseId = await CreateCcdRelease_MgmtAsync(
                projectId,
                envId,
                bucketId,
                $"Manual release for {badge}");

            EditorUtility.DisplayProgressBar("CCD Release + Badge", "Step 2/2 • Assigning badge...", 0.8f);

            // 🟡 Step 2: Assign Badge
            await AssignCcdBadge_MgmtAsync(
                projectId,
                envId,
                bucketId,
                badge,
                releaseId);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("✅ Release + Badge Complete",
                $"Created release and assigned badge successfully!\n\nBadge: {badge}\nReleaseId: {releaseId}", "OK");

            Debug.Log($"[CCD Release+Badge] ✅ Created Release {releaseId} and assigned Badge '{badge}'");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Create_Release_And_Assign_Badge_Only] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    [TabGroup("Tabs", "For Testing Only")]
    [Button(ButtonSizes.Large)]
    [GUIColor(0.8f, 1f, 0.8f)]
    [InfoBox("Builds the game player only — based on selected environment and target platform/artifact.")]
    public void Build_Player_Only()
    {
        try
        {
            // 🧩 Ensure Addressables profile vars are ready (adds BuildTarget/ContentBadge to RemoteLoadPath)
            EnsureAddressablesProfileSynced_NoBuild();

            var isProd     = Environment == Env.Prod;
            var platform   = Opt.TargetPlatform;
            var artifact   = Opt.TargetArtifact;
            var version    = PlayerSettings.bundleVersion;
            var product    = PlayerSettings.productName;

            // ✅ Android keystore password validation
            if (platform == BuildTarget.Android)
            {
                if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
                {
                    if (EditorUtility.DisplayDialog(
                        "Missing Android Keystore Password",
                        "Android keystore password is not set!\n\n" +
                        "Please set it in:\n" +
                        "Edit > Project Settings > Player > Android > Publishing Settings > Keystore Password\n\n" +
                        "Or click OK to open Project Settings now.",
                        "OK", "Cancel"))
                    {
                        // Open Project Settings to Android Player tab
                        UnityEditor.SettingsService.OpenProjectSettings("Project/Player");
                    }
                    return;
                }
            }

            // 🕓 Add timestamp (safe for filenames)
            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss");

            // ---------- DETERMINE EXTENSION ----------
            string ext = artifact switch
            {
                Artifact.EXE => "exe",
                Artifact.AAB => "aab",
                Artifact.APK => "apk",
                _ => "build"
            };

            // ---------- STRUCTURED OUTPUT PATH ----------
            string buildFolder = Path.Combine(
                Opt.OutputRoot,
                isProd ? "Prod" : "Dev",
                version,
                platform.ToString());

            Directory.CreateDirectory(buildFolder);

            string buildFile = Path.Combine(
                buildFolder,
                $"{product}-{version}-{(isProd ? "prod" : "dev")}-{timestamp}.{ext}");

            // ---------- CONFIRM ----------
            if (!EditorUtility.DisplayDialog(
                    "Confirm Player Build",
                    $"Environment: {(isProd ? "Production" : "Development")}\n" +
                    $"Platform: {platform}\nArtifact: {artifact}\n\n" +
                    $"Version: {version}\nOutput:\n{buildFile}\n\nProceed?",
                    "Build", "Cancel"))
                return;

            // ---------- PREP SCENES ----------
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new Exception("No scenes are enabled in Build Settings.");

            // ---------- BUILD OPTIONS ----------
            var buildOpts = isProd
                ? BuildOptions.None
                : BuildOptions.Development
                  | (Opt.Dev_Profiler ? BuildOptions.ConnectWithProfiler : 0)
                  | (Opt.Dev_Debugging ? BuildOptions.AllowDebugging : 0);

            // ---------- START BUILD ----------
            EditorUtility.DisplayProgressBar("Building Player", $"Building {platform} ({artifact})...", 0.35f);
            BuildReport report;

            switch (platform)
            {
                case BuildTarget.Android:
                    EditorUserBuildSettings.buildAppBundle = artifact == Artifact.AAB;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                    report = BuildPipeline.BuildPlayer(scenes, buildFile, BuildTarget.Android, buildOpts);
                    break;

                case BuildTarget.StandaloneWindows64:
                    report = BuildPipeline.BuildPlayer(scenes, buildFile, BuildTarget.StandaloneWindows64, buildOpts);
                    break;

                default:
                    EditorUtility.ClearProgressBar();
                    throw new NotSupportedException($"Platform {platform} not supported yet.");
            }

            EditorUtility.ClearProgressBar();

            // ---------- RESULT ----------
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build_Player_Only] ✅ Build completed successfully: {buildFile}");
                EditorUtility.DisplayDialog("✅ Build Complete",
                    $"Build succeeded!\n\nOutput:\n{buildFile}", "OK");
            }
            else
            {
                throw new Exception($"Build failed: {report.summary.result}");
            }
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[Build_Player_Only] FAILED: {ex}");
            EditorUtility.DisplayDialog("Error", ex.Message, "OK");
        }
    }
    
    private void EnsureAddressablesProfileSynced_NoBuild()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings
                       ?? throw new Exception("Addressables Settings missing.");
        var ps  = settings.profileSettings;
        var pid = settings.activeProfileId;

        EnsureContentBadgeMatchesBundleVersion();

        var envName = ps.GetValueByName(pid, "EnvironmentName");
        if (string.IsNullOrEmpty(envName))
            throw new Exception("Profile variable 'EnvironmentName' is empty. Please select an environment.");

        var bucketId  = SelectedBucketId ?? "";
        var projectId = Auth.ProjectId ?? "";
        var badgeRaw  = ps.GetValueByName(pid, "ContentBadge");
        var badge     = string.IsNullOrWhiteSpace(badgeRaw) ? ComputedBadge : badgeRaw;

        // ✅ Just update variables, no rebuild
        ps.SetValue(pid, "BucketId", bucketId);
        ps.SetValue(pid, "EnvironmentName", envName);
        ps.SetValue(pid, "ContentBadge", badge);
        ps.SetValue(pid, "ProjectId", projectId);
        
        ps.SetValue(pid, "Remote.BuildPath", $"CCDBuildData/[BuildTarget]/{envName}/[BucketId]/[ContentBadge]");
        ps.SetValue(pid, "Remote.LoadPath",
            $"https://[ProjectId].client-api.unity3dusercontent.com/client_api/v1/environments/{envName}/buckets/[BucketId]/release_by_badge/[ContentBadge]/entry_by_path/content/?path=[BuildTarget]/[ContentBadge]");

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[EnsureAddressablesProfileSynced_NoBuild] ✅ Addressables profile vars updated (no rebuild).");
    }
}

[Serializable]
public sealed class AuthSection
{
    [TitleGroup("Auth", "Authentication & CCD Credentials", Order = -50)]
    [BoxGroup("Auth/Service Account")]
    [LabelText("Key ID"), ReadOnly] public string KeyId = "";

    [BoxGroup("Auth/Service Account")]
    [LabelText("Secret"), ReadOnly] public string Secret = "";

    [BoxGroup("Auth/Service Account")]
    [LabelText("Authorization Header"), ReadOnly] public string AuthorizationHeader = "";

    [TitleGroup("CCD", "Cloud Content Delivery Settings", Order = -40)]
    [BoxGroup("CCD/Project & Bucket")]
    [LabelText("Project ID"), ReadOnly] public string ProjectId = "";
}

[Serializable]
public sealed class AddressablesSection
{
    [NonSerialized] public Action<string> OnProfileActivated; // profileId
    
    [TitleGroup("Addressables", "Profiles & Paths (read-only)", Order = -30)]
    [BoxGroup("Addressables/Profile")]
    [LabelText("Select Profile")]
    [ValueDropdown(nameof(GetProfileOptions))]
    [OnValueChanged(nameof(OnProfilePicked))]
    [SerializeField] public string SelectedProfileName;  // <-- persists

    [BoxGroup("Addressables/Profile")]
    [ShowInInspector, ReadOnly, LabelText("Active Profile ID")]
    [SerializeField] public string SelectedProfileId;    // <-- persists

    // Cache of ALL variables: name -> (raw, eval). Not serialized; we rebuild.
    [NonSerialized] private readonly Dictionary<string, (string raw, string eval)> _vars =
        new(StringComparer.Ordinal);
    [NonSerialized] private string[] _orderedNames = Array.Empty<string>();

    // ONE unified section that draws all variables as "Label : Value"
    [TitleGroup("Profile Variables", Order = 10)]
    [OnInspectorGUI]
    private void DrawProfileVariables()
    {
        if (_orderedNames == null || _orderedNames.Length == 0)
        {
            EditorGUILayout.HelpBox("No profile selected.", MessageType.Info);
            return;
        }

        var valueStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
        var rawStyle   = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        foreach (var name in _orderedNames)
        {
            if (!_vars.TryGetValue(name, out var v)) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name + ":", GUILayout.Width(160));
            EditorGUILayout.LabelField(v.eval ?? "", valueStyle);
            EditorGUILayout.EndHorizontal();

            if (!string.Equals(v.eval, v.raw, StringComparison.Ordinal) && !string.IsNullOrEmpty(v.raw))
            {
                using (new EditorGUI.IndentLevelScope(1))
                    EditorGUILayout.LabelField("Raw: " + v.raw, rawStyle);
            }

            EditorGUILayout.Space(4);
        }
    }

    // Called by window on reopen (OnEnable) to restore selection & rebuild UI
    public void RestoreProfileSelection()
    {
        var s = AddressableAssetSettingsDefaultObject.Settings;
        if (!s) return;

        string pid = null;
        if (!string.IsNullOrEmpty(SelectedProfileName))
            pid = s.profileSettings.GetProfileId(SelectedProfileName);
        if (string.IsNullOrEmpty(pid))
        {
            pid = s.activeProfileId;
            if (!string.IsNullOrEmpty(pid))
                SelectedProfileName = s.profileSettings.GetProfileName(pid);
        }
        if (string.IsNullOrEmpty(pid)) return;

        s.activeProfileId = pid;
        SelectedProfileId = pid;

        // 🔸 make sure custom RemoteBuildPath/RemoteLoadPath get (re)written on reopen
        OnProfileActivated?.Invoke(pid);

        RebuildVariablesCache(s, pid);
    }

    // Dropdown provider
    private IEnumerable<ValueDropdownItem<string>> GetProfileOptions()
    {
        var s = AddressableAssetSettingsDefaultObject.Settings;
        if (!s) yield break;

        var ps = s.profileSettings;
        foreach (var name in GetAllProfileNames(ps))
            yield return new ValueDropdownItem<string>(name, name);
    }

    // On pick: set active + rebuild cache
    private void OnProfilePicked()
    {
        var s = AddressableAssetSettingsDefaultObject.Settings;
        if (!s || string.IsNullOrEmpty(SelectedProfileName)) return;

        var ps  = s.profileSettings;
        var pid = ps.GetProfileId(SelectedProfileName);
        if (string.IsNullOrEmpty(pid)) return;

        s.activeProfileId = pid;       // make it active immediately
        SelectedProfileId  = pid;
        // tell the parent window to update the *custom* vars for this profile
        OnProfileActivated?.Invoke(pid);
        RebuildVariablesCache(s, pid);
    }

    private void RebuildVariablesCache(AddressableAssetSettings s, string profileId)
    {
        _vars.Clear();
        var ps = s.profileSettings;

        foreach (var varName in GetAllVariableNames(ps))
        {
            var raw  = SafeGetValue(ps, profileId, varName);
            var eval = EvaluateString(s, raw);
            _vars[varName] = (raw ?? "", eval ?? "");
        }

        // ✅ Updated to match built-in variable names
        var preferred = new[] { "EnvironmentName", "BucketId", "ContentBadge", "Remote.BuildPath", "Remote.LoadPath" };
        var rest = _vars.Keys.Except(preferred, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        _orderedNames = preferred.Where(_vars.ContainsKey).Concat(rest).ToArray();
    }

    // Helpers
    private static string SafeGetValue(AddressableAssetProfileSettings ps, string profileId, string varName)
        => string.IsNullOrEmpty(varName) ? "" : (ps.GetValueByName(profileId, varName) ?? "");

    private static string EvaluateString(AddressableAssetSettings settings, string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;
        var ps = settings.profileSettings;

        var m2 = typeof(AddressableAssetProfileSettings).GetMethod(
            "EvaluateString",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(AddressableAssetSettings), typeof(string) },
            modifiers: null);
        if (m2 != null) return (string)m2.Invoke(ps, new object[] { settings, raw });

        var m1 = typeof(AddressableAssetProfileSettings).GetMethod(
            "EvaluateString",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (m1 != null) return (string)m1.Invoke(ps, new object[] { raw });

        return raw;
    }

    private static IList<string> GetAllProfileNames(AddressableAssetProfileSettings ps)
    {
        var m = typeof(AddressableAssetProfileSettings).GetMethod(
            "GetAllProfileNames",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m?.Invoke(ps, null) is IEnumerable<string> list) return list.ToList();
        return Array.Empty<string>();
    }

    private static IList<string> GetAllVariableNames(AddressableAssetProfileSettings ps)
    {
        var m = typeof(AddressableAssetProfileSettings).GetMethod(
            "GetVariableNames",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m?.Invoke(ps, null) is IEnumerable<string> list) return list.ToList();
        return Array.Empty<string>();
    }
}

// This tiny holder lets us JsonUtility-pack the window for EditorPrefs (see window code below).
[Serializable]
public sealed class EditorWindowJsonRoot
{
    public static EditorWindowJsonRoot Instance = new ();

    // what we already saved
    public AddressablesSection Addr = new ();

    // NEW: persist env/bucket & current env toggle
    public string EnvironmentId, EnvironmentName, BucketId, BucketName;
    public CcdBuildAndPublish.Env Environment;
    // ✅ Add these:
    public bool DevProfiler;
    public bool DevDebugging;
}

[Serializable]
public sealed class OptionsSection
{
    [TitleGroup("Options", "Player Build & Safety", Order = -20)]
    [BoxGroup("Options/Player Build")]
    [LabelText("Target Platform")]
    [ValueDropdown(nameof(GetAllowedPlatforms))]
    [OnValueChanged(nameof(OnPlatformChanged))]
    public BuildTarget TargetPlatform = BuildTarget.StandaloneWindows64;

    // Only show for Windows & Android
    [BoxGroup("Options/Player Build")]
    [LabelText("Artifact")]
    [ShowIf(nameof(ShowArtifact))]
    [ValueDropdown(nameof(GetArtifactOptions))]
    public CcdBuildAndPublish.Artifact TargetArtifact = CcdBuildAndPublish.Artifact.AAB;

    [BoxGroup("Options/Player Build")]
    [LabelText("Dev: Attach Profiler")]
    public bool Dev_Profiler = true;

    [BoxGroup("Options/Player Build")]
    [LabelText("Dev: Script Debugging")]
    public bool Dev_Debugging = true;

    [BoxGroup("Options/Paths")]
    [LabelText("Output Root")]
    public string OutputRoot = "Builds";

    [BoxGroup("Options/Safety")]
    [LabelText("Force Unique Bundle IDs")]
    public bool Force_UniqueBundleIds = true;

    [BoxGroup("Options/Safety")]
    [LabelText("Build Remote Catalog")]
    public bool Force_RemoteCatalog = true;

    [BoxGroup("Options/Safety")]
    [LabelText("Optimize Catalog Size")]
    public bool Optimize_CatalogSize = true;

    // ----- UI helpers live INSIDE the section so Odin can resolve them -----
    public IEnumerable<ValueDropdownItem<BuildTarget>> GetAllowedPlatforms() => new[]
    {
        new ValueDropdownItem<BuildTarget>("Windows (StandaloneWindows64)", BuildTarget.StandaloneWindows64),
        new ValueDropdownItem<BuildTarget>("iOS",                        BuildTarget.iOS),
        new ValueDropdownItem<BuildTarget>("Android",                    BuildTarget.Android),
    };

    public IEnumerable<ValueDropdownItem<CcdBuildAndPublish.Artifact>> GetArtifactOptions()
    {
        return TargetPlatform switch
        {
            BuildTarget.StandaloneWindows64 => new[]
            {
                new ValueDropdownItem<CcdBuildAndPublish.Artifact>("EXE", CcdBuildAndPublish.Artifact.EXE)
            },
            BuildTarget.Android => new[]
            {
                new ValueDropdownItem<CcdBuildAndPublish.Artifact>("APK", CcdBuildAndPublish.Artifact.APK),
                new ValueDropdownItem<CcdBuildAndPublish.Artifact>("AAB", CcdBuildAndPublish.Artifact.AAB),
            },
            _ => Array.Empty<ValueDropdownItem<CcdBuildAndPublish.Artifact>>()
        };
    }

    public bool ShowArtifact() =>
        TargetPlatform is BuildTarget.StandaloneWindows64 or BuildTarget.Android;

    public void OnPlatformChanged()
    {
        TargetArtifact = TargetPlatform switch
        {
            BuildTarget.StandaloneWindows64 => CcdBuildAndPublish.Artifact.EXE,
            BuildTarget.Android when (TargetArtifact != CcdBuildAndPublish.Artifact.APK &&
                                      TargetArtifact != CcdBuildAndPublish.Artifact.AAB) => CcdBuildAndPublish.Artifact
                .AAB,
            _ => TargetArtifact
        };
    }
}

sealed class RemotePathOverrideScope : IDisposable
{
    readonly AddressableAssetSettings _settings;
    readonly string _activeProfileId;
    readonly string _prevEnvName;
    readonly string _prevBucketId;
    readonly List<(BundledAssetGroupSchema schema, string prevBuild, string prevLoad)> _snapshots = new();

    public RemotePathOverrideScope(AddressableAssetSettings settings, string selectedBucketId, string selectedEnvName)
    {
        _settings = settings;
        var ps = settings.profileSettings;
        _activeProfileId = settings.activeProfileId;

        _prevEnvName  = ps.GetValueByName(_activeProfileId, "EnvironmentName");
        _prevBucketId = ps.GetValueByName(_activeProfileId, "BucketId");

        // ✅ Update built-in Addressables profile variables
        if (!string.IsNullOrWhiteSpace(selectedEnvName))
            ps.SetValue(_activeProfileId, "EnvironmentName", selectedEnvName);
        if (!string.IsNullOrWhiteSpace(selectedBucketId))
            ps.SetValue(_activeProfileId, "BucketId", selectedBucketId);

        // ✅ Point groups to built-in Remote paths (only remote groups)
        foreach (var g in settings.groups)
        {
            if (!g) continue;
            var schema = g.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) continue;

            var prevBuild = schema.BuildPath?.GetName(settings);
            var prevLoad  = schema.LoadPath ?.GetName(settings);

            // Skip groups that are configured as Local (not remote)
            bool isRemoteGroup = (prevBuild?.Contains("Remote") == true) ||
                                 (prevLoad?.Contains("Remote") == true);
            if (!isRemoteGroup)
            {
                Debug.Log($"[RemotePathOverrideScope] Skipping local group: {g.Name}");
                continue;
            }

            _snapshots.Add((schema, prevBuild, prevLoad));

            schema.BuildPath?.SetVariableByName(settings, "Remote.BuildPath");
            schema.LoadPath ?.SetVariableByName(settings, "Remote.LoadPath");
            EditorUtility.SetDirty(schema);
        }

        settings.RemoteCatalogBuildPath?.SetVariableByName(settings, "Remote.BuildPath");
        AssetDatabase.SaveAssets();
    }

    public void Dispose()
    {
        var ps = _settings.profileSettings;

        ps.SetValue(_activeProfileId, "EnvironmentName", _prevEnvName ?? "");
        ps.SetValue(_activeProfileId, "BucketId", _prevBucketId ?? "");

        foreach (var (schema, prevBuild, prevLoad) in _snapshots)
        {
            if (schema.BuildPath != null && !string.IsNullOrEmpty(prevBuild))
                schema.BuildPath.SetVariableByName(_settings, prevBuild);
            if (schema.LoadPath != null && !string.IsNullOrEmpty(prevLoad))
                schema.LoadPath.SetVariableByName(_settings, prevLoad);
            EditorUtility.SetDirty(schema);
        }

        AssetDatabase.SaveAssets();
    }
}

// Progress-reporting content for HttpClient uploads
sealed class StreamWithProgressContent : HttpContent
{
    private readonly Stream _source;
    private readonly long _size;
    private readonly int _bufferSize;
    private readonly Action<long, long> _progress; // (bytesSent, totalBytes)

    public StreamWithProgressContent(Stream source, long size, string mime, int bufferSize, Action<long,long> progress)
    {
        _source = source;
        _size = size;
        _bufferSize = Math.Max(8 * 1024, bufferSize);
        _progress = progress;
        Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(mime) ? "application/octet-stream" : mime);
        Headers.ContentLength = size;
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _size;
        return true;
    }

    protected override async Task SerializeToStreamAsync(Stream target, TransportContext context)
    {
        var buffer = new byte[_bufferSize];
        long sent = 0;
        int read;
        while ((read = await _source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await target.WriteAsync(buffer, 0, read);
            sent += read;
            _progress?.Invoke(sent, _size);
        }
    }
}


[Serializable] class CcdBucketInfo { public string id; public string name; }
[Serializable] class CcdEnvInfo { public string id; public string name; public string key; }

#endif
