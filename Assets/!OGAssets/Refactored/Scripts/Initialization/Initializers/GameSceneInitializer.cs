using OG.Data;
using OG.Installers.Attributes;

namespace OG.Initialization.Initializers
{
    [SingletonMonoBehaviour(
        loadPriority: Priority.LOWEST,
        context: AppContextType.DefaultScene,
        createNewInstance: true,
        gameObjectName: "GameSceneInitializer")]
    internal sealed class GameSceneInitializer : InitializerBase
    {
        protected override AppContextType context => AppContextType.DefaultScene;
    }
    
    [SingletonMonoBehaviour(
        loadPriority: Priority.LOWEST,
        context: AppContextType.PortraitScene,
        createNewInstance: true,
        gameObjectName: "PortraitGameInitializer")]
    internal sealed class PortraitGameSceneInitializer : InitializerBase
    {
        protected override AppContextType context => AppContextType.PortraitScene;
    }
    
    [SingletonMonoBehaviour(
        loadPriority: Priority.LOWEST,
        context: AppContextType.LegacyScene,
        createNewInstance: true,
        gameObjectName: "LegacySceneInitializer")]
    internal sealed class LegacyGameSceneInitializer : InitializerBase
    {
        protected override AppContextType context => AppContextType.LegacyScene;
    }
}