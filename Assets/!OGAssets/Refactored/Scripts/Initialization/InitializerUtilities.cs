using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using OG.Data;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace OG.Initialization
{
  internal static class InitializerUtilities
  {
    private static readonly IReadOnlyDictionary<AppContextType, TypingInfo> typesMap = GenerateTypeMap();

    internal static int GetOrder(this object obj, AppContextType context)
    {
      // the 2nd check is to avoid unity issues that happen with interface reference usages
      if (obj == null || obj.Equals(null))
        return 0;

      return typesMap[context].GetOrder(obj);
    }

    internal static UniTask InitializeAsync(this object obj, AppContextType context)
      => typesMap[context].InitializeAsync(obj);

    internal static IReadOnlyList<object> GetInstances(this AppContextType context, DiContainer container)
    {
      var instances = new List<object>();

      foreach (var type in typesMap[context].Types)
      {
        var resolved = container.TryResolve(type);

        if (typeof(MonoBehaviour).IsAssignableFrom(type) && resolved == null)
          resolved = Object
            .FindAnyObjectByType(type); // TODO stop using this, use Zenject to resolve types fully instead

        if (resolved == null)
        {
          Debug.LogError($"[AppInitializer] Failed to resolve type {type.Name}. " +
                         "Ensure it is bound correctly in the Zenject container or exists in the scene.");
          continue;
        }

        if (resolved is IProjectInitializable or
            IPreloaderInitializable or
            IMenuInitializable or
            IGameInitializable or
            IPortraitGameInitializable or
            ILegacyGameInitializable)
          instances.Add(resolved);
        else
          Debug.LogError($"[AppInitializer] Type {type.Name} does not implement IProjectInitializable.");
      }

      return instances;
    }

    private static IReadOnlyDictionary<AppContextType, TypingInfo> GenerateTypeMap()
    {
      var domain = AppDomain.CurrentDomain;

      return new Dictionary<AppContextType, TypingInfo>
      {
        {
          AppContextType.Project,
          new TypingInfo(domain.GetAllImplementations<IProjectInitializable>(),
            (obj) => ((IProjectInitializable)obj).Order,
            (obj) => ((IProjectInitializable)obj).Initialize())
        },
        {
          AppContextType.MenuScene,
          new TypingInfo(domain.GetAllImplementations<IMenuInitializable>(),
            (obj) => ((IMenuInitializable)obj).Order,
            (obj) => ((IMenuInitializable)obj).Initialize())
        },
        {
          AppContextType.DefaultScene,
          new TypingInfo(domain.GetAllImplementations<IGameInitializable>(),
            (obj) => ((IGameInitializable)obj).Order,
            (obj) => ((IGameInitializable)obj).Initialize())
        },
        {
          AppContextType.PortraitScene,
          new TypingInfo(domain.GetAllImplementations<IPortraitGameInitializable>(),
            (obj) => ((IPortraitGameInitializable)obj).Order,
            (obj) => ((IPortraitGameInitializable)obj).Initialize())
        },
        {
          AppContextType.LegacyScene,
          new TypingInfo(domain.GetAllImplementations<ILegacyGameInitializable>(),
            (obj) => ((ILegacyGameInitializable)obj).Order,
            (obj) => ((ILegacyGameInitializable)obj).Initialize())
        },
        {
          AppContextType.PreloaderScene,
          new TypingInfo(domain.GetAllImplementations<IPreloaderInitializable>(),
            (obj) => ((IPreloaderInitializable)obj).Order,
            (obj) => ((IPreloaderInitializable)obj).Initialize())
        }
      };
    }

    internal static async UniTask Initialize(this AppContextType context, DiContainer container)
    {
      var instances = context
        .GetInstances(container)
        .OrderBy(x => x.GetOrder(context))
        .ToList();

      try
      {
        Debug.Log($"[AppInitializer] Found {instances.Count} initializable instances." +
                  $"Starting initialization...\n" +
                  $"Types:\n" +
                  $"{string.Join("\n", instances.Select(i => $"{i.GetOrder(context)} -- {i.GetType().Name}"))}");

        foreach (var instance in instances)
        {
          var order = instance.GetOrder(context);

          //Debug.Log($"[AppInitializer] ({order}) [{instance.GetType().Name}] " +
                  //  $"Attempting to initialize");

          var type = instance.GetType();

          try
          {
            await instance.InitializeAsync(context);
            //Debug.Log($"[AppInitializer] ({order}) [{type.Name}] " +
                //      $"Initialized successfully.");
          }
          catch (Exception e)
          {
            Debug.LogError($"[AppInitializer] ({order}) [{instance.GetType().Name}] " +
                           $"Failed to initialize.");
            Debug.LogException(e);
          }
        }
      }
      catch (Exception e)
      {
        Debug.LogError("[AppInitializer] An error occurred during initialization: " + e.Message);
      }
    }
  }
}