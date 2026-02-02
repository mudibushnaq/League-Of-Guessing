using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  [AttributeUsage(AttributeTargets.Class, Inherited = false)]
  public sealed class SingletonScriptableObjectResourceAttribute : InjectableResourceAttribute
  {
    /// <summary>
    /// Attribute to mark a ScriptableObject as a singleton resource.
    /// </summary>
    /// <param name="loadPriority">The priority for injection ordering.</param>
    /// <param name="context">The context in which the singleton is used.</param>
    /// <param name="assetPath">The asset path to the ScriptableObject resource.</param>
    /// <param name="nonLazy">Whether to instantiate the singleton immediately (non-lazy).</param>
    /// <param name="extraBindings">Additional types to bind to the singleton.</param>
    public SingletonScriptableObjectResourceAttribute(
      int loadPriority,
      AppContextType context,
      string assetPath,
      params Type[] extraBindings)
      : base(loadPriority, context, assetPath, extraBindings)
    {
    }

    public override string ToString()
      => $"SingletonScriptableObjectAttribute: " +
         $"{nameof(LoadPriority)}={LoadPriority}, " +
         $"{nameof(Context)}={Context}, " +
         $"{nameof(AssetPath)}={AssetPath}, " +
         $"{nameof(NonLazy)}={NonLazy}, " +
         $"{nameof(ExtraBindings)}={string.Join(", ", ExtraBindings)}";
  }
}