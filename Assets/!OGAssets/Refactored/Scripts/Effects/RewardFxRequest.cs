using TMPro;
using UnityEngine;

public struct RewardFxRequest
{
    public RectTransform IconPrefab;      // the coin/key icon prefab (UI)
    public RectTransform IconsParent;     // parent layer where icons spawn (usually an overlay canvas)
    public RectTransform TargetIcon;      // where to fly to (e.g., top-bar icon)
    public TMP_Text      TargetLabel;     // the counter label to tick
    public int           Amount;          // how much to add (positive)
    public RectTransform Source;          // where to explode from (any UI element)
    
    // Optional: captured world positions (useful if Source/Target disappear)
    public Vector3?      SourceWorld;    // if set, used instead of Source
    public Vector3?      TargetWorld;    // rarely needed; default is TargetIcon center
    
    // Optional overrides (fall back to RewardFX serialized fields if null/0)
    public SfxId TickClip;
    public SfxId SpawnClip;
    public SfxId WhooshClip;
    
    public float?        FlyTime;
    public float?        PerIconStagger;
    
    // NEW (optional): prefer chunks of about this size for +N/arrivals (e.g., 500)
    // We'll clamp to maxIcons and keep per-arrival values consistent with icon flights.
    public int?          PreferredChunkSize;
}