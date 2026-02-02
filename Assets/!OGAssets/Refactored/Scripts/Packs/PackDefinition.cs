// Assets/!OGAssets/Refactored/Scripts/Packs/PackDefinition.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum PackGateType
{
    None = 0,
    UnityAdsRewarded = 1,
    SocialRewards = 2
}

public enum RewardKind
{
    Keys,
    LP,
}

[Serializable]
public sealed class RewardSpec
{
    public RewardKind kind;
    public int amount = 1;

    [Tooltip("Optional payload (e.g., currency id or item id).")]
    public string payload;
}

[CreateAssetMenu(menuName = "LOG/Config/Packs/PackDefinition")]
public sealed class PackDefinition : ScriptableObject
{
    [Header("Identity")]
    public string packId = "free_pack_5m";       // unique id
    public string displayName = "Lucky Pack";

    [Header("Gate")]
    public PackGateType gate = PackGateType.UnityAdsRewarded;

    [Header("Limits")]
    public int cooldownSeconds = 300;            // 5 minutes
    public int maxClaimsPerDay = 0;              // 0 = unlimited

    [Header("Rewards")]
    public List<RewardSpec> rewards = new();

    [Header("FX")]
    public bool playFx = true;                   // let your granter trigger FX
}