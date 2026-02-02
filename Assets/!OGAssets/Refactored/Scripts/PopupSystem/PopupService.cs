using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers;
using OG.Installers.Attributes;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;
using Zenject;

[SingletonPrefabResource(
    loadPriority: Priority.HIGHEST,
    context: AppContextType.Project,
    assetPath: nameof(PopupService),
    gameObjectName: nameof(PopupService),
    extraBindings: typeof(IPopupService))]
public sealed class PopupService : MonoBehaviour, IPopupService, IProjectInitializable
{
    int IProjectInitializable.Order => 150;
    [Inject] private ProjectContainer projectContainer;
    
    UniTask IProjectInitializable.Initialize()
    {
        //await InitializeAsync();
        Debug.Log("[IProjectInitializable.Initialize] PopupService ready.");
        return UniTask.CompletedTask;
    }
    
    private string defaultChannelId = "Menu";
    private readonly Dictionary<string, PopupChannel> _channels = new();      // id -> channel
    private readonly Dictionary<string, GameObject> _canvasCache = new();     // addrKey -> instance

    // ---------- Channels (your existing gameplay canvas registration stays the same) ----------
    public void RegisterOrUpdateChannel(string id, Transform root, PopupView prefab, ClickBlocker blocker, PopupView preplaced = null)
    {
        _channels[id] = new PopupChannel { Id = id, PopupRoot = root, PopupPrefab = prefab, Blocker = blocker, PreplacedView = preplaced };
    }
    public void UnregisterChannel(string id) => _channels.Remove(id);

    // ---------- Public API ----------
    public UniTask<PopupResult> ShowAsync(PopupRequest request)
    {
        request.PressedToButton.interactable = false;
        return ShowAsync(request, defaultChannelId);
    }
        

    public async UniTask<PopupResult> ShowAsync(PopupRequest request, string channelId)
    {
        switch (request.TemplateMode)
        {
            case PopupTemplateMode.GameObjectView:
                return await ShowUsingGameObjectAsync(request);
            case PopupTemplateMode.AddressableCanvas:
                return await ShowUsingAddressableCanvasAsync(request); // ignores channelId

            case PopupTemplateMode.AddressableView:
                // needs an existing channel (Gameplay/Menu/etc.)
                if (!_channels.TryGetValue(channelId, out var ch))
                {
                    Debug.LogError($"PopupService: No channel '{channelId}' for AddressableView '{request.AddressableViewKey}'.");
                    return PopupResult.None;
                }
                return await ShowUsingAddressableViewAsync(request, ch);

            default:
                // default: use the registered channel's view/prefab
                if (!_channels.TryGetValue(channelId, out var channel))
                {
                    Debug.LogError($"PopupService: No channel '{channelId}'.");
                    return PopupResult.None;
                }
                return await InternalShowAsync(request, channel, usePreplacedOrPrefab: true);
        }
    }

    // ---------- Addressable VIEW (under an existing channel) ----------
    private async UniTask<PopupResult> ShowUsingAddressableViewAsync(PopupRequest req, PopupChannel channel)
    {
        if (string.IsNullOrEmpty(req.AddressableViewKey))
        {
            Debug.LogError("PopupService: AddressableViewKey is empty.");
            return PopupResult.None;
        }

        IDisposable scope = null;
        if (req.IsModal && channel.Blocker != null)
            scope = await channel.Blocker.BlockScopeAsync("Popup");

        GameObject viewGO = null;
        try
        {
            viewGO = await Addressables.InstantiateAsync(req.AddressableViewKey, channel.PopupRoot).Task;
            projectContainer.Inject(viewGO);
            viewGO.SetActive(true);

            // 1) CONTENT-FIRST: if prefab has IPopupContent, let it drive the result
            var content = viewGO.GetComponentInChildren<IPopupContent>(true);
            if (content != null)
            {
                var tcs = new UniTaskCompletionSource<PopupResult>();
                content.Bind(req, tcs);
                var result = await tcs.Task;

                scope?.Dispose();
                await UniTask.Delay(TimeSpan.FromSeconds(channel.Blocker ? channel.Blocker.FadeDuration : 0.1f));
                return result;
            }

            // 2) Otherwise, fall back to PopupView flow
            var view = viewGO.GetComponent<PopupView>();
            if (!view)
            {
                Debug.LogError($"PopupService: Addressable '{req.AddressableViewKey}' has neither IPopupContent nor PopupView.");
                return PopupResult.None;
            }

            var tcs2 = new UniTaskCompletionSource<PopupResult>();
            var title = ApplyTokens(req.Title, req.Tokens);
            var message = ApplyTokens(req.Message, req.Tokens);
            view.Bind(title, message, req.Icon, req.Style, req.Buttons, tcs2);

            if (req.AutoDismissSeconds.HasValue)
                _ = AutoDismiss(req.AutoDismissSeconds.Value, tcs2);

            var result2 = await tcs2.Task;

            scope?.Dispose();
            await UniTask.Delay(TimeSpan.FromSeconds(channel.Blocker ? channel.Blocker.FadeDuration : 0.1f));
            return result2;
        }
        finally
        {
            if (viewGO != null) Addressables.ReleaseInstance(viewGO);
        }
    }

