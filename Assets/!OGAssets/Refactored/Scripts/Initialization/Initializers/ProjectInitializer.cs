using OG.Data;
using OG.Installers.Attributes;
using Zenject;

namespace OG.Initialization.Initializers
{
    [SingletonMonoBehaviour(
        loadPriority: Priority.LOWEST,
        context: AppContextType.Project,
        createNewInstance: true,
        gameObjectName: "Initializer")]
    internal sealed class ProjectInitializer : InitializerBase
    {
        protected override AppContextType context => AppContextType.Project;
    }
}