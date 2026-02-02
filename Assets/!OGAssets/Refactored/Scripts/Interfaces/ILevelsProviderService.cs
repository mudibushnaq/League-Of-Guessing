using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OG.Data;

public interface ILevelsProviderService
{
    // Signals
    event Action<string> OnStatus;
    event Action<float> OnProgress;
    event Action<LoadPhase> OnPhaseChanged;
    event Action<int, int, float> OnAssetsLoadProgress;
    event Action<long, long, float> OnDownloadProgress;
    event Action OnLevelsReady;

    // Data (for the CURRENT active mode)
    List<ChampionEntry> Ordered { get; }
    LevelsData Levels { get; }
    int ResumeIndex { get; }

    // Lifecycle
    LevelsData Init();
    UniTask InitializeAndLoadAsync(bool forceReload = false);

    // ---- NEW: multi-mode support ----
    bool Initialized { get; }
    string ActiveModeKey { get; }
    void SetActiveMode(string key);
    LevelsData GetLevels(string modeKey = null);
    int GetResumeIndex(string modeKey = null);
    void SaveResumeIndex(int resumeIndex, string modeKey = null);
    void SaveLastCurrent(string championId, string modeKey = null);

    // ---- NEW: lazy loading support ----
    /// <summary>Discover level IDs only (no download/load). Fast startup.</summary>
    UniTask DiscoverLevelsOnlyAsync(bool forceReload = false);
    
    /// <summary>Load a single level (download + load asset).</summary>
    UniTask<ChampionEntry> LoadSingleLevelAsync(string levelId, string modeKey = null);
    
    /// <summary>Unload currently loaded level to free memory.</summary>
    void UnloadCurrentLevel();
    
    /// <summary>Get count of discovered levels for a mode.</summary>
    int GetLevelCount(string modeKey = null);
    
    /// <summary>Get ordered list of level IDs for a mode.</summary>
    List<string> GetLevelIds(string modeKey = null);
    
    /// <summary>Get next unsolved level ID for a mode.</summary>
    string GetNextUnsolvedLevelId(string modeKey = null);
    
    /// <summary>Check if a level is already downloaded (cached on disk).</summary>
    bool IsLevelDownloaded(string levelId, string modeKey = null);
    
    /// <summary>Download a level without loading it into memory (for prefetching).</summary>
    UniTask PrefetchLevelAsync(string levelId, string modeKey = null);
    
    /// <summary>Prefetch N levels ahead starting from a given level ID.</summary>
    UniTask PrefetchLevelsAheadAsync(string startLevelId, int count, string modeKey = null);

    // ---- Prefetch settings (read-only) ----
    bool hideLoadingUIForPrefetchedLevels { get; }
    int prefetchAheadCount { get; }
}