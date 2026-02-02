using Cysharp.Threading.Tasks;
using OG.Data;
using UnityEngine;
using Zenject;

namespace OG.Initialization.Initializers
{
    internal abstract class InitializerBase : MonoBehaviour
    {
        private static bool projectInitialized;
        protected abstract AppContextType context { get; }

        [Inject] protected DiContainer container { get; private set; }

        private async void Start()
        {
            if (context != AppContextType.Project)
            {
                Debug.Log($"[AppInitializer] Waiting for project initialization to complete before continuing with {context} context initialization.");
                await UniTask.WaitUntil(() => projectInitialized);
                Debug.Log($"[AppInitializer] Project initialization completed, continuing with {context} context initialization.");
            }
            
            Debug.Log($"[AppInitializer] Initializing {context} context...");
            await context.Initialize(container);
            Debug.Log($"[AppInitializer] {context} context initialization completed.");
            
            if (context == AppContextType.Project)
                projectInitialized = true;
        }
    }
}