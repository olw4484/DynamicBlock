using UnityEngine;

public class BannerSafeAreaLink : MonoBehaviour
{
    [SerializeField] SafeArea safeArea;  // ��� ��Ʈ�� SafeArea
    [SerializeField] bool applyBottom = true;

    BannerAdController banner;
    int lastW, lastH;
    bool resizing;

    void Awake()
    {
        if (safeArea == null) safeArea = GetComponentInChildren<SafeArea>(true);
        banner = AdManager.Instance?.Banner; // ���� �ϳ� ����
        if (banner != null) banner.BannerHeightChangedPx += OnBannerHeight;
        lastW = Screen.width; lastH = Screen.height;
    }

    void OnEnable()
    {
        // ���� �� ��� �ݿ�
        OnBannerHeight(banner != null ? banner.CurrentHeightPx : 0);
    }

    void OnDestroy()
    {
        if (banner != null) banner.BannerHeightChangedPx -= OnBannerHeight;
    }

    void OnBannerHeight(int px)
    {
        if (!applyBottom || safeArea == null) return;
        safeArea.SetExtraBottomPx(px);   // ���鸸 ���� (��� ���û ����)
    }

    // ���� �ػ� ����ÿ��� ��ٿ ���û
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
