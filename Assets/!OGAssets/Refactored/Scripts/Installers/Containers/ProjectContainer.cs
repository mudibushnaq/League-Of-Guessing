using System;
using System.Collections.Generic;
using System.Linq;
using OG.Data;
using OG.Installers.Attributes;
using Zenject;
using UnityEngine;

namespace OG.Installers
{
  [SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    createNewInstance: true,
    gameObjectName: "Project Container")]
  public sealed class ProjectContainer : ContainerContext
  {
    public override AppContextType Context => AppContextType.Project;

    private readonly IReadOnlyDictionary<AppContextType, List<ContainerContext>> containers =
      new Dictionary<AppContextType, List<ContainerContext>>()
      {
        { AppContextType.MenuScene, new List<ContainerContext>() },
        { AppContextType.DefaultScene, new List<ContainerContext>() },
        { AppContextType.LegacyScene, new List<ContainerContext>() },
        { AppContextType.PortraitScene, new List<ContainerContext>() },
        { AppContextType.PreloaderScene, new List<ContainerContext>() }
      };

    public TObjectType Inject<TObjectType>(TObjectType target, AppContextType? context = null)
      where TObjectType : Component
    {
      Inject(target.gameObject, context);
      return target;
    }

    public GameObject Inject(GameObject target, AppContextType? context = null)
    {
      foreach (var container in GetContainers(context))
      {
        try
        {
          container.InjectGameObject(target);
          return target; // If we injected successfully, we can exit the method
        }
        catch (System.Exception e)
        {
          // Do nothing, continue to the next container
        }
      }

      // If we reach here, it means no container could inject the GameObject
      throw new ZenjectException(
        $"Could not inject GameObject {target.name} in context {(context.HasValue ? context.Value : "All")}. " +
        "Ensure that the GameObject is bound in the appropriate container.");
    }

    public TResolveType Resolve<TResolveType>(ref TResolveType target, AppContextType? context = null)
    {
      target = Resolve<TResolveType>(context);
      return target;
    }

    public TResolveType Resolve<TResolveType>(AppContextType? context = null)
    {
      foreach (var container in GetContainers(context))
      {
        try
        {
          var item = container.Resolve<TResolveType>();
          return item; // If we resolved successfully, we can exit the method
        }
        catch (System.Exception e)
        {
          // Do nothing, continue to the next container
        }
      }

      // If we reach here, it means no container could resolve the type
      throw new ZenjectException(
        $"Could not resolve type {typeof(TResolveType).Name} in context {(context.HasValue ? context.Value : "All")}. " +
        "Ensure that the type is bound in the appropriate container.");
    }

    protected override void Register(ContainerContext containerContext)
    {
      if (containerContext == this)
      {
        this.RegisterProjectContainer();
        return;
      }

      containers[containerContext.Context].Add(containerContext);
    }

    protected override void Unregister(ContainerContext containerContext)
    {
      if (containerContext == this)
      {
        this.UnregisterProjectContainer();
        return;
      }

      containers[containerContext.Context].Remove(containerContext);
    }

    private IEnumerable<DiContainer> GetContainers(AppContextType? context = null)
    {
      if (context is null)
        return containers[AppContextType.DefaultScene]
          .Concat(containers[AppContextType.PortraitScene])
          .Concat(containers[AppContextType.LegacyScene])
          .Concat(containers[AppContextType.MenuScene])
          .Concat(containers[AppContextType.PreloaderScene])
          .Append(this)
          .Where(x => x != null && !x.Equals(null) && x.Container != null)
          .Select(x => x.Container);

      if (context is AppContextType.Project)
        return Enumerable
          .Empty<DiContainer>()
          .Append(Container);

      if (!containers.TryGetValue(context.Value, out var contextualContainers))
        return Enumerable.Empty<DiContainer>();

      return contextualContainers
        .Where(container => container != null && !container.Equals(null))
        .Select(container => container.Container);
    }
  }
}