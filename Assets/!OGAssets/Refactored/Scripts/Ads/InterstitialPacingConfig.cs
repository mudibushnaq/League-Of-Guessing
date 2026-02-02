// Assets/!OGAssets/Refactored/Scripts/Ads/InterstitialPacingConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="InterstitialPacingConfig", menuName="LOG/Ads/Interstitial Pacing Config")]
public sealed class InterstitialPacingConfig : ScriptableObject
{
    [Tooltip("Global minimum seconds between any two interstitials (hard gate).")]
    public int globalMinSecondsBetweenShows = 45;

    [Tooltip("Per-event pacing rules.")]
    public List<Rule> rules = new();

    [Serializable]
    public struct Rule
    {
        [Tooltip("Event key you will Track(), e.g. 'login', 'menu_visit', 'level_solved'")]
        public string eventKey;

        [Tooltip("Show one interstitial each time the counter reaches a multiple of this. E.g., 5 => 5,10,15...")]
        public int threshold;

        [Tooltip("Minimum seconds after the last interstitial before this event may show another one.")]
        public int minCooldownSeconds;

        [Tooltip("Max number of interstitials this event may show per UTC day (0 = unlimited).")]
        public int perDayCap;
    }

    public bool TryGetRule(string key, out Rule r)
    {
        foreach (var x in rules) { if (string.Equals(x.eventKey, key, StringComparison.Ordinal)) { r = x; return true; } }
        r = default; return false;
    }
}