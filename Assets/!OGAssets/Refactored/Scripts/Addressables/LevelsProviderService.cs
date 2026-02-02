// LevelsProviderServiceService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Zenject;

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(LevelsProviderService),
    gameObjectName: nameof(LevelsProviderService),
    extraBindings: typeof(ILevelsProviderService)
)]
public class LevelsProviderService : MonoBehaviour, ILevelsProviderService, IProjectInitializable
{
    int IProjectInitializable.Order => -50;
    bool ILevelsProviderService.hideLoadingUIForPrefetchedLevels => hideLoadingUIForPrefetchedLevels;
    int ILevelsProviderService.prefetchAheadCount => prefetchAheadCount;

    [Header("Update policy")]
    public bool checkForUpdatesOnLaunch = true;
    [Tooltip("Minimum hours between catalog checks.")]
    public int updateCheckIntervalHours = 24;
    
    
    [Header("Behavior")]
    [Tooltip("If true, downloads all level assets upfront. If false, you can lazy-load per level.")]
    public bool downloadAllOnStart = true;
    
    [Header("Prefetching")]
    [Tooltip("Number of levels to download ahead of the current level. Set to 0 to disable prefetching.")]
    [Range(0, 10)]
    public int prefetchAheadCount = 2;
    
    [Tooltip("If true, shows loading UI only when a level is not yet downloaded. If false, always shows loading UI.")]
    public bool hideLoadingUIForPrefetchedLevels = true;
    
    // ===== NEW: Modes configuration =====
    [Serializable]
    public class ModeConfig
    {
        public string key = "Default";                  // e.g. "Default", "Legacy", "Portrait"
        public string label = "downloadable/LogDefault"; // addressables label
        public string pathPrefix = "Default";           // e.g. "Default", "Legacy", "Portait"
        public bool hasSlices = true;
    }

    [Header("Modes (edit in Inspector)")]
    public ModeConfig[] modes = { };

    [Header("Active Mode")]
    [SerializeField] private string activeModeKey = "Default";

    // Signals
    public event Action<string> OnStatus;
    public event Action<float> OnProgress;
    public event Action<LoadPhase> OnPhaseChanged;
    public event Action<int, int, float> OnAssetsLoadProgress; // loaded, total, pct
    public event Action<long, long, float> OnDownloadProgress;
    public event Action OnLevelsReady;

    // Data (CURRENT active mode)
    public List<ChampionEntry> Ordered { get; private set; } = new();
    [SerializeField] private LevelsData _levelsData;
    public LevelsData Levels => _levelsData;
    public int ResumeIndex { get; private set; }

    // ---- NEW: service state ----
    public bool Initialized => _initialized;
    public string ActiveModeKey => activeModeKey;

    private bool _initialized;
    private bool _initializing;
    private readonly HashSet<string> _loadedIds = new(StringComparer.OrdinalIgnoreCase);
    bool _showedAddrInitError;
    
    // NEW: per-mode stores
    private readonly Dictionary<string, LevelsData> _levelsByMode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _resumeIndexByMode = new(StringComparer.OrdinalIgnoreCase);

    // NEW: Store discovered level IDs per mode (metadata only, no assets loaded)
    private readonly Dictionary<string, List<string>> _levelIdsByMode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _orderedLevelIdsByMode = new(StringComparer.OrdinalIgnoreCase);
    
    // NEW: Currently loaded level (only one at a time for lazy loading)
    private ChampionEntry _currentLoadedEntry;
    private string _currentLoadedMode;
    
    // NEW: Prefetching tracking
    private readonly HashSet<string> _downloadedLevelIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _downloadedLevelIdsByMode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _prefetchCancellationTokens = new(StringComparer.OrdinalIgnoreCase);

    // Service
    [Inject] private IAddressablesLevelsService _content;

    UniTask IProjectInitializable.Initialize()
    {
        EnsureService();
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        Debug.Log("[IProjectInitializable.Initialize] LevelsProviderServiceService ready.");
        return UniTask.CompletedTask;
    }

