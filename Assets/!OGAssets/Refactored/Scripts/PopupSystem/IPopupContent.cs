using Cysharp.Threading.Tasks;

public interface IPopupContent
{
    // Called by PopupService right after the prefab is instantiated.
    // The content should hook its UI and call tcs.TrySetResult(...) when done.
    void Bind(PopupRequest request, UniTaskCompletionSource<PopupResult> tcs);
}