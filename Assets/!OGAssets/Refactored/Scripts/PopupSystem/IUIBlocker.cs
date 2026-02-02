using Cysharp.Threading.Tasks;
using System;

public interface IUIBlocker
{
    /// <summary>Begin a blocking scope. Dispose to release. Multiple scopes stack safely.</summary>
    UniTask<IDisposable> BlockScopeAsync(string reason = null, float? timeoutSeconds = null);

    /// <summary>True if any active blocking scope exists.</summary>
    bool IsBlocking { get; }

    /// <summary>Emergency release of all blocking scopes (e.g., on scene change).</summary>
    void AllowAll();
}