    private void EnsureService()
    {
        if (_content == null)
        {
            Debug.LogWarning("[LevelsProvider] IAddressablesLevelsService is not injected yet.");
            return;
        }

        var policy = new AddressablesUpdatePolicy(checkForUpdatesOnLaunch, updateCheckIntervalHours);
        _content.SetPolicy(policy);
        _content.OnStatus += Report;
        _content.OnProgress += p => OnProgress?.Invoke(p);
    }

    public LevelsData Init()
    {
        // Initialize CURRENT active mode's ordered view from already loaded data (if any)
        UpdateCurrentOrderedFromActive();
        return _levelsData ?? new LevelsData(new List<ChampionEntry>());
    }

    /// <summary>
    /// NEW: Discovery-only mode - discovers level IDs without downloading/loading assets.
    /// This is what Bootstrapper should call for fast startup.
    /// </summary>
    public async UniTask DiscoverLevelsOnlyAsync(bool forceReload = false)
    {
        if (_initializing) return;
        if (_initialized && !forceReload) return;

        _initializing = true;

        try
        {
            // Optional CCD URL patch
            Addressables.ResourceManager.InternalIdTransformFunc = loc =>
            {
                var id = loc.InternalId;
                const string needle = "entry_by_path/content/?path=/";
                if (id.Contains(needle))
                {
                    id = id.Replace(needle, "entry_by_path/content/?path=");
                    Debug.Log("[CCD URL patched] " + id);
                }
                return id;
            };

            OnPhaseChanged?.Invoke(LoadPhase.CheckingAssets);
            await _content.InitializeAsync(forceReload);

            if (forceReload)
            {
                _levelIdsByMode.Clear();
                _orderedLevelIdsByMode.Clear();
                _levelsByMode.Clear();
                _resumeIndexByMode.Clear();
                _loadedIds.Clear();
                OnProgress?.Invoke(0f);
            }

            // ===== Discover baseIds per mode (metadata only, no download/load) =====
            Report("Discovering levels...");

            foreach (var m in modes)
            {
                var baseIds = await _content.DiscoverBaseIdsByLabelAsync(m.label);
                _levelIdsByMode[m.key] = baseIds;
                Report($"Mode '{m.key}': discovered {baseIds.Count} levels.");

                // Derive display names for ordering (alphabetical)
                var displayNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in baseIds)
                {
                    var displayName = DeriveDisplayName(id);
                    displayNameToId[displayName] = id;
                }

                var orderedIds = displayNameToId.Keys
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .Select(k => displayNameToId[k])
                    .ToList();

                // Apply persistent order if it exists
                orderedIds = LevelOrder.ApplyPersistentOrderToIds(orderedIds);
                _orderedLevelIdsByMode[m.key] = orderedIds;
            }

            OnProgress?.Invoke(1f);
            OnLevelsReady?.Invoke();
            _initialized = true;

            Report("Level discovery complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[LevelsProvider] Discovery error: " + ex);
            Report("Error: " + ex.Message);
            throw;
        }
        finally { _initializing = false; }
    }

    /// <summary>
    /// NEW: Check if a level is already downloaded (cached on disk).
    /// </summary>
    public bool IsLevelDownloaded(string levelId, string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return false;
        
        if (_downloadedLevelIdsByMode.TryGetValue(k, out var downloaded))
        {
            return downloaded.Contains(levelId);
        }
        return false;
    }

    /// <summary>
    /// NEW: Download a level without loading it into memory (for prefetching).
    /// </summary>
    public UniTask PrefetchLevelAsync(string levelId, string modeKey = null)
    {
        return PrefetchLevelAsync(levelId, modeKey, null);
    }

    /// <summary>
    /// NEW: Download a level without loading it into memory (for prefetching).
    /// </summary>
    public async UniTask PrefetchLevelAsync(string levelId, string modeKey = null, IProgress<DownloadProgress> progress = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k) || string.IsNullOrEmpty(levelId))
        {
            Debug.LogWarning($"[PrefetchLevel] Invalid parameters: modeKey={k}, levelId={levelId}");
            return;
        }

        // Check if already downloaded
        if (IsLevelDownloaded(levelId, k))
        {
            Debug.Log($"[PrefetchLevel] Level {levelId} already downloaded, skipping.");
            return;
        }

        try
        {
            // Get the base key (strip slice suffix if any)
            var baseKey = StripSliceSuffix(levelId);
            
            // Download dependencies only (doesn't load into memory)
            await _content.DownloadDependenciesAsync(baseKey, progress);
            
            // Mark as downloaded
            if (!_downloadedLevelIdsByMode.TryGetValue(k, out var downloaded))
            {
                downloaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _downloadedLevelIdsByMode[k] = downloaded;
            }
            downloaded.Add(levelId);
            _downloadedLevelIds.Add(levelId);
            
            Debug.Log($"[PrefetchLevel] Successfully prefetched level: {DeriveDisplayName(levelId)}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PrefetchLevel] Failed to prefetch {levelId}: {ex.Message}");
        }
    }

    /// <summary>
    /// NEW: Prefetch N levels ahead starting from a given level ID.
    /// </summary>
    public async UniTask PrefetchLevelsAheadAsync(string startLevelId, int count, string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k) || count <= 0)
            return;

        if (!_orderedLevelIdsByMode.TryGetValue(k, out var ids) || ids == null || ids.Count == 0)
            return;

        // Find start index
        int startIndex = ids.FindIndex(id => string.Equals(id, startLevelId, StringComparison.OrdinalIgnoreCase));
        if (startIndex < 0) startIndex = 0;

        // Cancel any existing prefetch for this mode
        if (_prefetchCancellationTokens.TryGetValue(k, out var existingCts))
        {
            existingCts.Cancel();
            // Do NOT dispose here; the in-flight task will dispose its own CTS.
        }

        var cts = new CancellationTokenSource();
        _prefetchCancellationTokens[k] = cts;

        try
        {
            // Prefetch up to 'count' levels ahead
            int prefetched = 0;
            for (int i = 1; i <= count && (startIndex + i) < ids.Count; i++)
            {
                if (cts.Token.IsCancellationRequested)
                    break;

                var levelId = ids[startIndex + i];
                
                // Skip if already downloaded
                if (IsLevelDownloaded(levelId, k))
                    continue;

                // Skip solved levels (optional - you might want to prefetch them too)
                // if (GameProgress.Solved.Contains(levelId)) continue;

                await PrefetchLevelAsync(levelId, k);
                prefetched++;

                // Small delay to avoid overwhelming the network
                await UniTask.Delay(50, cancellationToken: cts.Token);
            }

            Debug.Log($"[PrefetchLevelsAhead] Prefetched {prefetched} level(s) ahead of {DeriveDisplayName(startLevelId)}");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[PrefetchLevelsAhead] Prefetch cancelled.");
        }
        finally
        {
            if (_prefetchCancellationTokens.TryGetValue(k, out var token) && token == cts)
            {
                _prefetchCancellationTokens.Remove(k);
                cts.Dispose();
            }
        }
    }

    private string StripSliceSuffix(string baseId)
    {
        if (string.IsNullOrEmpty(baseId)) return baseId;
        
        // Remove [Q], [W], [E], [R] suffixes if present
        int bracketIndex = baseId.IndexOf('[');
        if (bracketIndex >= 0)
            return baseId.Substring(0, bracketIndex);
        
        return baseId;
    }

    /// <summary>
    /// NEW: Load a single level (download + load asset). Shows loading progress only if not already downloaded.
    /// </summary>
    public async UniTask<ChampionEntry> LoadSingleLevelAsync(string levelId, string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k) || string.IsNullOrEmpty(levelId))
        {
            Debug.LogError($"[LoadSingleLevel] Invalid parameters: modeKey={k}, levelId={levelId}");
            return null;
        }

        // Find mode config
        var mode = modes.FirstOrDefault(m => string.Equals(m.key, k, StringComparison.OrdinalIgnoreCase));
        if (mode == null)
        {
            Debug.LogError($"[LoadSingleLevel] Mode '{k}' not found.");
            return null;
        }

        // Unload previous level if any
        UnloadCurrentLevel();

        bool alreadyDownloaded = IsLevelDownloaded(levelId, k);
        bool shouldShowDownloadProgress = !alreadyDownloaded || !hideLoadingUIForPrefetchedLevels;

        try
        {
            if (shouldShowDownloadProgress)
            {
                OnPhaseChanged?.Invoke(LoadPhase.DownloadAssets);
                Report($"Loading level: {DeriveDisplayName(levelId)}...");
            }

            // Use the new integrated load method with progress tracking
            IProgress<DownloadProgress> downloadProg = null;
            if (shouldShowDownloadProgress)
            {
                downloadProg = new Progress<DownloadProgress>(p =>
                {
                    OnPhaseChanged?.Invoke(LoadPhase.DownloadAssets);
                    OnDownloadProgress?.Invoke(p.DownloadedBytes, p.TotalBytes, p.Percent);
                });
            }

            IProgress<float> loadProg = null;
            if (shouldShowDownloadProgress)
            {
                loadProg = new Progress<float>(p =>
                {
                    OnPhaseChanged?.Invoke(LoadPhase.LoadLevels);
                    OnAssetsLoadProgress?.Invoke(0, 1, p);
                    Report($"Loading level: {DeriveDisplayName(levelId)}... {p:P0}");
                });
            }

            // Load the level asset (download + load combined with progress)
            // If already downloaded, the download phase will be instant
            ChampionEntry entry = await _content.LoadSingleLevelWithProgressAsync(
                levelId,
                mode.hasSlices,
                DeriveDisplayName,
                downloadProg,
                loadProg);

            entry = SanitizeEntry(entry);

            if (entry == null)
            {
                Debug.LogError($"[LoadSingleLevel] Failed to load level {levelId}");
                if (shouldShowDownloadProgress)
                    OnAssetsLoadProgress?.Invoke(1, 1, 1f);
                return null;
            }

            // Mark as downloaded if not already marked
            if (!IsLevelDownloaded(levelId, k))
            {
                if (!_downloadedLevelIdsByMode.TryGetValue(k, out var downloaded))
                {
                    downloaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _downloadedLevelIdsByMode[k] = downloaded;
                }
                downloaded.Add(levelId);
                _downloadedLevelIds.Add(levelId);
            }

            // Track as loaded
            _currentLoadedEntry = entry;
            _currentLoadedMode = k;
            ModeSessionStore.SaveLastCurrent(k, levelId);

            if (shouldShowDownloadProgress)
            {
                OnAssetsLoadProgress?.Invoke(1, 1, 1f);
                OnProgress?.Invoke(1f);
            }
            Report($"Loaded: {entry.displayName}");

            return entry;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoadSingleLevel] Error loading {levelId}: {ex}");
            Report($"Error loading level: {ex.Message}");
            _currentLoadedEntry = null;
            _currentLoadedMode = null;
            return null;
        }
    }

    /// <summary>
    /// NEW: Unload currently loaded level to free memory.
    /// </summary>
    public void UnloadCurrentLevel()
    {
        if (_currentLoadedEntry == null) return;

        // Release addressable assets
        try
        {
            if (_currentLoadedEntry.skills != null)
            {
                foreach (var sprite in _currentLoadedEntry.skills)
                {
                    if (sprite != null)
                        Addressables.Release(sprite);
                }
            }
            if (_currentLoadedEntry.portrait != null)
            {
                Addressables.Release(_currentLoadedEntry.portrait);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnloadCurrentLevel] Error releasing assets: {ex.Message}");
        }

        _currentLoadedEntry = null;
        _currentLoadedMode = null;
    }

    /// <summary>
    /// NEW: Get count of discovered levels for a mode.
    /// </summary>
    public int GetLevelCount(string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return 0;
        return _orderedLevelIdsByMode.TryGetValue(k, out var ids) ? ids.Count : 0;
    }

    /// <summary>
    /// NEW: Get ordered list of level IDs for a mode.
    /// </summary>
    public List<string> GetLevelIds(string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return new List<string>();
        return _orderedLevelIdsByMode.TryGetValue(k, out var ids) ? new List<string>(ids) : new List<string>();
    }

    /// <summary>
    /// NEW: Get next unsolved level ID for a mode.
    /// </summary>
    public string GetNextUnsolvedLevelId(string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return null;

        if (!_orderedLevelIdsByMode.TryGetValue(k, out var ids) || ids == null || ids.Count == 0)
            return null;

        // Check if we have a last current ID to resume from
        var lastId = ModeSessionStore.LoadLastCurrent(k);
        int startIndex = 0;

        if (!string.IsNullOrEmpty(lastId))
        {
            startIndex = ids.FindIndex(id => string.Equals(id, lastId, StringComparison.OrdinalIgnoreCase));
            if (startIndex < 0) startIndex = 0;
        }

        // Look for next unsolved from startIndex
        int n = ids.Count;
        for (int i = 0; i < n; i++)
        {
            int idx = (startIndex + i) % n;
            var id = ids[idx];
            if (!GameProgress.Solved.Contains(id))
                return id;
        }

        return null; // All solved
    }

    public async UniTask InitializeAndLoadAsync(bool forceReload = false)
    {
        if (_initializing) return;
        if (_initialized && !forceReload) return;

        _initializing = true;

        try
        {
            // Optional CCD URL patch
            Addressables.ResourceManager.InternalIdTransformFunc = loc =>
            {
                var id = loc.InternalId;
                const string needle = "entry_by_path/content/?path=/";
                if (id.Contains(needle))
                {
                    id = id.Replace(needle, "entry_by_path/content/?path=");
                    Debug.Log("[CCD URL patched] " + id);
                }
                return id;
            };

            OnPhaseChanged?.Invoke(LoadPhase.CheckingAssets);
            await _content.InitializeAsync(forceReload);

            if (forceReload)
            {
                _levelsByMode.Clear();
                _resumeIndexByMode.Clear();
                _loadedIds.Clear();
                OnProgress?.Invoke(0f);
            }

            // ===== 1) Discover baseIds per mode =====
            var labelToBaseIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in modes)
            {
                var list = await _content.DiscoverBaseIdsByLabelAsync(m.label);
                labelToBaseIds[m.label] = list;
                Report($"Mode '{m.key}': discovered {list.Count} items.");
            }

            // ===== 2) Aggregate download size and download all (optional) =====
            OnPhaseChanged?.Invoke(LoadPhase.DownloadAssets);

            long totalBytes = 0;
            var labelSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in modes)
            {
                long b = await _content.GetDownloadSizeAsync(m.label);
                labelSizes[m.label] = b;
                totalBytes += b;
            }

            long downloadedSoFar = 0;
            OnDownloadProgress?.Invoke(0, totalBytes, totalBytes > 0 ? 0f : 1f);

            if (downloadAllOnStart && totalBytes > 0)
            {
                foreach (var m in modes)
                {
                    var label = m.label;
                    var size = labelSizes[label];
                    if (size <= 0) continue;

                    Report($"Downloading {FormatBytes(size)} for mode '{m.key}'…");

                    var prog = new Progress<DownloadProgress>(p =>
                    {
                        long current = downloadedSoFar + p.DownloadedBytes;
                        float pct = totalBytes > 0 ? (float)current / totalBytes : 1f;
                        OnDownloadProgress?.Invoke(current, totalBytes, pct);
                        OnProgress?.Invoke(pct);
                    });

                    await _content.DownloadDependenciesAsync(label, prog);
                    downloadedSoFar += size;
                }
            }
            else
            {
                OnDownloadProgress?.Invoke(totalBytes, totalBytes, 1f);
                OnProgress?.Invoke(1f);
            }

            // ===== 3) Load entries per mode (with throttle) =====
            OnPhaseChanged?.Invoke(LoadPhase.LoadLevels);
            Report("Loading champions for all modes…");

            foreach (var m in modes)
            {
                var baseIds = labelToBaseIds[m.label];
                var modeLevels = new LevelsData(new List<ChampionEntry>(baseIds.Count));

                int done = 0;
                int total = baseIds.Count;
                var tasks = new List<UniTask>(Mathf.Max(total, 1));
                var gate = new SemaphoreSlim(12);

                foreach (var baseId in baseIds)
                {
                    await gate.WaitAsync();
                    tasks.Add(LoadOne(m, baseId, total, gate, modeLevels, () =>
                    {
                        done++;
                        float pct = total <= 0 ? 1f : Mathf.Clamp01(done / (float)total);
                        OnAssetsLoadProgress?.Invoke(done, total, pct);
                        OnProgress?.Invoke(pct);
                    }));
                }

                await UniTask.WhenAll(tasks);

                // Stable, alphabetical by displayName
                var ordered = modeLevels.Entries
                    .Where(e => e != null)
                    .OrderBy(e => e.displayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Apply your persistent order if you have one
                ordered = LevelOrder.ApplyPersistentOrder(ordered) ?? ordered;

                // Save per-mode
                var finalLevels = new LevelsData(ordered);
                _levelsByMode[m.key] = finalLevels;

                // Prefer explicit saved resume index if present; else fall back to lastId/solved
                var savedIdx = ModeSessionStore.LoadResumeIndex(m.key);
                int resumeIdx;
                if (savedIdx >= 0 && savedIdx < ordered.Count)
                {
                    resumeIdx = savedIdx;
                }
                else
                {
                    var lastId = ModeSessionStore.LoadLastCurrent(m.key);
                    resumeIdx = LevelOrder.ComputeResumeIndex(ordered, GameProgress.Solved, lastId);
                }
                _resumeIndexByMode[m.key] = resumeIdx;

                Report($"Mode '{m.key}': loaded {finalLevels.Entries.Count} entries.");
            }
            
            // ===== 4) Finalize without requiring an active mode =====
            bool anyModeHasEntries = _levelsByMode.Values.Any(v => v?.Entries != null && v.Entries.Count > 0);
            if (!anyModeHasEntries)
            {
                // Nothing loaded for any configured mode → this is a real error
                string modeList = string.Join(", ", modes.Select(m => $"'{m.key}' → {m.label}"));
                throw new Exception($"No levels loaded for any configured mode. Checked: {modeList}");
            }

            if (string.IsNullOrWhiteSpace(activeModeKey))
            {
                // No active mode yet → keep legacy properties empty/neutral.
                // You will call SetActiveMode(...) later, and that will publish into Levels/Ordered/ResumeIndex.
                _levelsData = new LevelsData(new List<ChampionEntry>());
                Ordered = _levelsData.Entries;
                ResumeIndex = 0;
            }
            else
            {
                // An active mode exists → publish it to legacy properties for older callers.
                UpdateCurrentOrderedFromActive();

                // If someone set an unknown key, fall back to empty but DON'T throw (you can choose later)
                if (_levelsData == null || _levelsData.Entries == null)
                {
                    _levelsData = new LevelsData(new List<ChampionEntry>());
                    Ordered = _levelsData.Entries;
                    ResumeIndex = 0;
                }
            }

            OnProgress?.Invoke(1f);
            OnLevelsReady?.Invoke();     // Signals: "all modes are loaded"
            _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[LevelsProvider] Init error: " + ex);
            Report("Error: " + ex.Message);
            throw;
        }
        finally { _initializing = false; }

        // -------- local helper --------
        async UniTask LoadOne(ModeConfig mode, string baseId, int totalCount, SemaphoreSlim g, LevelsData into, Action tick)
        {
            try
            {
                if (_loadedIds.Contains(baseId)) return;

                ChampionEntry entry = mode.hasSlices
                    ? await _content.LoadChampionEntryAsync(baseId, DeriveDisplayName)
                    : await _content.LoadPortraitAsChampionEntryAsync(baseId, DeriveDisplayName);

                entry = SanitizeEntry(entry); // <— IMPORTANT

                if (entry == null) return;

                lock (_loadedIds)
                {
                    if (_loadedIds.Add(entry.id))
                        into.Entries.Add(entry);
                }
            }
            finally
            {
                tick?.Invoke();
                g.Release();
            }
        }
    }

    // === NEW: mode management API ===
    public void SetActiveMode(string key)
    {
        if (string.IsNullOrWhiteSpace(key))        // ignore empty
            return;

        activeModeKey = key;
        UpdateCurrentOrderedFromActive();
    }

    public LevelsData GetLevels(string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        return _levelsByMode.TryGetValue(k, out var d) ? d : new LevelsData(new List<ChampionEntry>());
    }

    
    public int GetResumeIndex(string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return 0;
        return _resumeIndexByMode.TryGetValue(k, out var v) ? v : 0;
    }

    public void SaveResumeIndex(int resumeIndex, string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return; // nothing to do if no mode yet

        // clamp to available entries
        var count = _levelsByMode.TryGetValue(k, out var d) && d?.Entries != null ? d.Entries.Count : 0;
        var idx = Mathf.Clamp(resumeIndex, 0, Mathf.Max(0, count - 1));

        _resumeIndexByMode[k] = idx;
        ModeSessionStore.SaveResumeIndex(k, idx);

        if (string.Equals(k, activeModeKey, StringComparison.OrdinalIgnoreCase))
            ResumeIndex = idx;
    }

    public void SaveLastCurrent(string championId, string modeKey = null)
    {
        var k = string.IsNullOrWhiteSpace(modeKey) ? activeModeKey : modeKey;
        if (string.IsNullOrWhiteSpace(k)) return;
        
        ModeSessionStore.SaveLastCurrent(k, championId);
        
        // Also update resume index for backward compatibility (if we have ordered IDs)
        if (_orderedLevelIdsByMode.TryGetValue(k, out var ids) && ids != null)
        {
            int index = ids.FindIndex(id => string.Equals(id, championId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                SaveResumeIndex(index, k);
            }
        }
    }

    private void UpdateCurrentOrderedFromActive()
    {
        if (string.IsNullOrWhiteSpace(activeModeKey))
        {
            _levelsData = new LevelsData(new List<ChampionEntry>());
            Ordered = _levelsData.Entries;
            ResumeIndex = 0;
            return;
        }

        if (_levelsByMode.TryGetValue(activeModeKey, out var lv) && lv?.Entries != null)
        {
            _levelsData = lv;
            Ordered = lv.Entries;
            ResumeIndex = _resumeIndexByMode.TryGetValue(activeModeKey, out var idx) ? idx : 0;
        }
        else
        {
            _levelsData = new LevelsData(new List<ChampionEntry>());
            Ordered = _levelsData.Entries;
            ResumeIndex = 0;
        }
    }

    // --- UI / naming helpers ----
    private void Report(string msg) => OnStatus?.Invoke(msg);

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }

    private string DeriveDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;

        // 1) Strip folders
        string name = id;
        int slash = Mathf.Max(id.LastIndexOf('/'), id.LastIndexOf('\\'));
        if (slash >= 0) name = id[(slash + 1)..];

        // 2) Strip extension
        int lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
        {
            string ext = name.Substring(lastDot).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg") name = name.Substring(0, lastDot);
        }

        // 3) Normalize underscores/dashes to spaces
        name = name.Replace('_', ' ').Replace('-', ' ');

        // 4) Title-case
        var chars = name.ToCharArray();
        bool newWord = true;
        for (int i = 0; i < chars.Length; i++)
        {
            char ch = chars[i];
            if (char.IsLetter(ch))
            {
                chars[i] = newWord ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);
                newWord = false;
            }
            else newWord = (ch == ' ');
        }
        return new string(chars);
    }

    // Simple per-mode session persistence (swap to your SessionStore if it has mode support)
    private static class ModeSessionStore
    {
        public static string LoadLastCurrent(string modeKey) =>
            PlayerPrefs.GetString($"LoG.LastCurrent.{modeKey}", null);

        public static void SaveLastCurrent(string modeKey, string id)
        {
            PlayerPrefs.SetString($"LoG.LastCurrent.{modeKey}", id ?? "");
            PlayerPrefs.Save();
        }

        public static int LoadResumeIndex(string modeKey)
        {
            return PlayerPrefs.GetInt($"LoG.ResumeIndex.{modeKey}", -1); // -1 = not set yet
        }

        public static void SaveResumeIndex(string modeKey, int index)
        {
            PlayerPrefs.SetInt($"LoG.ResumeIndex.{modeKey}", index);
            PlayerPrefs.Save();
        }
    }
    
    private static ChampionEntry SanitizeEntry(ChampionEntry e)
    {
        if (e == null) return null;

        if (e.artKind == EntryArtKind.Portrait1)
        {
            // Ensure no accidental skill fill for portrait entries
            if (e.skills == null || e.skills.Length != 4) e.skills = new Sprite[4];
            else Array.Clear(e.skills, 0, 4);
        }
        else
        {
            // Skills4 entries should not carry portrait by mistake
            e.portrait = null;
        }

        return e;
    }
    
}
