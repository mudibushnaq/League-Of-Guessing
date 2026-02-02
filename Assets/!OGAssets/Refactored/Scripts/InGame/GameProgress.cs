using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameProgress
{
    private const string SolvedKey = "LoG_Solved_IDs";
    private static HashSet<string> _solved;

    public static HashSet<string> Solved
    {
        get
        {
            if (_solved == null)
            {
                var json = PlayerPrefs.GetString(SolvedKey, "");
                _solved = string.IsNullOrEmpty(json)
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(JsonUtility.FromJson<StringList>(json).values, StringComparer.OrdinalIgnoreCase);
            }
            return _solved;
        }
    }

    public static void MarkSolved(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return;
        if (Solved.Add(levelId)) Save();
    }

    public static void ResetAll()
    {
        _solved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Save();
    }

    private static void Save()
    {
        var list = new StringList { values = new List<string>(Solved) };
        PlayerPrefs.SetString(SolvedKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    [Serializable] private class StringList { public List<string> values; }
}