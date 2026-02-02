using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  [AttributeUsage(AttributeTargets.Class, Inherited = false)]
  public sealed class SingletonMonoBehaviourAttribute : InjectableSingletonAttribute
  {
    /// <summary>
    /// If MonoBehaviour, should a new instance be created on a new GameObject or should it use an existing one in the scene?
    /// </summary>
    public bool CreateNewInstance { get; }

    /// <summary>
    /// The name of the GameObject if new one is created.
    /// </summary>
    public string GameObjectName { get; set; }

    /// <summary>
    /// Attribute to mark a MonoBehaviour as a singleton resource.
    /// </summary>
    /// <param name="loadPriority">The priority for injection ordering.</param>
    /// <param name="context">The context in which the singleton is used.</param>
    /// <param name="nonLazy">Whether to instantiate the singleton immediately (non-lazy).</param>
    /// <param name="extraBindings">Additional interfaces to bind to the singleton.</param>
    /// <param name="createNewInstance">If true, a new instance will be created on a new GameObject.</param>
    /// <param name="gameObjectName">The name of the GameObject if a new one is created.</param>
    public SingletonMonoBehaviourAttribute(
      int loadPriority,
      AppContextType context,
      bool createNewInstance,
      string gameObjectName,
      params Type[] extraBindings
    )
      : base(loadPriority, context, extraBindings)
    {
      CreateNewInstance = createNewInstance;
      GameObjectName = gameObjectName;
    }

    public override string ToString()
      => $"SingletonMonoBehaviourAttribute: " +
         $"{nameof(LoadPriority)}={LoadPriority}, " +
         $"{nameof(Context)}={Context}, " +
         $"{nameof(CreateNewInstance)}={CreateNewInstance}, " +
         $"{nameof(GameObjectName)}={GameObjectName}, " +
         $"{nameof(NonLazy)}={NonLazy}, " +
         $"{nameof(ExtraBindings)}={string.Join(", ", ExtraBindings)}";
  }
}