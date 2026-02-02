using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using OG.Data;
using OG.Installers.Attributes;
using Zenject;

namespace OG.Installers
{
  public static class InstallerUtilities
  {
    internal static IEnumerable<IInjectableSingleton> GetInjectableSingletons<TAttributeType>(
      AppContextType? context = null)
      where TAttributeType : InjectableSingletonAttribute
      => AppDomain.CurrentDomain.GetInjectableSingletons<TAttributeType>(context);

    public static IEnumerable<IInjectableSingleton> GetInjectableSingletons<TAttributeType>(
      this AppDomain domain,
      AppContextType? context = null)
      where TAttributeType : InjectableSingletonAttribute
      => domain
        .GetClassesWithAttribute<TAttributeType>()
        .Select(x => new InjectableType<TAttributeType>(x.Item1, x.Item2) as IInjectableSingleton)
        .Where(x => !context.HasValue || x.Attribute.Context == context)
        .OrderByDescending(x => x.Attribute.LoadPriority);

    internal static IEnumerable<IInjectableSingleton> GetThirdPartySingletons(
      AppContextType? context = null)
      => AppDomain.CurrentDomain.GetThirdPartySingletons(context);

    public static IEnumerable<IInjectableSingleton> GetThirdPartySingletons(
      this AppDomain domain,
      AppContextType? context = null)
      => domain
        .GetAllImplementations<ThirdPartySingleton>()
        .Select(x => (ThirdPartySingleton)Activator.CreateInstance(x))
        .Select(x => new InjectableType<InjectableSingletonAttribute>(
          x.GetTargetType(),
          x.GetAttribute()) as IInjectableSingleton)
        .Where(x => !context.HasValue || x.Attribute.Context == context)
        .OrderByDescending(x => x.Attribute.LoadPriority);

    internal static IEnumerable<IInjectableSingleton> GetAllSingletons(
      AppContextType? context = null)
      => AppDomain.CurrentDomain.GetAllSingletons(context);

    internal static IEnumerable<IInjectableSingleton> GetAllSingletons(
      this AppDomain domain,
      AppContextType? context = null)
      => domain.GetInjectableSingletons<InjectableSingletonAttribute>(context)
        .Concat(domain.GetThirdPartySingletons(context))
        .OrderByDescending(x => x.Attribute.LoadPriority);
  }
}