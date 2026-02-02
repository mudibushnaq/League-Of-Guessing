// Assets/!OGAssets/Refactored/Scripts/Packs/IRewardGranter.cs
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IRewardGranter
{
    UniTask GrantAsync(IReadOnlyList<RewardSpec> rewards, Transform source = null, CancellationToken ct = default);
}