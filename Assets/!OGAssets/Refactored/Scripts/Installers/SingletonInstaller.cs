using System;
using System.Linq;
using OG.Data;
using UnityEngine;
using Zenject;
using OG.Installers.Attributes;
using UnityEngine.AddressableAssets;

namespace OG.Installers
{
  public class SingletonInstaller : MonoInstaller
  {
    [SerializeField]
    private AppContextType context;

    public override void InstallBindings()
    {
      foreach (var injectable in InstallerUtilities.GetAllSingletons(context))
      {
        var concreteType = injectable.Type;
        var attribute = injectable.Attribute;

        var contracts =
          new[] { concreteType }
            .Concat(attribute.ExtraBindings ?? Enumerable.Empty<Type>())
            .ToArray();

        FromBinderNonGeneric binding = Container.Bind(contracts).To(concreteType);


        //Debug.Log($"Binding ({concreteType.Name}) singleton: {injectable.Attribute} " +
                 // $"with {injectable.Attribute.ExtraBindings?.Count ?? 0} extra bindings");

        try
        {
          switch (injectable.Attribute)
          {
            case SingletonClassAttribute singletonAttribute:
              BindSingleton(binding, singletonAttribute);
              break;

            case SingletonMonoBehaviourAttribute behaviourAttribute:
              BindSingleton(binding, behaviourAttribute);
              break;

            case SingletonPrefabResourceAttribute prefabResourceAttribute:
              BindSingleton(binding, prefabResourceAttribute);
              break;

            case SingletonScriptableObjectResourceAttribute scriptableObjectResourceAttribute:
              BindSingleton(binding, scriptableObjectResourceAttribute);
              break;

            case SingletonPrefabAddressableAttribute prefabAddressableAttribute:
              BindSingleton(binding, prefabAddressableAttribute);
              break;

            case SingletonScriptableObjectAddressableAttribute scriptableObjectAddressableAttribute:
              BindSingleton(binding, scriptableObjectAddressableAttribute);
              break;
          }
        }
        catch (Exception ex)
        {
          throw new Exception(
            $"[Singleton] " +
            $"Failed to bind singleton: {injectable.Attribute}\n" +
            $"from: {string.Join(", ", contracts.Select(x => x.Name))}\n" +
            $"to: {concreteType}\n" +
            $"error: {ex.Message}", ex);
          throw;
        }

        /*Bind(
          from: injectable.Type,
          to: injectable.Type,
          attribute: injectable.Attribute);

        for (var i = 0; i < injectable.Attribute.ExtraBindings.Count; i++)
        {
          Bind(
            from: injectable.Attribute.ExtraBindings[i],
            to: injectable.Type,
            attribute: injectable.Attribute);
        }*/
      }
    }

    private void BindSingleton(FromBinderNonGeneric binding, SingletonClassAttribute attribute)
    {
      binding
        .FromNew()
        .AsSingle()
        .NonLazy();
    }

    private void BindSingleton(FromBinderNonGeneric binding, SingletonMonoBehaviourAttribute attribute)
    {
      void OnInstantiated(InjectContext _, object obj)
      {
        InitializeSingleton(
          target: obj,
          name: $"[Singleton] " +
                $"[{attribute.Context}] " +
                $"{attribute.GameObjectName} " +
                $"({attribute.LoadPriority})");
      }

      if (attribute.CreateNewInstance)
      {
        binding
          .FromNewComponentOnNewGameObject()
          .AsSingle()
          .OnInstantiated(OnInstantiated)
          .NonLazy();
      }
      else
      {
        binding
          .FromComponentsInHierarchy()
          .AsSingle()
          .OnInstantiated(OnInstantiated)
          .NonLazy();
      }
    }

    private void BindSingleton(FromBinderNonGeneric binding, SingletonPrefabResourceAttribute attribute)
    {
      void OnInstantiated(InjectContext _, object obj)
      {
        InitializeSingleton(
          target: obj,
          name: $"[Singleton] " +
                $"[{attribute.Context}] " +
                $"{attribute.GameObjectName} " +
                $"({attribute.LoadPriority})");
      }

      binding
        .FromComponentInNewPrefabResource(attribute.AssetPath)
        .UnderTransform(parent: null)
        .AsSingle()
        .OnInstantiated(OnInstantiated)
        .NonLazy();
    }

    private void BindSingleton(FromBinderNonGeneric binding, SingletonPrefabAddressableAttribute attribute)
    {
      void OnInstantiated(InjectContext _, object obj)
      {
        InitializeSingleton(
          target: obj,
          name: $"[Singleton] " +
                $"[{attribute.Context}] " +
                $"{attribute.GameObjectName} " +
                $"({attribute.LoadPriority})");
      }

      var prefab = Addressables
        .LoadAssetAsync<GameObject>(attribute.Address)
        .WaitForCompletion();

      if (prefab == null)
        throw new NullReferenceException(
          $"[Singleton] " +
          $"Failed to load prefab from address: {attribute.Address}\n" +
          $"when binding: {attribute}");

      binding
        .FromComponentInNewPrefab(prefab)
        .AsSingle()
        .OnInstantiated(OnInstantiated)
        .NonLazy();
    }

    private void BindSingleton(FromBinderNonGeneric binding, SingletonScriptableObjectResourceAttribute attribute)
    {
      binding
        .FromNewScriptableObjectResource(attribute.AssetPath)
        .AsSingle()
        .NonLazy();
    }

    private void BindSingleton(FromBinderNonGeneric binding, SingletonScriptableObjectAddressableAttribute attribute)
    {
      var scriptableObject = Addressables
        .LoadAssetAsync<ScriptableObject>(attribute.Address)
        .WaitForCompletion();

      if (scriptableObject == null)
        throw new NullReferenceException(
          $"[Singleton] " +
          $"Failed to load ScriptableObject from address: {attribute.Address}\n" +
          $"when binding: {attribute}");

      binding
        .FromNewScriptableObject(scriptableObject)
        .AsSingle()
        .NonLazy();
    }

    private void InitializeSingleton(object target, string name)
    {
      var go = target switch
      {
        GameObject gameObject => gameObject,
        Behaviour behaviour => behaviour.gameObject,
        Transform transform => transform.gameObject,
        _ => null
      };

      if (!go)
        return;

      if (!string.IsNullOrWhiteSpace(name))
        go.name = name;

      go.transform.SetParent(this.transform);
    }
  }
}