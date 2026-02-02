using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  public abstract class InjectableAddressableAttribute : InjectableSingletonAttribute
  {
    /// <summary>
    /// Path to the addressable asset.
    /// </summary>
    public string Address { get; }

    public InjectableAddressableAttribute(
      int loadPriority,
      AppContextType context,
      string address,
      params Type[] extraBindings)
      : base(loadPriority, context, extraBindings)
    {
      Address = address;
    }
  }
}