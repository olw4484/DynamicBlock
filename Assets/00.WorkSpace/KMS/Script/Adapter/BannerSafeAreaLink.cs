using UnityEngine;

public class BannerSafeAreaLink : MonoBehaviour
{
    [SerializeField] SafeArea safeArea;  // 모달 루트의 SafeArea
    [SerializeField] bool applyBottom = true;

    BannerAdController banner;
    int lastW, lastH;
    bool resizing;
    bool binding;   // 중복 코루틴 방지

    void Awake()
    {
        if (safeArea == null) safeArea = GetComponentInChildren<SafeArea>(true);
        lastW = Screen.width; lastH = Screen.height;
    }

    void OnEnable()
    {
        // 배너 준비될 때까지 기다렸다가 바인딩
        TryBindBanner();
    }

    void OnDisable()
    {
        UnbindBanner();
    }

    void OnDestroy()
    {
        UnbindBanner();
    }

    void UnbindBanner()
    {
        if (banner != null)
        {
            banner.BannerHeightChangedPx -= OnBannerHeight;
            banner = null;
        }
    }

    void TryBindBanner()
    {
        if (binding) return;
        StartCoroutine(BindWhenReady());
    }

    System.Collections.IEnumerator BindWhenReady()
    {
        binding = true;

        // 기존 바인딩 해제
        UnbindBanner();

        // AdManager/배너가 생성될 때까지 프레임 단위로 대기
        while (AdManager.Instance == null || AdManager.Instance.Banner == null)
            yield return null;

        banner = AdManager.Instance.Banner;
        banner.BannerHeightChangedPx += OnBannerHeight;

        // 현재 높이 즉시 반영
        OnBannerHeight(banner.CurrentHeightPx);

        binding = false;
    }

    void OnBannerHeight(int px)
    {
        if (!applyBottom || safeArea == null) return;
        safeArea.SetExtraBottomPx(px);   // 여백만 조정
    }

    // 해상도/회전 변경 시 배너 측에 알림
    void OnRectTransformDimensionsChange()
    {
        if (Screen.width == lastW && Screen.height == lastH) return;
        if (resizing) return;
        lastW = Screen.width; lastH = Screen.height;
        StartCoroutine(DebouncedReinit());
    }

    System.Collections.IEnumerator DebouncedReinit()
    {
        resizing = true;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        // 배너가 아직 없으면 바인딩 재시도
        if (banner == null) TryBindBanner();
        else AdManager.Instance?.Banner?.OnOrientationOrResolutionChanged();

        resizing = false;
    }
}
