namespace OG.Installers
{
    public enum SingletonTypes
    {
        Everything = 0,
        Class = 1 << 0,
        MonoBehaviour = 1 << 1,
        PrefabResource = 1 << 2,
        ScriptableObjectResource = 1 << 3,
        PrefabAddressable = 1 << 4,
        ScriptableObjectAddressable = 1 << 5,
    }
}