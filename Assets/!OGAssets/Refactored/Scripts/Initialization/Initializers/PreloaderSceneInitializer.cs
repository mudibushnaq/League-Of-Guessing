using OG.Data;
using OG.Installers.Attributes;
using Zenject;

namespace OG.Initialization.Initializers
{
    [SingletonMonoBehaviour(
        loadPriority: Priority.LOWEST,
        context: AppContextType.PreloaderScene,
        createNewInstance: true,
        gameObjectName: "Initializer")]
    internal sealed class PreloaderSceneInitializer : InitializerBase
    {
        protected override AppContextType context => AppContextType.PreloaderScene;

    }
}