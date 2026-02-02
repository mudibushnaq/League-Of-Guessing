using System;
using OG.Installers.Attributes;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace OG.Installers
{
    public abstract class SingletonDisplayBase<T> : SingletonDisplay
        where T : InjectableSingletonAttribute
    {
        public Type TargetType { get; }
        public T TypedAttribute => attribute;
        public override InjectableSingletonAttribute Attribute => TypedAttribute;
        private readonly T attribute;

        [ShowInInspector, DisplayAsString]
        private int loadOrder;

        [ShowInInspector, DisplayAsString]
        private bool nonLazy;

        protected SingletonDisplayBase(IInjectableSingleton singleton, int index) 
            : base(singleton.Attribute.Context, index, singleton.Type?.Name ?? "Unknown")
        {
            attribute = singleton.Attribute as T;
            TargetType = singleton.Type;
            loadOrder = attribute?.LoadPriority ?? 0;
            nonLazy = attribute?.NonLazy ?? false;
        }

        [Button, HorizontalGroup("Buttons")]
        private void Select()
        {
            var scriptAsset = FindScriptAssetForType(TargetType);
            if (scriptAsset != null)
            {
                Selection.activeObject = scriptAsset;
            }
            else
            {
                Debug.LogError($"Script asset for {name} not found.");
            }
        }

        [Button, HorizontalGroup("Buttons")]
        private void Edit()
        {
            var scriptAsset = FindScriptAssetForType(TargetType);
            if (scriptAsset != null)
            {
                AssetDatabase.OpenAsset(scriptAsset);
            }
            else
            {
                Debug.LogError($"Script asset for {name} not found.");
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