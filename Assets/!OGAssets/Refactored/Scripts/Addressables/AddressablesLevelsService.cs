// AddressablesLevelsService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(IAddressablesLevelsService)
)]
public sealed class AddressablesLevelsService : IAddressablesLevelsService, IProjectInitializable
{
    int IProjectInitializable.Order => -100;

    public event Action<string> OnStatus;
    public event Action<float> OnProgress;

    private const string PP_LastCatalogCheck = "LoG_LastCatalogCheckUtc";
    private AddressablesUpdatePolicy _policy;
    private bool _initialized;

    UniTask IProjectInitializable.Initialize()
    {
        Debug.Log("[IProjectInitializable.Initialize] AddressablesLevelsService ready.");
        return UniTask.CompletedTask;
    }

    public void SetPolicy(AddressablesUpdatePolicy policy) => _policy = policy;

    public async UniTask InitializeAsync(bool forceReload = false)
    {
        if (_initialized && !forceReload) return;

        await RetryUI.RunStepWithRetry("Initialize Addressables", async () =>
        {
            Report("Initializing Addressables…");
            await Addressables.InitializeAsync().ToUniTask();
        }, Report);

        if (ShouldCheckForUpdates())
        {
            List<string> catalogs = null;
            await RetryUI.RunStepWithRetry("CheckForCatalogUpdates", async () =>
            {
                Report("Checking for catalog updates…");
                catalogs = await Addressables.CheckForCatalogUpdates(false).ToUniTask();
            }, Report);

            if (catalogs is { Count: > 0 })
            {
                await RetryUI.RunStepWithRetry($"UpdateCatalogs ({catalogs.Count})", async () =>
                {
                    var h = Addressables.UpdateCatalogs(catalogs, false);
                    try
                    {
                        Report("Updating catalogs… 0%");
                        while (!h.IsDone)
                        {
                            OnProgress?.Invoke(h.PercentComplete);
                            Report($"Updating catalogs… {(int)(h.PercentComplete * 100f)}%");
                            await UniTask.Yield();
                        }
                        await h.Task;
                        Report("Catalogs updated.");
                        OnProgress?.Invoke(1f);
                    }
                    finally { if (h.IsValid()) Addressables.Release(h); }
                }, Report);
            }
            else
            {
                Report("Catalog is up to date.");
                OnProgress?.Invoke(1f);
            }
            MarkCatalogChecked();
        }

        _initialized = true;
    }

