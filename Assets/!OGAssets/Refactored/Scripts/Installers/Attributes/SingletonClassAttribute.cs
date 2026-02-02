using System;
using System.Collections.Generic;
using System.Linq;
using OG.Data;

namespace OG.Installers.Attributes
{
  [AttributeUsage(AttributeTargets.Class, Inherited = false)]
  public sealed class SingletonClassAttribute : InjectableSingletonAttribute
  {
    /// <summary>
    /// Attribute to mark a basic C# class as a singleton resource.
    /// </summary>
    /// <param name="loadPriority">The priority for injection ordering.</param>
    /// <param name="context">The context in which the singleton is used.</param>
    /// <param name="nonLazy">Whether to instantiate the singleton immediately (non-lazy).</param>
    /// <param name="extraBindings">Additional types to bind in the container.</param>
    public SingletonClassAttribute(
      int loadPriority,
      AppContextType context,
      params Type[] extraBindings)
      : base(loadPriority, context, extraBindings)
    {
    }

    public override string ToString()
      => $"SingletonClassAttribute: " +
         $"{nameof(LoadPriority)}={LoadPriority}, " +
         $"{nameof(Context)}={Context}, " +
         $"{nameof(NonLazy)}={NonLazy}, " +
         $"{nameof(ExtraBindings)}={string.Join(", ", ExtraBindings.Select(t => t.Name))}";
  }
}