    // ---------- Addressable CANVAS (its own blocker + preplaced view) ----------
    private async UniTask<PopupResult> ShowUsingAddressableCanvasAsync(PopupRequest req)
    {
        if (string.IsNullOrEmpty(req.AddressableCanvasKey))
        {
            Debug.LogError("PopupService: AddressableCanvasKey is empty.");
            return PopupResult.None;
        }

        if (!_canvasCache.TryGetValue(req.AddressableCanvasKey, out var canvasInst))
        {
            canvasInst = await Addressables.InstantiateAsync(req.AddressableCanvasKey).Task;
            projectContainer.Inject(canvasInst);
            if (req.CacheTemplate)
            {
                _canvasCache[req.AddressableCanvasKey] = canvasInst;
                DontDestroyOnLoad(canvasInst);
            }
        }

        // Try content first
        var content = canvasInst.GetComponentInChildren<IPopupContent>(true);
        var blocker = canvasInst.GetComponentInChildren<ClickBlocker>(true);

        IDisposable scope = null;
        if (req.IsModal && blocker != null)
            scope = await blocker.BlockScopeAsync("Popup");

        try
        {
            if (content != null)
            {
                // Let the prefab handle all Confirm/Cancel and close the popup by resolving tcs
                var tcs = new UniTaskCompletionSource<PopupResult>();
                (content as Component)?.gameObject.SetActive(true);
                content.Bind(req, tcs);
                var result = await tcs.Task;

                scope?.Dispose();
                await UniTask.Delay(TimeSpan.FromSeconds(blocker ? blocker.FadeDuration : 0.1f));
                return result;
            }

            // Else fallback to a preplaced PopupView in the canvas
            var preplacedView = canvasInst.GetComponentInChildren<PopupView>(true);
            if (!preplacedView)
            {
                Debug.LogError($"PopupService: Canvas '{req.AddressableCanvasKey}' has neither IPopupContent nor PopupView.");
                return PopupResult.None;
            }

            // Reuse InternalShowAsync with a temp channel
            var root = preplacedView.transform.parent;
            var tempChannel = new PopupChannel {
                Id = req.AddressableCanvasKey,
                PopupRoot = root,
                PopupPrefab = null,
                Blocker = blocker,
                PreplacedView = preplacedView
            };

            // Ensure active to participate in fade
            preplacedView.gameObject.SetActive(true);
            preplacedView.gameObject.SetActive(false);

            var result2 = await InternalShowAsync(req, tempChannel, usePreplacedOrPrefab: true);
            return result2;
        }
        finally
        {
            // release if not cached
            if (!req.CacheTemplate)
            {
                Destroy(canvasInst);
                Addressables.ReleaseInstance(canvasInst);
                _canvasCache.Remove(req.AddressableCanvasKey);
            }
        }
    }
    
