using Cysharp.Threading.Tasks;

public interface IPopupService
{
    UniTask<PopupResult> ShowAsync(PopupRequest request);
}