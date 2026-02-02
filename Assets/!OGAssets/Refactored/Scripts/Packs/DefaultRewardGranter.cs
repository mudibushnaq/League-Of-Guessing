// Assets/!OGAssets/Refactored/Scripts/Packs/DefaultRewardGranter.cs
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(IRewardGranter))]
public sealed class DefaultRewardGranter : IRewardGranter, IProjectInitializable
{
    int IProjectInitializable.Order => 50;
    
    [Inject] private IRewardFX _fx;  // your VFX service (optional)

    UniTask IProjectInitializable.Initialize()
    {
        //await InitializeAsync();
        Debug.Log("[IProjectInitializable.Initialize] DefaultRewardGranter ready.");
        return UniTask.CompletedTask;
    }
    
    public async UniTask GrantAsync(IReadOnlyList<RewardSpec> rewards, Transform source = null, CancellationToken ct = default)
    {
        foreach (var r in rewards)
        {
            switch (r.kind)
            {
                case RewardKind.Keys:
                    CurrencyStore.AddKeys(r.amount); // <-- your wallet call
                    break;
                case RewardKind.LP:
                    CurrencyStore.AddLP(r.amount);
                    break;
            }
            
            // optional FX
            if (_fx != null)
            {
                if (r.kind == RewardKind.Keys)
                {
                    // If you have async FX: await _fx.PlayGainFXAsync(...);
                    await _fx.PlayGainFXAsync(r.amount, WalletType.Keys, () =>
                    {
                    });
                    await UniTask.Yield();
                }
                else
                {
                    // If you have async FX: await _fx.PlayGainFXAsync(...);
                    await _fx.PlayGainFXAsync(r.amount, WalletType.LP, () =>
                    {
                    });
                    await UniTask.Yield();
                }
            }
        }
    }
}