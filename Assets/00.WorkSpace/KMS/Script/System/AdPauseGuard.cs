using UnityEngine;
using UnityEngine.EventSystems;

public static class AdPauseGuard
{
    public static bool IsAdShowing { get; private set; }
    public static bool PausedByAd { get; private set; }
    static float _suppressBackUntil;

    public static void OnAdOpened()
    {
        IsAdShowing = true;
        PausedByAd = true;
        _suppressBackUntil = Time.unscaledTime + 0.5f;

        Time.timeScale = 0f;
        AudioListener.pause = true;
#if !UNITY_EDITOR
        if (EventSystem.current) EventSystem.current.enabled = false;
#endif
    }

    public static void OnAdClosedOrFailed()
    {
        IsAdShowing = false;
        if (PausedByAd)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (EventSystem.current) EventSystem.current.enabled = true;
        }
        PausedByAd = false;
        _suppressBackUntil = Time.unscaledTime + 0.5f;
    }

    public static bool ShouldIgnoreBackNow()
        => IsAdShowing || Time.unscaledTime < _suppressBackUntil;
}
