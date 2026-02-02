// Assets/Scripts/Addressables/CcdPathConfig.cs
using UnityEngine;

[CreateAssetMenu(menuName = "LOG/Config/CCD Path Config")]
public class CcdPathConfig : ScriptableObject
{
    [Header("CCD request prefix to inject after ?path=/")]
    [Tooltip("Example: CCDBuildData/development/<bucket-guid>/latest")]
    public string pathPrefix = "";  // no leading slash required (we'll add it)

    [Header("Strip platform folder if present (/Android, /iOS, etc.)")]
    public bool stripPlatformFolder = true;

    [Header("Optional: log rewritten URLs")]
    public bool logUrls = false;
}