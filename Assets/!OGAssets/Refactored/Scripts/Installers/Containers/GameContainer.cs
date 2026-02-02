using OG.Data;
using OG.Installers.Attributes;

namespace OG.Installers
{
  [SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.DefaultScene,
    createNewInstance: true,
    gameObjectName: "Game Scene Container")]
  internal sealed class GameContainer : ContainerContext
  {
    public override AppContextType Context
      => AppContextType.DefaultScene;
  }
  
  [SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.PortraitScene,
    createNewInstance: true,
    gameObjectName: "Portrait Game Scene Container")]
  internal sealed class PortraitGameContainer : ContainerContext
  {
    public override AppContextType Context
      => AppContextType.PortraitScene;
  }
  
  [SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.LegacyScene,
    createNewInstance: true,
    gameObjectName: "Legacy Game Scene Container")]
  internal sealed class LegacyGameContainer : ContainerContext
  {
    public override AppContextType Context
      => AppContextType.LegacyScene;
  }
}