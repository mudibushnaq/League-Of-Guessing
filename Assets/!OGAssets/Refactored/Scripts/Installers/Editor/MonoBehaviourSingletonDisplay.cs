using OG.Installers.Attributes;
using Sirenix.OdinInspector;

namespace OG.Installers
{
    public class MonoBehaviourSingletonDisplay : SingletonDisplayBase<SingletonMonoBehaviourAttribute>
    {
        public override SingletonTypes SingletonType => SingletonTypes.MonoBehaviour;

        [ShowInInspector, DisplayAsString]
        private bool createNewInstance;

        [ShowInInspector, DisplayAsString]
        private string gameObjectName;

        public MonoBehaviourSingletonDisplay(IInjectableSingleton injectable, int index)
            : base(injectable, index)
        {
            createNewInstance = TypedAttribute?.CreateNewInstance ?? false;
            gameObjectName = TypedAttribute?.GameObjectName ?? "UNKNOWN";
        }
    }
}