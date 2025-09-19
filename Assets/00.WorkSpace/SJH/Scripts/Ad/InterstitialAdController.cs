using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class InterstitialAdController
{
    // ------------------------------
    // 분기: 테스트/실광고 단위 ID
    // ------------------------------
#if UNITY_ANDROID
    private const string TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/1033173712";
    private const string PROD_INTERSTITIAL = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX"; // TODO: 실제 ID 입력
#elif UNITY_IOS
    private const string TEST_INTERSTITIAL = "ca-app-pub-3940256099942544/4411468910";
    private const string PROD_INTERSTITIAL = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX"; // TODO: 실제 ID 입력
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

    // 1시간 유효 (AdMob 가이드)
    private static readonly TimeSpan CacheValidity = TimeSpan.FromHours(1);

    private InterstitialAd _loadedAd;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public bool IsReady { get; private set; }
    public event Action Closed;
    public event Action Failed;

    // 앱 시작시 한 번, 혹은 AdManager에서 글로벌로 호출 권장
    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
        // 내부테스트에서 실광고 노출 방지(이중 안전장치)
#if TEST_ADS || DEVELOPMENT_BUILD
        var conf = new RequestConfiguration.Builder()
            .SetTestDeviceIds(testDeviceIds == null ? null : new System.Collections.Generic.List<string>(testDeviceIds))
            .build();
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    public void Init()
    {
        // 유효하면 스킵
        if (_loadedAd != null && DateTime.UtcNow < _expiresAtUtc)
        {
            Debug.Log("[Interstitial] Already loaded & valid.");
            IsReady = true;
            return;
        }

        // 만료/없음 → 정리 후 재로드
        DestroyAd();

        Debug.Log($"[Interstitial] Loading... id={InterstitialId}");

        InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"[Interstitial] Load failed: {error}");
                _loadedAd = null;
                IsReady = false;
                return;
            }
            if (ad == null)
            {
                Debug.LogError("[Interstitial] Load callback with null ad.");
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
            // 스킵 시 다음 기회에 즉시 가능하도록 보정
            AdManager.Instance.NextInterstitialTime = DateTime.UtcNow;
            return;
        }

        // 유효성 체크(만료 시 즉시 재로드)
        if (_loadedAd == null || DateTime.UtcNow >= _expiresAtUtc || !_loadedAd.CanShowAd())
        {
            Debug.Log("[Interstitial] Not ready. Try load again.");
            Init();
            return;
        }

        _loadedAd.Show(); // 1로딩 1재생 원칙
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
        {
            Debug.Log("[Interstitial] Opened");
        };

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
