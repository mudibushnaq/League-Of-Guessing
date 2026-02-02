// ISettingsService.cs
using System;

public interface ISettingsService
{
    float BgmVolume01 { get; set; }   // 0..1
    float SfxVolume01 { get; set; }   // 0..1
    bool  VibrationEnabled { get; set; }

    event Action<float> OnBgmVolumeChanged;
    event Action<float> OnSfxVolumeChanged;
    event Action<bool>  OnVibrationChanged;
}

// ISocialVisitService.cs
public enum SocialKind { Facebook, Twitch, Instagram, Discord }

public interface ISocialVisitService
{
    bool Open(SocialKind kind); // returns false if not configured
}