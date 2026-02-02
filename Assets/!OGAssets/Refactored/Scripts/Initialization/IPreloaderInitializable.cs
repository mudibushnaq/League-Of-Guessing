using Cysharp.Threading.Tasks;

namespace OG.Initialization
{
  public interface IPreloaderInitializable
  {
    int Order { get; }
    UniTask Initialize();
  }
}