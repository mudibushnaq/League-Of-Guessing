using System;
using UnityEngine;

public static class StreakSystem
{
    public const int MaxStreak = 5; // Penta

    // Tier windows (seconds): 1->2, 2->3, 3->4, 4->5
    public static float[] WindowSecondsByTier = { 0f, 45f, 35f, 30f, 25f };

    private static int _currentTier;          // 0..5 (session only)
    private static DateTime? _deadlineUtc;    // null if no active window (tier==0 or ==5)

    /// Call this AFTER a level is solved; returns new tier (1..5) and sets next deadline if tier<5.
    public static int BumpAndSetDeadline()
    {
        _currentTier = Mathf.Clamp(_currentTier + 1, 1, MaxStreak);

        if (_currentTier < MaxStreak)
        {
            float window = GetWindowSecondsForCurrentTier();
            _deadlineUtc = DateTime.UtcNow.AddSeconds(window);
        }
        else
        {
            _deadlineUtc = null; // no next step after penta
        }
        return _currentTier;
    }

    public static void Reset()
    {
        _currentTier = 0;
        _deadlineUtc = null;
    }

    public static int GetCurrentTier() => _currentTier;

    public static float GetWindowSecondsForCurrentTier()
    {
        int t = Mathf.Clamp(_currentTier, 1, MaxStreak);
        if (t >= MaxStreak) return 0f;
        return WindowSecondsByTier[Mathf.Clamp(t, 1, WindowSecondsByTier.Length - 1)];
    }

    /// Remaining seconds until chain breaks; <=0 means expired/no window.
    public static float GetRemainingSeconds()
    {
        if (_deadlineUtc == null) return -1f;
        return (float)((DateTime.UtcNow - _deadlineUtc.Value).TotalSeconds * -1.0);
    }
    
    /// Cancel/hide the window (e.g., during transitions).
    public static void ClearWindow()
    {
        _deadlineUtc = null;
    }
    
    /// Start the next countdown window for the current tier (if < Max).
    public static void StartWindowForNextStep()
    {
        if (_currentTier <= 0 || _currentTier >= MaxStreak) { _deadlineUtc = null; return; }
        float window = GetWindowSecondsForCurrentTier();
        _deadlineUtc = DateTime.UtcNow.AddSeconds(window);
    }
}