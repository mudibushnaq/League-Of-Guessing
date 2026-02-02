using System;
using OG.Data;
using OG.Installers.Attributes;
using UnityEngine;

namespace OG.Installers
{
  public abstract class ThirdPartySingleton
  {
    /// <summary>
    /// The priority for injection ordering.
    /// </summary>
    public abstract int LoadOrder { get; }

    /// <summary>
    /// The context in which the singleton is used.
    /// </summary>
    public abstract AppContextType Context { get; }

    /// <summary>
    /// The asset path to the ScriptableObject resource.
    /// </summary>
    public abstract string AssetPath { get; }

    /// <summary>
    /// Whether to instantiate the singleton immediately (non-lazy).
    /// </summary>
    public abstract string GameObjectName { get; }

    /// <summary>
    /// The name of the GameObject that will be created from the prefab.
    /// </summary>
    public virtual bool NonLazy { get; } = true;

    /// <summary>
    /// Additional types to bind to the singleton.
    /// </summary>
    public virtual Type[] ExtraBindings { get; } = Array.Empty<Type>();

    protected abstract (Type, InjectableSingletonAttribute) GetSingletonInfo();

    public abstract Type GetTargetType();

    public InjectableSingletonAttribute GetAttribute()
      => new SingletonPrefabResourceAttribute(LoadOrder, Context, AssetPath, GameObjectName, ExtraBindings);
  }

  public abstract class ThirdPartySingleton<TInstanceType> : ThirdPartySingleton
    where TInstanceType : MonoBehaviour
  {
    protected override (Type, InjectableSingletonAttribute) GetSingletonInfo()
      => (typeof(TInstanceType), GetAttribute());

    public override Type GetTargetType()
      => typeof(TInstanceType);
  }
}