using UnityEngine;

public class BannerSafeAreaLink : MonoBehaviour
{
    [SerializeField] SafeArea safeArea;  // 모달 루트의 SafeArea
    [SerializeField] bool applyBottom = true;

    BannerAdController banner;
    int lastW, lastH;
    bool resizing;

    void Awake()
    {
        if (safeArea == null) safeArea = GetComponentInChildren<SafeArea>(true);
        banner = AdManager.Instance?.Banner; // 전역 하나 참조
        if (banner != null) banner.BannerHeightChangedPx += OnBannerHeight;
        lastW = Screen.width; lastH = Screen.height;
    }

    void OnEnable()
    {
        // 현재 값 즉시 반영
        OnBannerHeight(banner != null ? banner.CurrentHeightPx : 0);
    }

    void OnDestroy()
    {
        if (banner != null) banner.BannerHeightChangedPx -= OnBannerHeight;
    }

    void OnBannerHeight(int px)
    {
        if (!applyBottom || safeArea == null) return;
        safeArea.SetExtraBottomPx(px);   // 여백만 조정 (배너 재요청 없음)
    }

    // 실제 해상도 변경시에만 디바운스 재요청
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
        AdManager.Instance?.Banner?.OnOrientationOrResolutionChanged();
        resizing = false;
    }
}
