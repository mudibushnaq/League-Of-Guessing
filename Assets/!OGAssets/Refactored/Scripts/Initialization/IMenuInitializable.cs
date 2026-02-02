using Cysharp.Threading.Tasks;

namespace OG.Initialization
{
  public interface IMenuInitializable
  {
    int Order { get; }
    UniTask Initialize();
  }
}