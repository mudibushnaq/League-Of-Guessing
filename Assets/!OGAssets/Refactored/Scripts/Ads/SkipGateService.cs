// Assets/!OGAssets/Refactored/Scripts/Ads/SkipGateService.cs

using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;

public enum SkipGate { Keys, Rewarded }

public interface ISkipGateService
{
    SkipGate GetGate(int currentKeys);
    void RecordSkip(); // call after a skip is actually performed
    int TotalSkips { get; }
}

/// Rules:
/// - If currentKeys == 0 => Rewarded
/// - Otherwise, alternate: after every 1 skip, the *next* is Rewarded (i.e., when TotalSkips is odd).
[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(ISkipGateService))]
public sealed class SkipGateService : ISkipGateService, IMenuInitializable
{
    int IMenuInitializable.Order => 0;
    
    const string PP_TOTAL_SKIPS = "TOTAL_SKIPS";
    int _total;

    public int TotalSkips => _total;

    UniTask IMenuInitializable.Initialize()
    {
        _total = PlayerPrefs.GetInt(PP_TOTAL_SKIPS, 0);
        Debug.Log("[IMenuInitializable.Initialize] SkipGateService ready.");
        return UniTask.CompletedTask;
    }
    
    public SkipGate GetGate(int currentKeys)
    {
        if (currentKeys <= 0) return SkipGate.Rewarded;
        // After every 1 skip, the next is rewarded → when total so far is odd (1,3,5…)
        return (_total % 2 == 1) ? SkipGate.Rewarded : SkipGate.Keys;
    }

    public void RecordSkip()
    {
        _total++;
        PlayerPrefs.SetInt(PP_TOTAL_SKIPS, _total);
        PlayerPrefs.Save();
    }
}