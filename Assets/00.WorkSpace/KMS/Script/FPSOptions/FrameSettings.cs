using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public enum FrameOption { Low30 = 0, High60 = 1, Max = 2 }

public static class FrameSettings
{
    const string Key = "frame_option";

    public static FrameOption Current
    {
        get => (FrameOption)PlayerPrefs.GetInt(Key, (int)FrameOption.High60);
        set { PlayerPrefs.SetInt(Key, (int)value); Apply(value); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => Apply(Current);

    public static void Apply(FrameOption option)
    {
        // vSync 끄고 프레임 반토막 방지
        QualitySettings.vSyncCount = 0;
        OnDemandRendering.renderFrameInterval = 1;

#if UNITY_ANDROID || UNITY_IOS
        int target = option switch
        {
            FrameOption.Low30 => 30,
            FrameOption.High60 => 60,
            FrameOption.Max => RequestHighestRefreshRate(allowResolutionChange: false), // 필요시 true
            _ => 60
        };

        if (target <= 0) target = SafeCurrentHz(); // NaN/미확인 폴백
        Application.targetFrameRate = target;

        Debug.Log($"[FrameSettings] Applied: {option}, target={target}, " +
                  $"current={Screen.currentResolution.width}x{Screen.currentResolution.height}@{SafeCurrentHz()}Hz");
#else
        // 에디터/PC: MAX는 모니터 주사율(vSync), 그 외는 타겟 고정
        if (option == FrameOption.Max)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            Debug.Log($"[FrameSettings] Applied: Max (vSync), monitor≈{SafeCurrentHz()}Hz");
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (option == FrameOption.Low30) ? 30 : 60;
            Debug.Log($"[FrameSettings] Applied: {option}, target={Application.targetFrameRate}");
        }
#endif
    }

#if UNITY_2021_2_OR_NEWER
    static int RequestHighestRefreshRate(bool allowResolutionChange)
    {
        int curW = Screen.currentResolution.width;
        int curH = Screen.currentResolution.height;

        var modes = Screen.resolutions;
        if (modes == null || modes.Length == 0) return SafeCurrentHz();

        // 해상도를 바꾸고 싶지 않다면 같은 해상도만 후보
        var candidates = allowResolutionChange
            ? modes.AsEnumerable()
            : modes.Where(r => r.width == curW && r.height == curH);

        var best = candidates
            .OrderByDescending(r => RoundHz(r.refreshRateRatio))
            .FirstOrDefault();

        int bestHz = RoundHz(best.refreshRateRatio);
        if (bestHz <= 0) return SafeCurrentHz();

        // 같은 해상도면 주사율만 요청, 다르면 해상도 변경까지 요청
        if (allowResolutionChange && (best.width != curW || best.height != curH))
            Screen.SetResolution(best.width, best.height, FullScreenMode.FullScreenWindow, best.refreshRateRatio);
        else
            Screen.SetResolution(curW, curH, FullScreenMode.FullScreenWindow, best.refreshRateRatio);

        return bestHz;
    }
#else
    static int RequestHighestRefreshRate(bool _) => SafeCurrentHz();
#endif

    static int SafeCurrentHz()
    {
#if UNITY_2021_2_OR_NEWER
        float v = (float)Screen.currentResolution.refreshRateRatio.value;
        if (float.IsNaN(v) || float.IsInfinity(v) || v <= 0f) return 60;
        return Mathf.RoundToInt(v);
#else
        return Screen.currentResolution.refreshRate > 0 ? Screen.currentResolution.refreshRate : 60;
#endif
    }

#if UNITY_2021_2_OR_NEWER
    static int RoundHz(RefreshRate rr)
    {
        float v = (float)rr.value;
        if (float.IsNaN(v) || float.IsInfinity(v) || v <= 0f) return 0;
        return Mathf.RoundToInt(v);
    }
#endif

    public static void Reapply() => Apply(Current);
}
