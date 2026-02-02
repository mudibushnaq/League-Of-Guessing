using System;
using Cysharp.Threading.Tasks;

public interface ISceneLoader
{
    // Signals
    event Action<string>             OnSceneLoadStarted;
    event Action<string, float>      OnSceneLoadProgress;   // 0..1
    event Action<string>             OnSceneLoadCompleted;
    event Action<string, Exception>  OnSceneLoadFailed;

    // API
    UniTask LoadSceneSingleAsync(string sceneAddress);
    UniTask LoadSceneAdditiveAsync(string sceneAddress);
    UniTask UnloadCurrentAdditiveAsync();
}