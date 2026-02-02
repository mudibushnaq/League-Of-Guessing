using System;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(ISceneLoader))]
public class SceneLoader : ISceneLoader, IProjectInitializable
{
    int IProjectInitializable.Order => -100;
    
    // 🔔 New events for the preloader UI to subscribe to:
    public event Action<string>        OnSceneLoadStarted;
    public event Action<string,float>  OnSceneLoadProgress;  // 0..1
    public event Action<string>        OnSceneLoadCompleted;
    public event Action<string,Exception> OnSceneLoadFailed;

    AsyncOperationHandle<SceneInstance>? _currentAdditive;
    
    UniTask IProjectInitializable.Initialize()
    {
        Debug.Log("[IProjectInitializable.Initialize] SceneLoader ready.");
        return UniTask.CompletedTask;
    }
    
    public async UniTask LoadSceneSingleAsync(string sceneAddress)
    {
        OnSceneLoadStarted?.Invoke(sceneAddress);

        // 1) Defer activation so Awake/OnEnable haven't fired yet
        var handle = Addressables.LoadSceneAsync(sceneAddress, LoadSceneMode.Single, activateOnLoad: false);

        try
        {
            while (!handle.IsDone)
            {
                OnSceneLoadProgress?.Invoke(sceneAddress, handle.PercentComplete);
                await UniTask.Yield();
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"Failed to load scene: {sceneAddress}");
            
            // 3) Now activate: components will Awake/OnEnable with injected deps
            await handle.Result.ActivateAsync().ToUniTask();

            OnSceneLoadProgress?.Invoke(sceneAddress, 1f);
            OnSceneLoadCompleted?.Invoke(sceneAddress);
        }
        catch (Exception ex)
        {
            OnSceneLoadFailed?.Invoke(sceneAddress, ex);
            throw;
        }
    }
    
    public async UniTask LoadSceneSingleAsync_Old(string sceneAddress)
    {
        OnSceneLoadStarted?.Invoke(sceneAddress);

        var handle = Addressables.LoadSceneAsync(sceneAddress);
        try
        {
            while (!handle.IsDone)
            {
                float pct = handle.PercentComplete;
                OnSceneLoadProgress?.Invoke(sceneAddress, pct);
                await UniTask.Yield();
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"Failed to load scene: {sceneAddress}");
            
            OnSceneLoadProgress?.Invoke(sceneAddress, 1f);
            OnSceneLoadCompleted?.Invoke(sceneAddress);
        }
        catch (Exception ex)
        {
            OnSceneLoadFailed?.Invoke(sceneAddress, ex);
            throw;
        }
    }

    public async UniTask LoadSceneAdditiveAsync(string sceneAddress)
    {
        OnSceneLoadStarted?.Invoke(sceneAddress);

        var handle = Addressables.LoadSceneAsync(sceneAddress, LoadSceneMode.Additive, true);
        try
        {
            while (!handle.IsDone)
            {
                float pct = handle.PercentComplete;
                OnSceneLoadProgress?.Invoke(sceneAddress, pct);
                await UniTask.Yield();
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"Failed to load scene additively: {sceneAddress}");

            _currentAdditive = handle;
            
            OnSceneLoadProgress?.Invoke(sceneAddress, 1f);
            OnSceneLoadCompleted?.Invoke(sceneAddress);
        }
        catch (Exception ex)
        {
            OnSceneLoadFailed?.Invoke(sceneAddress, ex);
            throw;
        }
    }

    public async UniTask UnloadCurrentAdditiveAsync()
    {
        if (_currentAdditive.HasValue)
        {
            var handle = Addressables.UnloadSceneAsync(_currentAdditive.Value, true);
            await handle.Task;
            _currentAdditive = null;
        }
    }
}
