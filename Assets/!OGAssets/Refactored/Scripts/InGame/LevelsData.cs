using System;
using System.Collections.Generic;
using OG.Data;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LevelsData
{
    [SerializeField] private List<ChampionEntry> entries;
    public List<ChampionEntry> Entries => entries;

    public LevelsData(List<ChampionEntry> entries)
    {
        this.entries = entries;
    }
}