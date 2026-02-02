// ErrorModalService.cs
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class ErrorModalService
{
    private static ErrorModalView _view;

    /// Call this once after your Canvas is ready.
    public static void Register(ErrorModalView view)
    {
        _view = view;
    }

    public static bool IsReady => _view != null;

    public static UniTask<bool> ShowRetryCancel(string title, string msg, string details)
    {
        if (_view == null)
        {
            Debug.LogWarning("ErrorModalService: No view registered.");
            return UniTask.FromResult(false);
        }
        return _view.ShowAsync(title, msg, details, "Retry", "Cancel");
    }

    public static UniTask<bool> ShowInfo(string title, string msg, string details, string ok = "OK")
    {
        if (_view == null)
        {
            Debug.LogWarning("ErrorModalService: No view registered.");
            return UniTask.FromResult(false);
        }
        // reuse, but hide Retry by labeling as OK and hiding Cancel in the layout if desired
        return _view.ShowAsync(title, msg, details, ok, "Dismiss");
    }
}