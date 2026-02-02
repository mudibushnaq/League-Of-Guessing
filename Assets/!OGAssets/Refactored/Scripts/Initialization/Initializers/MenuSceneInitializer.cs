using OG.Data;
using OG.Installers.Attributes;
using Zenject;

namespace OG.Initialization.Initializers
{
    [SingletonMonoBehaviour(
        loadPriority: Priority.LOWEST,
        context: AppContextType.MenuScene,
        createNewInstance: true,
        gameObjectName: "Initializer")]
    internal sealed class MenuSceneInitializer : InitializerBase
    {
        protected override AppContextType context => AppContextType.MenuScene;
    }
}