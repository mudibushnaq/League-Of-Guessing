using Cysharp.Threading.Tasks;

namespace OG.Initialization
{
    public interface IProjectInitializable 
    {
        int Order { get; }
        UniTask Initialize();
    }
}