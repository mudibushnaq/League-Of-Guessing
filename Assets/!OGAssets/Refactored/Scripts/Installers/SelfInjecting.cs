using UnityEngine;
using Zenject;

namespace OG.Installers
{
    [DisallowMultipleComponent]
    public sealed class SelfInjecting : MonoBehaviour
    {
        [SerializeField] private ContextType injectFromContext = ContextType.Scene;

        private void Awake()
        {
            switch (injectFromContext)
            {
                case ContextType.Project:
                    InjectFromProject();
                    return;

                case ContextType.Scene:
                    InjectFromScene();
                    return;

                default:
                    Debug.LogException(
                        new System.ArgumentOutOfRangeException(nameof(injectFromContext), injectFromContext, null),
                        this);
                    return;
            }
        }

        private void InjectFromProject()
            => ProjectContext
                .Instance
                .Container
                .InjectGameObject(gameObject);

        private void InjectFromScene()
        {
            var sceneContext = FindObjectOfType<SceneContext>();

            if (sceneContext == null)
            {
                InjectFromProject();
                return;
            }

            sceneContext
                .Container
                .InjectGameObject(gameObject);
        }

        private enum ContextType
        {
            Scene,
            Project
        }
    }
}