using System;
using System.Collections.Generic;
using System.Linq;
using OG.Data;
using OG.Initialization;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace Initialization.Editor
{
  public class InitializationInspector : OdinEditorWindow
  {
    [ShowInInspector]
    [TabGroup("Project", AnimateVisibility = false)]
    [LabelText("Ordered List of executed initializers")]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<InitializableDisplay> project;

    [ShowInInspector]
    [TabGroup("Preloader", AnimateVisibility = false)]
    [LabelText("Ordered List of executed initializers")]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<InitializableDisplay> preloader;

    [ShowInInspector]
    [TabGroup("Menu", AnimateVisibility = false)]
    [LabelText("Ordered List of executed initializers")]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<InitializableDisplay> menu;

    [ShowInInspector]
    [TabGroup("Game", AnimateVisibility = false)]
    [LabelText("Ordered List of executed initializers")]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<InitializableDisplay> game;
    
    [ShowInInspector]
    [TabGroup("PortraitGame", AnimateVisibility = false)]
    [LabelText("Ordered List of executed initializers")]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<InitializableDisplay> portraitGame;
    
    [ShowInInspector]
    [TabGroup("LegacyGame", AnimateVisibility = false)]
    [LabelText("Ordered List of executed initializers")]
    [ListDrawerSettings(ShowFoldout = false, ShowPaging = false,
      ShowItemCount = false, ShowIndexLabels = false, IsReadOnly = true)]
    private List<InitializableDisplay> legacyGame;

    [MenuItem("OG/Initialization Inspector")]
    private static void ShowWindow()
    {
      GetWindow<InitializationInspector>("Installers Inspector").Show();
    }

    protected override void Initialize()
    {
      base.Initialize();
      minSize = new Vector2(600, 500);
      titleContent = new GUIContent("Initialization Inspector");

      var domain = AppDomain.CurrentDomain;

      project = domain
        .GetAllImplementations<IProjectInitializable>()
        .Select((x, i) => CreateDisplay(Activator.CreateInstance(x) as IProjectInitializable))
        .OrderBy(x => x.Order)
        .ToList();

      preloader = domain
        .GetAllImplementations<IPreloaderInitializable>()
        .Select((x, i) => CreateDisplay(Activator.CreateInstance(x) as IPreloaderInitializable))
        .OrderBy(x => x.Order)
        .ToList();

      menu = domain
        .GetAllImplementations<IMenuInitializable>()
        .Select((x, i) => CreateDisplay(Activator.CreateInstance(x) as IMenuInitializable))
        .OrderBy(x => x.Order)
        .ToList();

      game = domain
        .GetAllImplementations<IGameInitializable>()
        .Select((x, i) => CreateDisplay(Activator.CreateInstance(x) as IGameInitializable))
        .OrderBy(x => x.Order)
        .ToList();
      
      portraitGame = domain
        .GetAllImplementations<IPortraitGameInitializable>()
        .Select((x, i) => CreateDisplay(Activator.CreateInstance(x) as IPortraitGameInitializable))
        .OrderBy(x => x.Order)
        .ToList();
      
      legacyGame = domain
        .GetAllImplementations<ILegacyGameInitializable>()
        .Select((x, i) => CreateDisplay(Activator.CreateInstance(x) as ILegacyGameInitializable))
        .OrderBy(x => x.Order)
        .ToList();

      project.ForEach((x, i) => x.executionOrder = i + 1);
      preloader.ForEach((x, i) => x.executionOrder = i + 1);
      menu.ForEach((x, i) => x.executionOrder = i + 1);
      game.ForEach((x, i) => x.executionOrder = i + 1);
      portraitGame.ForEach((x, i) => x.executionOrder = i + 1);
      legacyGame.ForEach((x, i) => x.executionOrder = i + 1);
    }

    private static InitializableDisplay CreateDisplay(IProjectInitializable initializable)
      => new InitializableDisplay(initializable.Order, initializable.GetType());

    private static InitializableDisplay CreateDisplay(IPreloaderInitializable initializable)
      => new InitializableDisplay(initializable.Order, initializable.GetType());

    private static InitializableDisplay CreateDisplay(IMenuInitializable initializable)
      => new InitializableDisplay(initializable.Order, initializable.GetType());

    private static InitializableDisplay CreateDisplay(IGameInitializable initializable)
      => new InitializableDisplay(initializable.Order, initializable.GetType());
    
    private static InitializableDisplay CreateDisplay(IPortraitGameInitializable initializable)
      => new InitializableDisplay(initializable.Order, initializable.GetType());
    
    private static InitializableDisplay CreateDisplay(ILegacyGameInitializable initializable)
      => new InitializableDisplay(initializable.Order, initializable.GetType());

    [InlineProperty, HideReferenceObjectPicker, HideDuplicateReferenceBox]
    public class InitializableDisplay
    {
      public int Order => definedOrder;

      [ShowInInspector, DisplayAsString(EnableRichText = true), BoxGroup, HideLabel]
      private string typeName;

      [ShowInInspector, DisplayAsString, BoxGroup]
      public int executionOrder;

      [ShowInInspector, DisplayAsString, BoxGroup]
      private int definedOrder;

      private Type type;

      public InitializableDisplay(int order, Type type)
      {
        this.type = type;
        this.definedOrder = order;
        this.typeName = $"<b>{type.Name}</b>";
      }

      [Button, HorizontalGroup("Buttons")]
      private void Select()
      {
        var scriptAsset = FindScriptAssetForType(type);
        if (scriptAsset != null)
        {
          Selection.activeObject = scriptAsset;
        }
        else
        {
          Debug.LogError($"Script asset for {typeName} not found.");
        }
      }

      [Button, HorizontalGroup("Buttons")]
      private void Edit()
      {
        var scriptAsset = FindScriptAssetForType(type);
        if (scriptAsset != null)
        {
          AssetDatabase.OpenAsset(scriptAsset);
        }
        else
        {
          Debug.LogError($"Script asset for {typeName} not found.");
        }
      }

      MonoScript FindScriptAssetForType(Type type)
      {
        string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
        foreach (string guid in guids)
        {
          string path = AssetDatabase.GUIDToAssetPath(guid);
          MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
          if (ms != null && ms.GetClass() == type)
            return ms;
        }

        return null;
      }
    }
  }
}