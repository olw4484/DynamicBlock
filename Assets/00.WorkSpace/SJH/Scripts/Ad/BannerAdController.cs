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

    public int CurrentHeightPx { get; private set; } = 0;

    BannerView _view;
    bool _isLoaded;
    bool _isShown;

    public bool IsVisible => _isShown;
    public bool IsLoaded => _isLoaded;

    // 배너 높이(px) 변경 시 알림 → UI가 SafeArea 보정
    public event Action<int> BannerHeightChangedPx;

    // 배너 클릭 → 전체화면 열림/닫힘
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

    /// <summary>
    /// Adaptive 배너로 초기화 (권장)
    /// </summary>
    public void InitAdaptive(AdPosition pos = AdPosition.Bottom)
    {
        InitInternal(GetAdaptiveSize(), pos);
    }

    /// <summary>
    /// (필요 시) 고정 사이즈로 초기화
    /// </summary>
    public void Init(AdSize size = null, AdPosition pos = AdPosition.Bottom)
    {
        InitInternal(size ?? AdSize.Banner, pos);
    }

    void InitInternal(AdSize size, AdPosition pos)
    {
        // 초기화 보장
        AdsInitGate.EnsureInit();

        if (!AdsInitGate.Ready)
        {
            Debug.Log("[Banner] Wait for MobileAds initialization...");
            // 초기화가 끝나면 같은 파라미터로 재진입
            AdsInitGate.WhenReady(() => InitInternal(size, pos));
            return;
        }

        // 여기부터 안전 구간
        _view?.Destroy();
        _isLoaded = false;
        _isShown = false;

        _view = new BannerView(BannerId, size, pos);
        HookEvents(_view);

        Debug.Log($"[Banner] Init & Load... id={BannerId}, size={size}, pos={pos}");
        _view.LoadAd(new AdRequest());
    }

    AdSize GetAdaptiveSize()
    {
        // 현재 방향 기준, 가로폭 맞춘 Adaptive
        return AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
    }

    public void ShowAd()
    {
        if (_view == null) { InitAdaptive(); return; }
        if (!_isLoaded) { _view.LoadAd(new AdRequest()); return; }

        _view.Show();
        _isShown = true;

        // 이미 로드된 상태라면 표시 직후 높이 다시 전달
        NotifyHeightChanged();
        Debug.Log("[Banner] Show");
    }

    public void HideAd()
    {
        if (_view == null) return;
        _view.Hide();
        _isShown = false;
        BannerHeightChangedPx?.Invoke(0); // 여백 해제
        Debug.Log("[Banner] Hide");
    }

    public void DestroyAd()
    {
        if (_view == null) return;
        _view.Destroy();
        _view = null;
        _isLoaded = false;
        _isShown = false;
        BannerHeightChangedPx?.Invoke(0);
    }

    void HookEvents(BannerView v)
    {
        if (v == null) return;

        v.OnBannerAdLoaded += () =>
        {
            _isLoaded = true;
            Debug.Log("[Banner] Loaded");
            NotifyHeightChanged();
        };

        v.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            _isLoaded = false;
            Debug.LogError($"[Banner] Load failed: {error}");
            BannerHeightChangedPx?.Invoke(0);
        };

        v.OnAdPaid += (AdValue val) => Debug.Log($"[Banner] Paid: {val.CurrencyCode}/{val.Value}");
        v.OnAdImpressionRecorded += () => Debug.Log("[Banner] Impression recorded");
        v.OnAdClicked += () => Debug.Log("[Banner] Clicked");

        v.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[Banner] Fullscreen opened");
            FullOpened?.Invoke();
        };
        v.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Banner] Fullscreen closed");
            FullClosed?.Invoke();
            // 복귀 시 레이아웃 재적용
            NotifyHeightChanged();
        };
    }

    void NotifyHeightChanged()
    {
        if (_view == null || !_isLoaded)
        {
            CurrentHeightPx = 0;
            BannerHeightChangedPx?.Invoke(0);
            return;
        }

        float h = _view.GetHeightInPixels();
        int hPx = Mathf.Clamp(Mathf.CeilToInt(h), 0, Screen.height);

        CurrentHeightPx = hPx;
        BannerHeightChangedPx?.Invoke(hPx);
    }

    /// <summary>
    /// 회전/해상도 변화 시 호출: Adaptive 사이즈 갱신 + 높이 재통지
    /// </summary>
    public void OnOrientationOrResolutionChanged()
    {
        if (_view == null) return;

        // Adaptive 새 크기로 갈아끼움이 가장 깔끔
        bool wasShown = _isShown;
        DestroyAd();
        InitAdaptive();         // 로드 후 OnLoaded에서 높이 통지
        if (wasShown) ShowAd(); // 자동 표출 유지
    }
}
