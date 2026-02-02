using System;
using System.Collections.Generic;
using System.Linq;
using OG.Data;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

namespace OG.Installers
{
  public static class ContainerExtensions
  {
    private static ProjectContainer projectContainer;

    public static GameObject InjectFallback(this GameObject target, AppContextType? context = null)
      => projectContainer.Inject(target, context);

    public static TObjectType InjectFallback<TObjectType>(this TObjectType target, AppContextType? context = null)
      where TObjectType : Component
      => projectContainer.Inject(target, context);

    public static TResolveType ResolveFallback<TResolveType>(this AppContextType context, ref TResolveType target)
      => projectContainer.Resolve(ref target, context);

    public static TResolveType ResolveFallback<TResolveType>(this AppContextType context)
      => projectContainer.Resolve<TResolveType>(context);

    internal static void RegisterProjectContainer(this ProjectContainer container)
      => projectContainer = container;

    internal static void UnregisterProjectContainer(this ProjectContainer container)
      => projectContainer = null;
  }
}