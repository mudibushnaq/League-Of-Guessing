using OG.Data;
using OG.Installers.Attributes;

namespace OG.Installers
{
  [SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.MenuScene,
    createNewInstance: true,
    gameObjectName: "Menu Scene Container")]
  internal sealed class MenuContainer : ContainerContext
  {
    public override AppContextType Context
      => AppContextType.MenuScene;
  }
}