    private async UniTask<PopupResult> ShowUsingGameObjectAsync(PopupRequest req)
    {
        // Try content first
        var content = req.gm.GetComponentInChildren<IPopupContent>(true);
        var blocker = req.gm.GetComponentInChildren<ClickBlocker>(true);

        IDisposable scope = null;
        if (req.IsModal && blocker != null)
            scope = await blocker.BlockScopeAsync("Popup");

        try
        {
            if (content != null)
            {
                // Let the prefab handle all Confirm/Cancel and close the popup by resolving tcs
                var tcs = new UniTaskCompletionSource<PopupResult>();
                (content as Component)?.gameObject.SetActive(true);
                content.Bind(req, tcs);
                var result = await tcs.Task;

                scope?.Dispose();
                await UniTask.Delay(TimeSpan.FromSeconds(blocker ? blocker.FadeDuration : 0.1f));
                return result;
            }

            // Else fallback to a preplaced PopupView in the canvas
            var preplacedView = req.gm.GetComponentInChildren<PopupView>(true);
            if (!preplacedView)
            {
                Debug.LogError($"PopupService: Canvas '{req.AddressableCanvasKey}' has neither IPopupContent nor PopupView.");
                return PopupResult.None;
            }

            // Reuse InternalShowAsync with a temp channel
            var root = preplacedView.transform.parent;
            var tempChannel = new PopupChannel {
                Id = req.gm.GetInstanceID().ToString(),
                PopupRoot = root,
                PopupPrefab = null,
                Blocker = blocker,
                PreplacedView = preplacedView
            };

            // Ensure active to participate in fade
            preplacedView.gameObject.SetActive(true);
            preplacedView.gameObject.SetActive(false);

            var result2 = await InternalShowAsync(req, tempChannel, usePreplacedOrPrefab: true);
            return result2;
        }
        finally
        {
            // release if not cached
            if (!req.CacheTemplate)
            {
                //Destroy(canvasInst);
                //Addressables.ReleaseInstance(canvasInst);
                _canvasCache.Remove(req.gm.GetInstanceID().ToString());
            }
        }
    }

    // ---------- Default internal show (preplaced or prefab under channel) ----------
    private async UniTask<PopupResult> InternalShowAsync(PopupRequest req, PopupChannel channel, bool usePreplacedOrPrefab)
    {
        IDisposable scope = null;

        // Activate view BEFORE fade-in (so it fades with parent group)
        PopupView view = channel.PreplacedView;
        GameObject toDestroy = null;

        if (!usePreplacedOrPrefab || view == null)
        {
            // Instantiate the channel’s default prefab
            if (channel.PopupPrefab == null)
            {
                Debug.LogError("PopupService: channel has no PopupPrefab and no PreplacedView.");
                return PopupResult.None;
            }
            var viewObj = Instantiate(channel.PopupPrefab, channel.PopupRoot);
            view = viewObj.GetComponent<PopupView>();
            toDestroy = viewObj.gameObject;
        }

        // Ensure it’s active so it participates in the blocker fade
        view.gameObject.SetActive(true);

        if (req.IsModal && channel.Blocker != null)
            scope = await channel.Blocker.BlockScopeAsync("Popup");

        var tcs = new UniTaskCompletionSource<PopupResult>();
        var title = ApplyTokens(req.Title, req.Tokens);
        var message = ApplyTokens(req.Message, req.Tokens);
        view.Bind(title, message, req.Icon, req.Style, req.Buttons, tcs);

        if (req.AutoDismissSeconds.HasValue)
            _ = AutoDismiss(req.AutoDismissSeconds.Value, tcs);

        var result = await tcs.Task;

        // Fade out (overlay drives parent CanvasGroup fade)
        scope?.Dispose();
        await UniTask.Delay(TimeSpan.FromSeconds(channel.Blocker ? channel.Blocker.FadeDuration : 0.1f));

        // Hide / cleanup
        if (channel.PreplacedView != null)
            channel.PreplacedView.gameObject.SetActive(false);
        if (toDestroy != null)
            Destroy(toDestroy);

        return result;
    }

    private async UniTaskVoid AutoDismiss(float seconds, UniTaskCompletionSource<PopupResult> tcs)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(seconds));
        tcs.TrySetResult(PopupResult.Closed);
    }

    private static string ApplyTokens(string input, Dictionary<string, object> tokens)
    {
        if (string.IsNullOrEmpty(input) || tokens == null) return input;
        foreach (var kv in tokens) input = input.Replace($"{{{kv.Key}}}", kv.Value?.ToString());
        return input;
    }
}

[Serializable]
public class PopupChannel
{
    public string Id;
    public Transform PopupRoot;
    public PopupView PopupPrefab;      // default view (optional if using preplaced)
    public ClickBlocker Blocker;    // drives fade + blocks clicks
    public PopupView PreplacedView;    // if the canvas has a PopupView already inside
}
