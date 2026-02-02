using OG.Data;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(Bootstrapper),
    context: AppContextType.PreloaderScene)]
public sealed class Bootstrapper : MonoBehaviour
{
    [Inject] ILevelsProviderService _levels;
    [Inject] ISceneLoader _sceneLoader;
    
    async void Start()
    {
        // NEW: Only discover level IDs, don't download/load assets for fast startup
        await _levels.DiscoverLevelsOnlyAsync();
        await _sceneLoader.LoadSceneSingleAsync("scenes/MenuScene");
    }
}