// AddressablesGlobalErrors.cs
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressablesGlobalErrors
{
    private static bool _hooked;
    private static bool _showing;

    public static void EnsureHook()
    {
        if (_hooked) return;
        _hooked = true;

        ResourceManager.ExceptionHandler = async (AsyncOperationHandle op, System.Exception ex) =>
        {
            var debugName = "<invalid handle>";
            try { if (op.IsValid()) debugName = op.DebugName; } catch { }

            Debug.LogError($"[Addressables/Global] {debugName} failed: {ex}");

            if (_showing) return;
            _showing = true;
            try
            {
                if (ErrorModalService.IsReady)
                {
                    await ErrorModalService.ShowInfo(
                        "Background Addressables Error",
                        ex.Message,
                        ex.ToString(),
                        ok: "OK"
                    );
                }
            }
            finally { _showing = false; }
        };
    }
}