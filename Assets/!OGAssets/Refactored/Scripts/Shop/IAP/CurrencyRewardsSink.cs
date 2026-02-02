#nullable enable
using UnityEngine;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using Zenject;

/// Bridge between IAP grants → CurrencyStore updates + RewardFX animation.
[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(CurrencyRewardsSink),
    gameObjectName: nameof(CurrencyRewardsSink),
    extraBindings: typeof(IIapRewardsSink))]
public sealed class CurrencyRewardsSink : MonoBehaviour, IIapRewardsSink, IProjectInitializable
{
    int IProjectInitializable.Order => -150;
    
    [Header("FX (optional)")]
    [Inject] private IRewardFX _fx;            // drag your RewardFX here
    [SerializeField] private bool animateThenCommit = true; // commit after FX completes
    
    UniTask IProjectInitializable.Initialize()
    {
        CurrencyStore.InitIfNeeded();
        return UniTask.CompletedTask;
    }

    public async UniTask GrantLp(int amount, string reason)
    {
        if (amount <= 0) return;

        if (animateThenCommit)
        {
            // Commit AFTER the flight so label increments match the visual
            /*await _fx.PlayGainFXAsync(amount, WalletType.LP, onCommittedAsync: async () =>
            {
                CurrencyStore.AddLP(amount);
                await UniTask.CompletedTask;
            });*/
            
            await _fx.PlayGainFXAsync(
                amount,
                WalletType.LP,
                onCommitted: () => CurrencyStore.AddLP(amount));
        }
        else
        {
            // Commit immediately, optionally animate without waiting
            CurrencyStore.AddLP(amount);
            _fx.PlayGainFX(amount, WalletType.LP);
        }
    }

    public async UniTask GrantKeys(int amount, string reason)
    {
        if (amount <= 0) return;

        if (animateThenCommit)
        {
            _fx.PlayGainFX(amount, WalletType.Keys, onCommittedAsync: async () =>
            {
                CurrencyStore.AddKeys(amount);
                await UniTask.CompletedTask;
            });
        }
        else
        {
            CurrencyStore.AddKeys(amount);
            _fx.PlayGainFX(amount, WalletType.Keys);
        }
    }
}