using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OG.Data;

public interface IAddressablesLevelsService
{
    event Action<string> OnStatus;
    event Action<float> OnProgress;

    UniTask InitializeAsync(bool forceReload = false);
    UniTask<List<string>> DiscoverBaseIdsByLabelAsync(string label);
    UniTask<long> GetDownloadSizeAsync(object keyOrLabel);

    UniTask DownloadDependenciesAsync(object keyOrLabel, IProgress<DownloadProgress> progress = null);
    UniTask DownloadDependenciesAsync(object keyOrLabel, IProgress<float> progress);

    /// <summary>Loads the 4 skill sprites (Q/W/E/R) for a baseId.</summary>
    UniTask<ChampionEntry> LoadChampionEntryAsync(string baseId, Func<string, string> deriveDisplayName);

    /// <summary>
    /// NEW: Loads a single portrait sprite (no Q/W/E/R) and returns it as a ChampionEntry
    /// with skills[0] = portrait, others = null (so your UI can still consume it uniformly).
    /// </summary>
    UniTask<ChampionEntry> LoadPortraitAsChampionEntryAsync(string baseId, Func<string, string> deriveDisplayName);

    /// <summary>
    /// NEW: Loads a single level asset with progress reporting (download + load combined).
    /// Progress reports both download and load phases.
    /// </summary>
    UniTask<ChampionEntry> LoadSingleLevelWithProgressAsync(
        string baseId,
        bool hasSlices,
        Func<string, string> deriveDisplayName,
        IProgress<DownloadProgress> downloadProgress = null,
        IProgress<float> loadProgress = null);

    void SetPolicy(AddressablesUpdatePolicy policy);
}

public readonly struct AddressablesUpdatePolicy
{
    public readonly bool CheckOnLaunch;
    public readonly int IntervalHours;
    public AddressablesUpdatePolicy(bool checkOnLaunch, int intervalHours)
    {
        CheckOnLaunch = checkOnLaunch;
        IntervalHours = intervalHours;
    }
}