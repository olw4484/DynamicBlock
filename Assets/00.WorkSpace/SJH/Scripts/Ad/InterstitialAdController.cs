using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class InterstitialAdController
{
#if UNITY_ANDROID
    private const string TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/1033173712";
    private const string PROD_INTERSTITIAL = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX";
#elif UNITY_IOS
    private const string TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/4411468910";
    private const string PROD_INTERSTITIAL = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX";
#else
    private const string TEST_INTERSTITIAL = "unexpected_platform";
    private const string PROD_INTERSTITIAL = "unexpected_platform";
#endif

    private string InterstitialId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_INTERSTITIAL;
#else
        PROD_INTERSTITIAL;
#endif

    private static readonly TimeSpan CacheValidity = TimeSpan.FromHours(1);

    private InterstitialAd _ad;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public bool IsReady { get; private set; }

    public event Action Opened;
    public event Action Closed;
    public event Action Failed;

    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var conf = new RequestConfiguration
        {
            TestDeviceIds = (testDeviceIds == null) ? null : new System.Collections.Generic.List<string>(testDeviceIds)
        };
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    public void Init()
    {
        if (_ad != null && DateTime.UtcNow < _expiresAtUtc)
        {
            IsReady = true;
            return;
        }

        DestroyAd();
        Debug.Log($"[Interstitial] Loading... id={InterstitialId}");

        InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"[Interstitial] Load failed: {error}");
                _ad = null;
                IsReady = false;
                return;
            }

            _ad = ad;
            _expiresAtUtc = DateTime.UtcNow + CacheValidity - TimeSpan.FromMinutes(1);
            IsReady = true;
            Debug.Log("[Interstitial] Load success.");
            HookEvents(_ad);
        });
    }

    public void ShowAd()
    {
        // 자동노출 쿨다운은 AdManager에서 관리하지만, 여기서도 한번 더 방어
        if (AdManager.Instance != null && AdManager.Instance.NextInterstitialTime > DateTime.UtcNow)
        {
            Debug.Log("[Interstitial] Skipped due to cooldown.");
            AdManager.Instance.NextInterstitialTime = DateTime.UtcNow; // 보정
            return;
        }

        if (_ad == null || DateTime.UtcNow >= _expiresAtUtc || !_ad.CanShowAd())
        {
            Debug.Log("[Interstitial] Not ready. Try load again.");
            Init();
            return;
        }

        _ad.Show();
    }

    public void DestroyAd()
    {
        if (_ad == null) return;
        _ad.Destroy();
        _ad = null;
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
        {
            Debug.Log("[Interstitial] Opened");
            Opened?.Invoke();
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Interstitial] Closed");
            IsReady = false;
            Closed?.Invoke();
            Init(); // 다음 로드
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
