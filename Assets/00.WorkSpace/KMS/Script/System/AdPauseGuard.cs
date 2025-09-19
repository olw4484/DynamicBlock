using UnityEngine;
using UnityEngine.EventSystems;

public static class AdPauseGuard
{
    public static bool IsAdShowing { get; private set; }
    static float _suppressBackUntil;

    public static void OnAdOpened()
    {
        IsAdShowing = true;
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

        Time.timeScale = 1f;
        AudioListener.pause = false;

        // � ��쿡�� �ٽ� ����(����)
        if (EventSystem.current) EventSystem.current.enabled = true;

        _suppressBackUntil = Time.unscaledTime + 0.5f; // ���� ���� ��Ű ����
    }

    public static bool ShouldIgnoreBackNow()
        => IsAdShowing || Time.unscaledTime < _suppressBackUntil;
}
