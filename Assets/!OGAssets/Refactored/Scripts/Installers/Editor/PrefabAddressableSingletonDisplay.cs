using OG.Installers.Attributes;
using Sirenix.OdinInspector;

namespace OG.Installers
{
    public class PrefabAddressableSingletonDisplay : SingletonDisplayBase<SingletonPrefabAddressableAttribute>
    {
        public override SingletonTypes SingletonType => SingletonTypes.PrefabAddressable;

        [ShowInInspector, DisplayAsString]
        private string address;

        [ShowInInspector, DisplayAsString]
        private string gameObjectName;

        public PrefabAddressableSingletonDisplay(IInjectableSingleton injectable, int index)
            : base(injectable, index)
        {
            address = TypedAttribute?.Address ?? "UNKNOWN";
            gameObjectName = TypedAttribute?.GameObjectName ?? "UNKNOWN";
        }
    }
}