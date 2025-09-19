using GoogleMobileAds.Api;
using UnityEngine;
using System.Collections.Generic;

public class BannerAdController
{
    // ==========================
    // 1) 유닛ID 분기
    // ==========================
#if UNITY_ANDROID
    private const string TEST_BANNER = "ca-app-pub-3940256099942544/6300978111";
    private const string PROD_BANNER = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX"; // TODO: 실제 ID
#elif UNITY_IOS
    private const string TEST_BANNER = "ca-app-pub-3940256099942544/2934735716";
    private const string PROD_BANNER = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX"; // TODO: 실제 ID
#else
    private const string TEST_BANNER = "unexpected_platform";
    private const string PROD_BANNER = "unexpected_platform";
#endif

    private string BannerId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_BANNER;
#else
        PROD_BANNER;
#endif

    // ==========================
    // 2) 상태
    // ==========================
    private BannerView _loadedAd;
    private bool _isLoaded;
    private bool _isShown;

    public bool IsVisible => _isShown;
    public bool IsLoaded => _isLoaded;

    // ==========================
    // (선택) 테스트 기기 설정
    // 앱 시작 시 1회 호출
    // ==========================
    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
    var list = (testDeviceIds == null) ? null : new List<string>(testDeviceIds);
    var conf = new RequestConfiguration
    {
        TestDeviceIds = list
    };
    MobileAds.SetRequestConfiguration(conf);
#endif
    }

    // ==========================
    // 3) 초기화 & 로드
    // ==========================
    public void Init(AdSize size = null, AdPosition pos = AdPosition.Bottom)
    {
        // 중복 생성 방지
        if (_loadedAd != null)
        {
            _loadedAd.Destroy();
            _loadedAd = null;
        }

        var adSize = size ?? AdSize.Banner; // 필요시 Adaptive로 교체 가능
        _loadedAd = new BannerView(BannerId, adSize, pos);

        HookEvents(_loadedAd);

        Debug.Log($"[Banner] Init & Load... id={BannerId}, size={adSize}, pos={pos}");
        _isLoaded = false;
        _isShown = false;
        _loadedAd.LoadAd(new AdRequest());
    }

    // ==========================
    // 4) 토글 / 표시 / 숨김 / 제거
    // ==========================
    public void AdToggle()
    {
        if (_isShown) HideAd();
        else ShowAd();
    }

    public void ShowAd()
    {
        if (_loadedAd == null) { Init(); return; }
        if (!_isLoaded) { _loadedAd.LoadAd(new AdRequest()); return; }

        _loadedAd.Show();
        _isShown = true;
        Debug.Log("[Banner] Show");
    }

    public void HideAd()
    {
        if (_loadedAd == null) return;

        _loadedAd.Hide();
        _isShown = false;
        Debug.Log("[Banner] Hide");
    }

    public void DestroyAd()
    {
        if (_loadedAd == null) return;

        Debug.Log("[Banner] Destroy");
        _loadedAd.Destroy();
        _loadedAd = null;
        _isLoaded = false;
        _isShown = false;
    }

    // ==========================
    // 5) 이벤트 연결
    // ==========================
    private void HookEvents(BannerView view)
    {
        if (view == null) return;

        view.OnBannerAdLoaded += () =>
        {
            _isLoaded = true;
            Debug.Log("[Banner] Loaded");
            // 자동 표시를 원하면: ShowAd();
        };

        view.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            _isLoaded = false;
            Debug.LogError($"[Banner] Load failed: {error}");
            // 재시도 전략이 필요하면 여기서 처리 가능 (지수 백오프 등)
        };

        view.OnAdPaid += (AdValue v) =>
            Debug.Log($"[Banner] Paid: {v.CurrencyCode}/{v.Value}");

        view.OnAdImpressionRecorded += () =>
            Debug.Log("[Banner] Impression recorded");

        view.OnAdClicked += () =>
            Debug.Log("[Banner] Clicked");

        view.OnAdFullScreenContentOpened += () =>
            Debug.Log("[Banner] Fullscreen opened");

        view.OnAdFullScreenContentClosed += () =>
            Debug.Log("[Banner] Fullscreen closed");
    }
}
