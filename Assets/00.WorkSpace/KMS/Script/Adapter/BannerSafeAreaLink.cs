using UnityEngine;

public class BannerSafeAreaLink : MonoBehaviour
{
    [SerializeField] SafeArea safeArea;  // ��� ��Ʈ�� SafeArea
    [SerializeField] bool applyBottom = true;

    BannerAdController banner;
    int lastW, lastH;
    bool resizing;
    bool binding;   // �ߺ� �ڷ�ƾ ����

    void Awake()
    {
        if (safeArea == null) safeArea = GetComponentInChildren<SafeArea>(true);
        lastW = Screen.width; lastH = Screen.height;
    }

    void OnEnable()
    {
        // ��� �غ�� ������ ��ٷȴٰ� ���ε�
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

        // ���� ���ε� ����
        UnbindBanner();

        // AdManager/��ʰ� ������ ������ ������ ������ ���
        while (AdManager.Instance == null || AdManager.Instance.Banner == null)
            yield return null;

        banner = AdManager.Instance.Banner;
        banner.BannerHeightChangedPx += OnBannerHeight;

        // ���� ���� ��� �ݿ�
        OnBannerHeight(banner.CurrentHeightPx);

        binding = false;
    }

    void OnBannerHeight(int px)
    {
        if (!applyBottom || safeArea == null) return;
        safeArea.SetExtraBottomPx(px);   // ���鸸 ����
    }

    // �ػ�/ȸ�� ���� �� ��� ���� �˸�
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

        // ��ʰ� ���� ������ ���ε� ��õ�
        if (banner == null) TryBindBanner();
        else AdManager.Instance?.Banner?.OnOrientationOrResolutionChanged();

        resizing = false;
    }
}
