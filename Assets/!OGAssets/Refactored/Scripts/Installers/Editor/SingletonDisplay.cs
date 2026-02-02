using OG.Data;
using OG.Installers.Attributes;
using Sirenix.OdinInspector;

namespace OG.Installers
{
    [InlineProperty, HideReferenceObjectPicker, HideDuplicateReferenceBox]
    public abstract class SingletonDisplay
    {
        public abstract InjectableSingletonAttribute Attribute { get; }
    
        [ShowInInspector, DisplayAsString(EnableRichText =  true), PropertyOrder(-100), HideLabel]
        protected string name { get; private set; }
    
        [ShowInInspector, DisplayAsString]
        private int executionOrder;
    
        [ShowInInspector, DisplayAsString]
        public AppContextType Context { get; private set; }

        [ShowInInspector, DisplayAsString]
        public abstract SingletonTypes SingletonType { get; }

        public SingletonDisplay(AppContextType context, int index, string name)
        {
            Context = context;
            executionOrder = index + 1;
            this.name = $"<b>{name}</b>";
        }
    }
}