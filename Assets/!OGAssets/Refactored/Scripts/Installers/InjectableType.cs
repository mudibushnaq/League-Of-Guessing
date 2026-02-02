using System;
using OG.Installers.Attributes;

namespace OG.Installers
{
  public interface IInjectableSingleton
  {
    Type Type { get; }
    InjectableSingletonAttribute Attribute { get; }
  }
  
  public readonly struct InjectableType<TAttributeType> : IInjectableSingleton
    where TAttributeType : InjectableAttribute
  {
    InjectableSingletonAttribute IInjectableSingleton.Attribute => Attribute as InjectableSingletonAttribute;
    
    public Type Type { get; }
    public TAttributeType Attribute { get; }

    public InjectableType(Type type, TAttributeType attribute)
    {
      Type = type;
      Attribute = attribute;
    }
  }
}