    public async UniTask<List<string>> DiscoverBaseIdsByLabelAsync(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label must be non-empty.", nameof(label));

        IList<IResourceLocation> locations = null;

        await RetryUI.RunStepWithRetry($"Discover label: {label}", async () =>
        {
            Report($"Discovering items with label '{label}'…");
            locations = await Addressables.LoadResourceLocationsAsync(label).ToUniTask();
            if (locations == null || locations.Count == 0)
                throw new InvalidOperationException($"No assets found for label '{label}'.");
        }, Report);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loc in locations)
        {
            var rawKey = loc.PrimaryKey ?? loc.InternalId ?? Guid.NewGuid().ToString();
            set.Add(StripSliceSuffix(rawKey)); // if it's "X.png[Q]" → "X.png"; otherwise unchanged
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AddrSvc] Label '{label}': {locations.Count} locations → {set.Count} baseIds.");
#endif
        return set.ToList();
    }

    public async UniTask<long> GetDownloadSizeAsync(object keyOrLabel)
    {
        long bytes = 0;
        await RetryUI.RunStepWithRetry("GetDownloadSize", async () =>
        {
            bytes = await Addressables.GetDownloadSizeAsync(keyOrLabel).ToUniTask();
        }, Report);
        return bytes;
    }

    public async UniTask DownloadDependenciesAsync(object keyOrLabel, IProgress<DownloadProgress> progress = null)
    {
        await RetryUI.RunStepWithRetry("DownloadDependencies", async () =>
        {
            var h = Addressables.DownloadDependenciesAsync(keyOrLabel, false);
            try
            {
                while (!h.IsDone)
                {
                    var st = h.GetDownloadStatus();
                    var pct = Mathf.Clamp01(h.PercentComplete);
                    progress?.Report(new DownloadProgress(st.DownloadedBytes, st.TotalBytes, pct));
                    OnProgress?.Invoke(pct);
                    await UniTask.Yield();
                }
                await h.Task;

                var final = h.GetDownloadStatus();
                progress?.Report(new DownloadProgress(final.DownloadedBytes, final.TotalBytes, 1f));
                OnProgress?.Invoke(1f);

                if (h.Status != AsyncOperationStatus.Succeeded)
                    throw h.OperationException ?? new Exception("DownloadDependencies failed.");
            }
            finally { if (h.IsValid()) Addressables.Release(h); }
        }, Report);
    }

    public async UniTask DownloadDependenciesAsync(object keyOrLabel, IProgress<float> progress)
    {
        var adapter = (progress == null)
            ? null
            : new Progress<DownloadProgress>(p => progress.Report(p.Percent));
        await DownloadDependenciesAsync(keyOrLabel, adapter);
    }

    public async UniTask<ChampionEntry> LoadChampionEntryAsync(string baseId, Func<string, string> deriveDisplayName)
    {
        if (string.IsNullOrEmpty(baseId)) return null;
        var baseKey = StripSliceSuffix(baseId);

        async UniTask<Sprite> Slice(string letter)
        {
            var h = Addressables.LoadAssetAsync<Sprite>($"{baseKey}[{letter}]");
            await h.ToUniTask();
            return h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;
        }

        var (q, w, e, r) = await UniTask.WhenAll(Slice("Q"), Slice("W"), Slice("E"), Slice("R"));
        if (q == null && w == null && e == null && r == null) return null;

        var displayName = deriveDisplayName?.Invoke(baseKey) ?? baseKey;
        return new ChampionEntry
        {
            id = baseKey,
            displayName = displayName,
            normalizedName = StringUtils.NormalizeAnswer(displayName),
            skills = new[] { q, w, e, r }
        };
    }

    // === NEW: portrait-only loader (no Q/W/E/R) ===
    public async UniTask<ChampionEntry> LoadPortraitAsChampionEntryAsync(
        string baseId,
        Func<string, string> deriveDisplayName)
    {
        if (string.IsNullOrEmpty(baseId)) return null;

        // Load the sprite (your existing logic)
        Sprite portrait = null;
        var h1 = Addressables.LoadAssetAsync<Sprite>(baseId);
        await h1.Task;
        if (h1.Status == AsyncOperationStatus.Succeeded)
        {
            portrait = h1.Result;
        }
        else
        {
            Addressables.Release(h1);
            var h2 = Addressables.LoadAssetAsync<Sprite>($"{baseId}.png");
            await h2.Task;
            if (h2.Status == AsyncOperationStatus.Succeeded)
            {
                portrait = h2.Result;
            }
            else
            {
                Addressables.Release(h2);
                return null;
            }
        }

        // >>> IMPORTANT: normalize exactly like the QWER path
        var displayName   = deriveDisplayName?.Invoke(baseId) ?? baseId;
        var normalized    = StringUtils.NormalizeAnswer(displayName);

        return new ChampionEntry
        {
            id            = baseId,
            displayName   = displayName,
            normalizedName= normalized,          // <-- use NormalizeAnswer
            artKind       = EntryArtKind.Portrait1,
            portrait      = portrait,
            skills        = new Sprite[4]        // keep empty for portrait mode
        };
    }

    /// <summary>
    /// NEW: Load a single level with integrated download and load progress reporting.
    /// This method combines download and load into one operation with proper progress tracking.
    /// </summary>
    public async UniTask<ChampionEntry> LoadSingleLevelWithProgressAsync(
        string baseId,
        bool hasSlices,
        Func<string, string> deriveDisplayName,
        IProgress<DownloadProgress> downloadProgress = null,
        IProgress<float> loadProgress = null)
    {
        if (string.IsNullOrEmpty(baseId)) return null;

        // Step 1: Download dependencies (if needed) with progress
        // NOTE: With "Pack Together" bundle mode, this will download the entire bundle.
        // To truly load one asset at a time, change Addressables settings to "Pack Separately".
        var baseKey = StripSliceSuffix(baseId);
        var downloadHandle = Addressables.DownloadDependenciesAsync(baseKey, false);
        
        try
        {
            // Report download progress
            while (!downloadHandle.IsDone)
            {
                var status = downloadHandle.GetDownloadStatus();
                var percent = Mathf.Clamp01(downloadHandle.PercentComplete);
                downloadProgress?.Report(new DownloadProgress(status.DownloadedBytes, status.TotalBytes, percent));
                OnProgress?.Invoke(percent * 0.7f); // 70% for download phase
                await UniTask.Yield();
            }
            await downloadHandle.Task;
            
            var finalStatus = downloadHandle.GetDownloadStatus();
            downloadProgress?.Report(new DownloadProgress(finalStatus.DownloadedBytes, finalStatus.TotalBytes, 1f));
            
            if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
                throw downloadHandle.OperationException ?? new Exception($"Failed to download dependencies for {baseId}");
        }
        finally
        {
            if (downloadHandle.IsValid())
                Addressables.Release(downloadHandle);
        }

        // Step 2: Load the actual asset(s) with progress
        OnProgress?.Invoke(0.7f);
        loadProgress?.Report(0f);

        ChampionEntry entry;
        if (hasSlices)
        {
            // Load Q/W/E/R slices
            async UniTask<Sprite> Slice(string letter, float progressStart, float progressEnd)
            {
                var h = Addressables.LoadAssetAsync<Sprite>($"{baseKey}[{letter}]");
                while (!h.IsDone)
                {
                    loadProgress?.Report(Mathf.Lerp(progressStart, progressEnd, h.PercentComplete));
                    await UniTask.Yield();
                }
                await h.Task;
                return h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;
            }

            var (q, w, e, r) = await UniTask.WhenAll(
                Slice("Q", 0.0f, 0.25f),
                Slice("W", 0.25f, 0.5f),
                Slice("E", 0.5f, 0.75f),
                Slice("R", 0.75f, 1.0f)
            );

            loadProgress?.Report(1f);
            OnProgress?.Invoke(1f);

            if (q == null && w == null && e == null && r == null) return null;

            var displayName = deriveDisplayName?.Invoke(baseKey) ?? baseKey;
            entry = new ChampionEntry
            {
                id = baseKey,
                displayName = displayName,
                normalizedName = StringUtils.NormalizeAnswer(displayName),
                skills = new[] { q, w, e, r }
            };
        }
        else
        {
            // Load portrait
            Sprite portrait = null;
            var h1 = Addressables.LoadAssetAsync<Sprite>(baseId);
            while (!h1.IsDone)
            {
                loadProgress?.Report(h1.PercentComplete * 0.5f);
                OnProgress?.Invoke(0.7f + (h1.PercentComplete * 0.15f));
                await UniTask.Yield();
            }
            await h1.Task;

            if (h1.Status == AsyncOperationStatus.Succeeded)
            {
                portrait = h1.Result;
            }
            else
            {
                Addressables.Release(h1);
                var h2 = Addressables.LoadAssetAsync<Sprite>($"{baseId}.png");
                while (!h2.IsDone)
                {
                    loadProgress?.Report(0.5f + (h2.PercentComplete * 0.5f));
                    OnProgress?.Invoke(0.85f + (h2.PercentComplete * 0.15f));
                    await UniTask.Yield();
                }
                await h2.Task;
                if (h2.Status == AsyncOperationStatus.Succeeded)
                {
                    portrait = h2.Result;
                }
                else
                {
                    Addressables.Release(h2);
                    return null;
                }
            }

            loadProgress?.Report(1f);
            OnProgress?.Invoke(1f);

            var displayName = deriveDisplayName?.Invoke(baseId) ?? baseId;
            entry = new ChampionEntry
            {
                id = baseId,
                displayName = displayName,
                normalizedName = StringUtils.NormalizeAnswer(displayName),
                artKind = EntryArtKind.Portrait1,
                portrait = portrait,
                skills = new Sprite[4]
            };
        }

        return entry;
    }

    // ---------- helpers ----------
    private void Report(string msg) { Debug.Log($"[Report] {msg}"); OnStatus?.Invoke(msg); }

    private bool ShouldCheckForUpdates()
    {
        if (!_policy.CheckOnLaunch) return false;
        var last = PlayerPrefs.GetString(PP_LastCatalogCheck, "");
        if (string.IsNullOrEmpty(last)) return true;
        if (!DateTime.TryParse(last, out var when)) return true;
        var elapsed = DateTime.UtcNow - when.ToUniversalTime();
        return elapsed.TotalHours >= _policy.IntervalHours;
    }

    private void MarkCatalogChecked()
    {
        PlayerPrefs.SetString(PP_LastCatalogCheck, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
    }

    private static string StripSliceSuffix(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        int i = key.LastIndexOf('[');
        if (i >= 0 && key.EndsWith("]"))
        {
            if (key.Length - i - 2 == 1)
            {
                char c = key[i + 1];
                if (c == 'Q' || c == 'W' || c == 'E' || c == 'R')
                    return key.Substring(0, i);
            }
        }
        return key;
    }
}
