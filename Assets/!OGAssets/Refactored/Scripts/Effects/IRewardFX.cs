// IRewardFX.cs
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IRewardFX
{
    void PlayGainFX(int qty, WalletType type, Func<UniTask> onCommittedAsync = null,
        RectTransform source = null, RectTransform iconPrefabOverride = null);
    UniTask PlayGainFXAsync(int qty, WalletType type, Action onCommitted = null,
        RectTransform source = null, RectTransform iconPrefabOverride = null);

    UniTask PlayAdvanced(RewardFxRequest req, Action onCommitted, Func<UniTask> onCommittedAsync);
    Transform Transform { get; }
    GameObject GameObject { get; }
}