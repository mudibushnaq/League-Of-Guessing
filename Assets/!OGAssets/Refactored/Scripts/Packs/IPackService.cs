// Assets/!OGAssets/Refactored/Scripts/Packs/IPackService.cs
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IPackService
{
    bool CanClaim(PackDefinition def, out TimeSpan remaining, out int claimedToday);
    TimeSpan GetRemaining(PackDefinition def);
    int GetClaimsToday(PackDefinition def);

    /// Claim the pack (handles ad gate if needed). Returns true when rewards were granted.
    UniTask<bool> ClaimAsync(PackDefinition def, Transform source = null);
}