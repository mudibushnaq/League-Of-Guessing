using OG.Data;
using OG.Installers.Attributes;

namespace OG.Installers
{
  [SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.PreloaderScene,
    createNewInstance: true,
    gameObjectName: "Preloader Scene Container")]
  internal sealed class PreloadContainer : ContainerContext
  {
    public override AppContextType Context
      => AppContextType.PreloaderScene;
  }
}