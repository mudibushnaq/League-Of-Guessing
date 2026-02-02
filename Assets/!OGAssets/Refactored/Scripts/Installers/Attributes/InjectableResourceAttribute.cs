using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  public abstract class InjectableResourceAttribute : InjectableSingletonAttribute
  {
    /// <summary>
    /// Resource Path
    /// </summary>
    public string AssetPath { get; }

    public InjectableResourceAttribute(
      int loadPriority,
      AppContextType context,
      string assetPath,
      params Type[] extraBindings)
      : base(loadPriority, context, extraBindings)
    {
      AssetPath = assetPath;
    }
  }
}