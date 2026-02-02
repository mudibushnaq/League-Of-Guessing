using System;
using UnityEngine;

namespace OG.Data
{
    public enum EntryArtKind { Skills4, Portrait1 }

    [Serializable]
    public class ChampionEntry
    {
        public string id;
        public string displayName;
        public string normalizedName;

        // Always allocate exactly 4 slots
        public Sprite[] skills = new Sprite[4]; // items may be null

        // Single-image portrait
        public Sprite portrait;
        public EntryArtKind artKind;

        public void ClearSkills()
        {
            if (skills == null || skills.Length != 4) skills = new Sprite[4];
            else Array.Clear(skills, 0, 4);
        }
    }
}