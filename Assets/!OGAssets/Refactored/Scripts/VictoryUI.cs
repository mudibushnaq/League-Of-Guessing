using Cysharp.Threading.Tasks;
using OG.Initialization;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class VictoryUI : MonoBehaviour, IPopupContent
{
    //int IMenuInitializable.Order => 250;
    
    public Button backButton;
    
    private PopupRequest req;
    private UniTaskCompletionSource<PopupResult> _tcs;

    [Inject] private ISceneLoader _sceneLoader;
    
    //UniTask IMenuInitializable.Initialize()
    //{
    //    return UniTask.CompletedTask;
    //}
    
    public void Bind(PopupRequest request, UniTaskCompletionSource<PopupResult> tcs)
    {
        backButton.interactable = true;
        req = request;
        _tcs = tcs;
        
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(OnBackClicked);
    }

    void OnBackClicked() => LoadMenu().Forget();

    private async UniTaskVoid LoadMenu()
    {
        backButton.interactable = false;
        _tcs?.TrySetResult(PopupResult.Secondary);
        await _sceneLoader.LoadSceneSingleAsync("scenes/MenuScene");
    }
}
