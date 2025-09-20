using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;

public class BannerAdController
{
#if UNITY_ANDROID
    private const string TEST_BANNER = "ca-app-pub-3940256099942544/6300978111";
    private const string PROD_BANNER = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX";
#elif UNITY_IOS
    private const string TEST_BANNER = "ca-app-pub-3940256099942544/2934735716";
    private const string PROD_BANNER = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX";
#else
    private const string TEST_BANNER = "unexpected_platform";
    private const string PROD_BANNER = "unexpected_platform";
#endif

    private string BannerId =>
#if TEST_ADS || DEVELOPMENT_BUILD
    TEST_BANNER;
#else
        AdIds.Banner;
#endif

    private BannerView _view;
    bool _isLoaded;
    bool _isShown;

    public bool IsVisible => _isShown;
    public bool IsLoaded => _isLoaded;

    // 배너 클릭으로 전면(브라우저/웹뷰) 전환될 때 게임 일시정지/복구용
    public event Action FullOpened;
    public event Action FullClosed;

    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = (testDeviceIds == null) ? null : new List<string>(testDeviceIds);
        var conf = new RequestConfiguration { TestDeviceIds = list };
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    public void Init(AdSize size = null, AdPosition pos = AdPosition.Bottom)
    {
        if (_view != null)
        {
            _view.Destroy();
            _view = null;
        }

        var adSize = size ?? AdSize.Banner; // 필요시 Adaptive로 교체
        _view = new BannerView(BannerId, adSize, pos);

        HookEvents(_view);

        Debug.Log($"[Banner] Init & Load... id={BannerId}, size={adSize}, pos={pos}");
        _isLoaded = false;
        _isShown = false;
        _view.LoadAd(new AdRequest());
    }

    public void ShowAd()
    {
        if (_view == null) { Init(); return; }
        if (!_isLoaded) { _view.LoadAd(new AdRequest()); return; }

        _view.Show();
        _isShown = true;
        Debug.Log("[Banner] Show");
    }

    public void HideAd()
    {
        if (_view == null) return;
        _view.Hide();
        _isShown = false;
        Debug.Log("[Banner] Hide");
    }

    public void DestroyAd()
    {
        if (_view == null) return;
        _view.Destroy();
        _view = null;
        _isLoaded = false;
        _isShown = false;
    }

    void HookEvents(BannerView v)
    {
        if (v == null) return;

        v.OnBannerAdLoaded += () =>
        {
            _isLoaded = true;
            Debug.Log("[Banner] Loaded");
        };

        v.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            _isLoaded = false;
            Debug.LogError($"[Banner] Load failed: {error}");
        };

        v.OnAdPaid += (AdValue val) => Debug.Log($"[Banner] Paid: {val.CurrencyCode}/{val.Value}");
        v.OnAdImpressionRecorded += () => Debug.Log("[Banner] Impression recorded");
        v.OnAdClicked += () => Debug.Log("[Banner] Clicked");

        // 일부 단말/상황에서 배너 클릭 → 전체화면 컨텐츠가 열릴 수 있음
        v.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[Banner] Fullscreen opened");
            FullOpened?.Invoke();
        };
        v.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Banner] Fullscreen closed");
            FullClosed?.Invoke();
        };
    }
}
