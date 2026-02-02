using OG.Installers.Attributes;
using Sirenix.OdinInspector;

namespace OG.Installers
{
    public class PrefabResourceSingletonDisplay : SingletonDisplayBase<SingletonPrefabResourceAttribute>
    {
        public override SingletonTypes SingletonType => SingletonTypes.PrefabResource;

        [ShowInInspector, DisplayAsString]
        private string assetPath;

        [ShowInInspector, DisplayAsString]
        private string gameObjectName;

        public PrefabResourceSingletonDisplay(IInjectableSingleton injectable, int index)
            : base(injectable, index)
        {
            assetPath = TypedAttribute?.AssetPath ?? "UNKNOWN";
            gameObjectName = TypedAttribute?.GameObjectName ?? "UNKNOWN";
        }
    }
}