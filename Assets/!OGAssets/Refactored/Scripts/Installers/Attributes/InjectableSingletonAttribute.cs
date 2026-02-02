using System.Collections.Generic;
using System.Linq;
using System;
using OG.Data;

namespace OG.Installers.Attributes
{
  public abstract class InjectableSingletonAttribute : InjectableAttribute
  {
    /// <summary>
    /// Defines which context this singleton should be bound to.
    /// </summary>
    public AppContextType Context { get; }

    public InjectableSingletonAttribute(
      int loadPriority,
      AppContextType context,
      params Type[] extraBindings)
      : base(loadPriority, nonLazy: true, extraBindings)
    {
      Context = context;
    }
  }
}