using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AdStateProbe
{
    // 전면(Interstitial/Rewarded) 광고가 화면을 가리는 동안만 true
    public static bool IsFullscreenShowing { get; set; }

    // 부활 대기(리워드 수령/부활 처리 중)일 때 true
    public static bool IsRevivePending { get; set; }

    // 배너는 추적만(게이트에는 쓰지 않음)
    public static bool IsBannerShowing { get; set; }

    // [레거시 호환] 예전 코드가 IsAdShowing을 쓰더라도, 이제는 풀스크린만 의미하도록 유지
    public static bool IsAdShowing => IsFullscreenShowing;
}