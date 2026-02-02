using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class BootLoader : MonoBehaviour
{
    [SerializeField] private ErrorModalView errorModalView;
    [SerializeField] private LevelsProviderService levelsProviderService;
    void Awake()
    {
        // Register the view so ErrorModalService can use it
        ErrorModalService.Register(errorModalView);

        // Hook global Addressables error handler once
        AddressablesGlobalErrors.EnsureHook();
    }
}