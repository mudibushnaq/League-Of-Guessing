using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OG.Data;

namespace OG.Installers.Attributes
{
  public abstract class InjectableAttribute : Attribute
  {
    /// <summary>
    /// Defines priority of the singleton. Higher values are bound first.
    /// </summary>
    public int LoadPriority { get; }

    /// <summary>
    /// Do not lazy bind this singleton.
    /// </summary>
    public bool NonLazy { get; }

    /// <summary>
    /// Extra bindings for this injectable.
    /// Anything besides interfaces will be ignored.
    /// </summary>
    public IReadOnlyList<Type> ExtraBindings { get; }

    protected InjectableAttribute(
      int loadPriority,
      bool nonLazy = true,
      params Type[] extraBindings)
    {
      LoadPriority = loadPriority;
      NonLazy = nonLazy;
      ExtraBindings = extraBindings.ToList();
    }
  }
}