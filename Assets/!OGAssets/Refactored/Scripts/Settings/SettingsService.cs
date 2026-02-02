// SettingsService.cs
#nullable enable
using System;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using UnityEngine;
using Zenject;

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(ISettingsService))]
public sealed class SettingsService : ISettingsService, IProjectInitializable
{
    int IProjectInitializable.Order => -100;
    
    // Persist only vibration here (your AudioManager already persists volumes)
    [SerializeField] string ppVibration = "SET_VIBRATION";

    [Inject(Optional = true)] private IAudioService _audio = null!;

    public event Action<float>? OnBgmVolumeChanged;
    public event Action<float>? OnSfxVolumeChanged;
    public event Action<bool>?  OnVibrationChanged;

    public float BgmVolume01
    {
        get => _audio != null ? _audio.BgmVolume01 : 1f;
        set { value = Mathf.Clamp01(value); if (_audio != null) _audio.BgmVolume01 = value; OnBgmVolumeChanged?.Invoke(value); }
    }

    public float SfxVolume01
    {
        get => _audio != null ? _audio.SfxVolume01 : 1f;
        set { value = Mathf.Clamp01(value); if (_audio != null) _audio.SfxVolume01 = value; OnSfxVolumeChanged?.Invoke(value); }
    }

    public bool VibrationEnabled
    {
        get => PlayerPrefs.GetInt(ppVibration, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(ppVibration, value ? 1 : 0);
            PlayerPrefs.Save();
            OnVibrationChanged?.Invoke(value);
#if UNITY_ANDROID || UNITY_IOS
            if (value) Handheld.Vibrate(); // tiny confirmation buzz
#endif
        }
    }

    UniTask IProjectInitializable.Initialize()
    {
        return UniTask.CompletedTask;
    }
}