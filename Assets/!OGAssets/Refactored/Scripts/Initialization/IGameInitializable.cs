using Cysharp.Threading.Tasks;

namespace OG.Initialization
{
  public interface IGameInitializable 
  {
    int Order { get; }
    UniTask Initialize();
  }
  
  public interface IPortraitGameInitializable 
  {
    int Order { get; }
    UniTask Initialize();
  }
  
  public interface ILegacyGameInitializable 
  {
    int Order { get; }
    UniTask Initialize();
  }
}