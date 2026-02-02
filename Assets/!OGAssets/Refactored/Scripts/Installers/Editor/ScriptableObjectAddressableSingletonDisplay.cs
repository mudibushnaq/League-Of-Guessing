using OG.Installers.Attributes;
using Sirenix.OdinInspector;

namespace OG.Installers
{
    public sealed class ScriptableObjectAddressableSingletonDisplay : SingletonDisplayBase<SingletonScriptableObjectAddressableAttribute>
    {
        public override SingletonTypes SingletonType => SingletonTypes.ScriptableObjectAddressable;

        [ShowInInspector, DisplayAsString]
        private string address;

        public ScriptableObjectAddressableSingletonDisplay(IInjectableSingleton injectable, int index)
            : base(injectable, index)
        {
            address = TypedAttribute?.Address ?? "UNKNOWN";
        }
    }
}