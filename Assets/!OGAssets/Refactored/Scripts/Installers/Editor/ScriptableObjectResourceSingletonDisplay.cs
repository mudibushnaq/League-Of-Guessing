using OG.Installers.Attributes;
using Sirenix.OdinInspector;

namespace OG.Installers
{
    public class ScriptableObjectResourceSingletonDisplay : SingletonDisplayBase<SingletonScriptableObjectResourceAttribute>
    {
        public override SingletonTypes SingletonType => SingletonTypes.ScriptableObjectResource;

        [ShowInInspector, DisplayAsString]
        private string assetPath;

        public ScriptableObjectResourceSingletonDisplay(IInjectableSingleton injectable, int index)
            : base(injectable, index)
        {
            assetPath = TypedAttribute?.AssetPath ?? "UNKNOWN";
        }
    }
}