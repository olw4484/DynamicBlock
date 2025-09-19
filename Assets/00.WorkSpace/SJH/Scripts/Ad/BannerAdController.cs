using GoogleMobileAds.Api;
using UnityEngine;
using System.Collections.Generic;

public class BannerAdController
{
#if UNITY_ANDROID
    private const string TEST_BANNER = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_IOS
    private const string TEST_BANNER = "ca-app-pub-3940256099942544/2934735716";
#else
    private const string TEST_BANNER = "unexpected_platform";
#endif

    // ▶ 테스트/개발 빌드: 테스트ID, 릴리스: AdIds.Banner
    private string BannerUnitId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_BANNER;
#else
        AdIds.Banner;
#endif

    private BannerView _banner;
    private bool _isLoaded;
    private bool _isShown;

    public bool IsVisible => _isShown;
    public bool IsLoaded => _isLoaded;

    // 테스트 디바이스 등록 — 앱 시작 1회
    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = (testDeviceIds == null) ? null : new List<string>(testDeviceIds);
        var conf = new RequestConfiguration.Builder().SetTestDeviceIds(list).build();
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    // --- 초기화 & 로드 ---
    public void Init(bool useAdaptive = false, AdPosition pos = AdPosition.Bottom)
    {
        DestroyAd();

        AdSize size = useAdaptive ? GetAdaptiveSize() : AdSize.Banner;

        _banner = new BannerView(BannerUnitId, size, pos);
        HookEvents(_banner);

        _isLoaded = false;
        _isShown = false;

        Debug.Log($"[Banner] Init & Load... id={BannerUnitId}, size={size}, pos={pos}");
        _banner.LoadAd(new AdRequest());
    }

    // Adaptive 배너 사이즈 계산 (현재 화면 폭 기준)
    private AdSize GetAdaptiveSize()
    {
#if UNITY_ANDROID || UNITY_IOS
        int width = Screen.width; // px
        // DPI 변환 없이도 AdMob가 내부적으로 맞춰줌. 필요시 dp 변환 추가 가능.
        return AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(width);
#else
        return AdSize.Banner;
#endif
    }

    // --- 표시/숨김/제거 ---
    public void ShowAd()
    {
        if (_banner == null) { Init(); return; }
        if (!_isLoaded) { _banner.LoadAd(new AdRequest()); return; }

        _banner.Show();
        _isShown = true;
        Debug.Log("[Banner] Show");
    }

    public void HideAd()
    {
        if (_banner == null) return;

        _banner.Hide();
        _isShown = false;
        Debug.Log("[Banner] Hide");
    }

    public void DestroyAd()
    {
        if (_banner == null) return;
        _banner.Destroy();
        _banner = null;
        _isLoaded = false;
        _isShown = false;
        Debug.Log("[Banner] Destroy");
    }

    // --- 이벤트 ---
    private void HookEvents(BannerView view)
    {
        if (view == null) return;

        view.OnBannerAdLoaded += () =>
        {
            _isLoaded = true;
            Debug.Log("[Banner] Loaded");
            // 자동 표시 원하면: ShowAd();
        };

        view.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            _isLoaded = false;
            Debug.LogError($"[Banner] Load failed: {error}");
            // 필요시 재시도 로직(지수 백오프) 추가 가능
        };

        view.OnAdPaid += (AdValue v) => Debug.Log($"[Banner] Paid: {v.CurrencyCode}/{v.Value}");
        view.OnAdImpressionRecorded += () => Debug.Log("[Banner] Impression");
        view.OnAdClicked += () => Debug.Log("[Banner] Click");
        view.OnAdFullScreenContentOpened += () => Debug.Log("[Banner] Fullscreen opened");
        view.OnAdFullScreenContentClosed += () => Debug.Log("[Banner] Fullscreen closed");
    }

    // 회전/해상도 변경 시 Adaptive 재생성 호출
    public void RecreateForOrientationChange()
    {
        if (_banner == null) return;
        bool wasShown = _isShown;
        Init(useAdaptive: true);
        if (wasShown) ShowAd();
    }
}
