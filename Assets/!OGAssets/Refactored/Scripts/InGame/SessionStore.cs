using UnityEngine;

public static class SessionStore
{
    private const string PP_LastCurrentId = "LoG_LastCurrentLevelId";

    public static void SaveLastCurrent(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        PlayerPrefs.SetString(PP_LastCurrentId, id);
        PlayerPrefs.Save();
    }

    public static string LoadLastCurrent()
    {
        return PlayerPrefs.GetString(PP_LastCurrentId, "");
    }

    public static void ClearLastCurrent()
    {
        PlayerPrefs.DeleteKey(PP_LastCurrentId);
    }
}