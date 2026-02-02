using System;
using System.Collections.Generic;
using System.Linq;
using OG.Installers.Attributes;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace OG.Installers
{
  public class InstallersWindow : OdinEditorWindow
  {
    [ShowInInspector]
    [TabGroup("Singletons", "All", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> allDisplays;

    [ShowInInspector]
    [TabGroup("Singletons", "Classes", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> classDisplays;

    [ShowInInspector]
    [TabGroup("Singletons", "MonoBehaviours", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> monoBehaviourDisplays;

    [ShowInInspector]
    [TabGroup("Singletons", "Resource Prefabs", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> prefabResourcesDisplays;

    [ShowInInspector]
    [TabGroup("Singletons", "Resource ScriptableObjects", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> scriptableObjectResourceDisplays;
    
    [ShowInInspector]
    [TabGroup("Singletons", "Addressable Prefabs", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> prefabAddressableDisplays;

    [ShowInInspector]
    [TabGroup("Singletons", "Addressable ScriptableObjects", AnimateVisibility = false)]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<SingletonDisplay> scriptableObjectAddressableDisplays;

    [MenuItem("OG/Installers Inspector")]
    private static void ShowWindow()
    {
      GetWindow<InstallersWindow>("Installers Inspector").Show();
    }

    protected override void Initialize()
    {
      base.Initialize();
      minSize = new Vector2(600, 500);
      titleContent = new GUIContent("Installers Inspector");

      allDisplays = AppDomain
        .CurrentDomain
        .GetInjectableSingletons<InjectableSingletonAttribute>()
        .Select(GetDisplay)
        .ToList();

      classDisplays = allDisplays.Where(x => x.SingletonType == SingletonTypes.Class).ToList();
      monoBehaviourDisplays = allDisplays.Where(x => x.SingletonType == SingletonTypes.MonoBehaviour).ToList();
      prefabResourcesDisplays = allDisplays.Where(x => x.SingletonType == SingletonTypes.PrefabResource).ToList();
      scriptableObjectResourceDisplays = allDisplays.Where(x => x.SingletonType == SingletonTypes.ScriptableObjectResource).ToList();
      prefabAddressableDisplays = allDisplays.Where(x => x.SingletonType == SingletonTypes.PrefabAddressable).ToList();
      scriptableObjectAddressableDisplays = allDisplays.Where(x => x.SingletonType == SingletonTypes.ScriptableObjectAddressable).ToList();
    }

    private static SingletonDisplay GetDisplay(IInjectableSingleton singleton, int index)
    {
      var type = singleton.Type;
      var injectableAttribute = singleton.Attribute;
      
      if (injectableAttribute is SingletonPrefabResourceAttribute)
        return new PrefabResourceSingletonDisplay(singleton, index);
      if (injectableAttribute is SingletonScriptableObjectResourceAttribute)
        return new ScriptableObjectResourceSingletonDisplay(singleton, index);
      if (injectableAttribute is SingletonClassAttribute)
        return new ClassSingletonDisplay(singleton, index);
      if (injectableAttribute is SingletonMonoBehaviourAttribute)
        return new MonoBehaviourSingletonDisplay(singleton, index);
      if (injectableAttribute is SingletonPrefabAddressableAttribute)
        return new PrefabAddressableSingletonDisplay(singleton, index);
      if (injectableAttribute is SingletonScriptableObjectAddressableAttribute)
        return new ScriptableObjectAddressableSingletonDisplay(singleton, index);

      Debug.LogWarning($"Unknown injectable singleton type: {type}");
      return null;
    }
  }
}