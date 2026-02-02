// Assets/!OGAssets/Refactored/Scripts/Ads/UnlockGateService.cs

using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.Purchasing;

public enum UnlockGate { Keys, Rewarded }

public interface IUnlockGateService
{
    /// Decides whether the *next* unlock action is pay-with-keys or rewarded ad.
    UnlockGate GetGate(int currentKeys);

    /// Must be called after a successful unlock (keys or ad) to advance the milestone counter.
    void RecordUnlock();

    int TotalUnlocks { get; }
}

/// Rules:
/// - If user has 0 keys -> Rewarded
/// - Otherwise, every 4th unlock (3,7,11,...) is Rewarded
[SingletonClass(
    loadPriority: Priority.LOWEST,
    context: AppContextType.Project,
    extraBindings: typeof(IUnlockGateService))]
public sealed class UnlockGateService : IUnlockGateService, IProjectInitializable
{
    int IProjectInitializable.Order => -100;
    
    const string PP_TOTAL_UNLOCKS = "TOTAL_UNLOCKS"; // lifetime or profile-based tally

    int _total;
    
    UniTask IProjectInitializable.Initialize()
    {
        _total = PlayerPrefs.GetInt(PP_TOTAL_UNLOCKS, 0);
        Debug.Log("[IProjectInitializable.Initialize] UnlockGateService ready.");
        return UniTask.CompletedTask;
    }

    public int TotalUnlocks => _total;

    public UnlockGate GetGate(int currentKeys)
    {
        // keep this if you still want "no keys => ad" regardless of cycle
        if (currentKeys <= 0) 
            return UnlockGate.Rewarded;

        const int cycle = 6; // after 6 key unlocks, next (7th) is rewarded
        bool gate = (_total > 0) && (_total % cycle == 0);
        return gate ? UnlockGate.Rewarded : UnlockGate.Keys;
    }

    public void RecordUnlock()
    {
        _total++;
        PlayerPrefs.SetInt(PP_TOTAL_UNLOCKS, _total);
        PlayerPrefs.Save();
    }
}