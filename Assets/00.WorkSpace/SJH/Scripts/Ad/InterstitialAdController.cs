using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class InterstitialAdController
{
#if UNITY_ANDROID
    private const string TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/1033173712";
#elif UNITY_IOS
    private const string TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/4411468910";
#else
    private const string TEST_INTERSTITIAL = "unexpected_platform";
#endif

    // 테스트/개발 빌드: 테스트ID, 릴리스: AdIds.Interstitial
    private string InterstitialId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_INTERSTITIAL;
#else
        AdIds.Interstitial;
#endif

    private static readonly TimeSpan CacheValidity = TimeSpan.FromHours(1);

    private InterstitialAd _loadedAd;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public bool IsReady { get; private set; }
    public event Action Closed;
    public event Action Failed;

    // 테스트 디바이스 등록 - 앱 시작 시 1회
    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = testDeviceIds == null ? null : new System.Collections.Generic.List<string>(testDeviceIds);
        var conf = new RequestConfiguration.Builder().SetTestDeviceIds(list).build();
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    public void Init()
    {
        // 이미 로드되어 있고 유효하면 스킵
        if (_loadedAd != null && DateTime.UtcNow < _expiresAtUtc)
        {
            Debug.Log("[Interstitial] Already loaded & valid.");
            IsReady = true;
            return;
        }

        DestroyAd(); // 만료/없음 → 정리 후 재로드

        Debug.Log($"[Interstitial] Loading... id={InterstitialId}");
        InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"[Interstitial] Load failed: {error}");
                _loadedAd = null;
                IsReady = false;
                return;
            }

            _loadedAd = ad;
            _expiresAtUtc = DateTime.UtcNow + CacheValidity - TimeSpan.FromMinutes(1); // 살짝 여유
            IsReady = true;

            Debug.Log("[Interstitial] Load success.");
            HookEvents(_loadedAd);
        });
    }

    public void ShowAd()
    {
        // 빈도 제한
        if (AdManager.Instance.NextInterstitialTime > DateTime.UtcNow)
        {
            Debug.Log("[Interstitial] Skipped due to cooldown.");
            AdManager.Instance.NextInterstitialTime = DateTime.UtcNow; // 보정
            return;
        }

        // 유효성 체크
        if (_loadedAd == null || DateTime.UtcNow >= _expiresAtUtc || !_loadedAd.CanShowAd())
        {
            Debug.Log("[Interstitial] Not ready. Try load again.");
            Init();
            return;
        }

        _loadedAd.Show(); // 1로딩 1재생
    }

    public void DestroyAd()
    {
        if (_loadedAd == null) return;

        Debug.Log("[Interstitial] Destroy.");
        _loadedAd.Destroy();
        _loadedAd = null;
        IsReady = false;
        _expiresAtUtc = DateTime.MinValue;
    }

    private void HookEvents(InterstitialAd ad)
    {
        if (ad == null) return;

        ad.OnAdPaid += (AdValue v) => Debug.Log($"[Interstitial] Paid: {v.CurrencyCode}/{v.Value}");
        ad.OnAdImpressionRecorded += () => Debug.Log("[Interstitial] Impression recorded");
        ad.OnAdClicked += () => Debug.Log("[Interstitial] Clicked");

        ad.OnAdFullScreenContentOpened += () =>
            Debug.Log("[Interstitial] Opened");

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Interstitial] Closed");
            AdManager.Instance.NextInterstitialTime = DateTime.UtcNow.AddSeconds(AdManager.Instance.InterstitialTime);
            IsReady = false;
            Closed?.Invoke();
            Init(); // 다음 로드 예약
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"[Interstitial] Show error: {e}");
            IsReady = false;
            Failed?.Invoke();
            Init(); // 재시도
        };
    }
}
