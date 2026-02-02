using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  public sealed class SingletonScriptableObjectAddressableAttribute : InjectableAddressableAttribute
  {
    /// <summary>
    /// Attribute to mark a ScriptableObject as a singleton resource.
    /// </summary>
    /// <param name="loadPriority">The priority for injection ordering.</param>
    /// <param name="context">The context in which the singleton is used.</param>
    /// <param name="address">The asset path to the ScriptableObject resource.</param>
    /// <param name="nonLazy">Whether to instantiate the singleton immediately (non-lazy).</param>
    /// <param name="extraBindings">Additional types to bind to the singleton.</param>
    public SingletonScriptableObjectAddressableAttribute(
      int loadPriority,
      AppContextType context,
      string address,
      params Type[] extraBindings)
      : base(loadPriority, context, address, extraBindings)
    {
    }

    public override string ToString()
      => $"SingletonScriptableObjectAddressableAttribute: " +
         $"{nameof(LoadPriority)}={LoadPriority}, " +
         $"{nameof(Address)}={Address}, " +
         $"{nameof(NonLazy)}={NonLazy}, " +
         $"{nameof(ExtraBindings)}={string.Join(", ", ExtraBindings)}";
  }
}