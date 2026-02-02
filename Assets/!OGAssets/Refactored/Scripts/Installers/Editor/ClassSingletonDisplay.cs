using OG.Installers.Attributes;

namespace OG.Installers
{
    public class ClassSingletonDisplay : SingletonDisplayBase<SingletonClassAttribute>
    {
        public override SingletonTypes SingletonType => SingletonTypes.Class;

        public ClassSingletonDisplay(IInjectableSingleton injectable, int index)
            : base(injectable, index)
        {
        }
    }
}