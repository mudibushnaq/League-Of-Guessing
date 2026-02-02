using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  public sealed class SingletonPrefabAddressableAttribute : InjectableAddressableAttribute
  {
    /// <summary>
    /// The name of the GameObject that will be created from the prefab.
    /// </summary>
    public string GameObjectName { get; }


    /// <summary>
    /// Attribute to mark a Prefab as a singleton resource.
    /// </summary>
    /// <param name="loadPriority">The priority for injection ordering.</param>
    /// <param name="context">The context in which the singleton is used.</param>
    /// <param name="address">The asset path to the ScriptableObject resource.</param>
    /// <param name="nonLazy">Whether to instantiate the singleton immediately (non-lazy).</param>
    /// <param name="gameObjectName">The name of the GameObject that will be created from the prefab.</param>
    /// <param name="extraBindings">Additional types to bind to the singleton.</param>
    public SingletonPrefabAddressableAttribute(
      int loadPriority,
      AppContextType context,
      string address,
      string gameObjectName,
      params Type[] extraBindings)
      : base(loadPriority, context, address, extraBindings)
    {
      GameObjectName = gameObjectName;
    }

    public override string ToString()
      => $"SingletonPrefabAddressableAttribute: " +
         $"{nameof(LoadPriority)}={LoadPriority}, " +
         $"{nameof(Address)}={Address}, " +
         $"{nameof(GameObjectName)}={GameObjectName}, " +
         $"{nameof(NonLazy)}={NonLazy}, " +
         $"{nameof(ExtraBindings)}={string.Join(", ", ExtraBindings)}";
  }
}