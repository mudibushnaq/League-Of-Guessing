using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OG.Data
{
  public static class AppDomainExtensions
  {
    public static IEnumerable<Type> GetAllProjectTypes(this AppDomain domain)
      => domain
        .GetAssemblies()
        .Where(asm => !asm.IsDynamic
                      && !asm.FullName.StartsWith("System")
                      && !asm.FullName.StartsWith("Unity"))
        .SelectMany(assembly =>
        {
          try
          {
            return assembly.GetTypes();
          }
          catch (ReflectionTypeLoadException e)
          {
            return e.Types.Where(t => t != null).ToArray();
          }
        });

    public static IEnumerable<Type> GetAllImplementations<TBaseType>(this AppDomain domain)
      => domain
        .GetAllImplementations(typeof(TBaseType));

    public static IEnumerable<Type> GetAllImplementations(this AppDomain domain, Type baseType)
      => domain
        .GetAllProjectTypes()
        .Where(type => type != null
                       && baseType.IsAssignableFrom(type)
                       && type.IsClass
                       && !type.IsAbstract);

    public static IEnumerable<(Type, TAttribute)> GetClassesWithAttribute<TAttribute>(this AppDomain domain)
      where TAttribute : Attribute
      => domain
        .GetAllProjectTypes()
        .Select(t => (Type: t, Attr: t.GetCustomAttribute<TAttribute>()))
        .Where(x => x.Attr is not null);
  }
}