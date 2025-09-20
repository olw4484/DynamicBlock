using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;
using UnityEngine;

public class RewardAdController
{
#if UNITY_ANDROID
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/5224354917";
    private const string PROD_REWARDED = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX";
#elif UNITY_IOS
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/1712485313";
    private const string PROD_REWARDED = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX";
#else
    private const string TEST_REWARDED = "unexpected_platform";
    private const string PROD_REWARDED = "unexpected_platform";
#endif

    private string RewardId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_REWARDED;
#else
        PROD_REWARDED;
#endif

    private RewardedAd _ad;
    private Action _externalOnReward;

    public bool IsReady { get; private set; }

    public event Action Opened;
    public event Action Closed;
    public event Action Failed;
    public event Action Rewarded;

    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = (testDeviceIds == null) ? null : new List<string>(testDeviceIds);
        var conf = new RequestConfiguration { TestDeviceIds = list };
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    public void Init()
    {
        DestroyAd();
        IsReady = false;

        Debug.Log("[Rewarded] Loading...");
        RewardedAd.Load(RewardId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"[Rewarded] Load failed: {error}");
                IsReady = false;
                return;
            }

            _ad = ad;
            IsReady = true;
            Debug.Log("[Rewarded] Load success");
            HookEvents(_ad);
        });
    }

    public void DestroyAd()
    {
        if (_ad == null) return;
        _ad.Destroy();
        _ad = null;
        _externalOnReward = null;
        IsReady = false;
    }

    // onReward는 AdManager에서 처리하므로 여기서는 null로 넘겨도 됨
    public bool ShowAd(Action onReward = null, bool ignoreCooldown = false)
    {
        // 자동노출(쿨다운) 정책은 AdManager에서 처리해도 되지만,
        // 혹시 남아있다면 방지
        if (!ignoreCooldown && AdManager.Instance != null &&
            AdManager.Instance.NextRewardTime > DateTime.UtcNow)
        {
            Debug.Log("[Rewarded] Skipped due to cooldown");
            Failed?.Invoke();
            return false;
        }

        if (_ad == null || !_ad.CanShowAd() || !IsReady)
        {
            Debug.Log("[Rewarded] Not ready → Load");
            Init();
            Failed?.Invoke();
            return false;
        }

        _externalOnReward = onReward;
        IsReady = false;

        _ad.Show(reward =>
        {
            Debug.Log($"[Rewarded] Granted: {reward.Type} x{reward.Amount}");
            try { Rewarded?.Invoke(); } catch { }
            try { _externalOnReward?.Invoke(); } catch { }
        });

        return true;
    }

    void HookEvents(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdPaid += (AdValue v) => Debug.Log($"[Rewarded] Paid: {v.CurrencyCode}/{v.Value}");
        ad.OnAdImpressionRecorded += () => Debug.Log("[Rewarded] Impression recorded");
        ad.OnAdClicked += () => Debug.Log("[Rewarded] Clicked");

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[Rewarded] Opened");
            Opened?.Invoke();
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Rewarded] Closed");
            Closed?.Invoke();
            _externalOnReward = null;
            IsReady = false;
            Init(); // 다음 로드
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"[Rewarded] Show error: {e}");
            Failed?.Invoke();
            _externalOnReward = null;
            IsReady = false;
            Init(); // 재시도
        };
    }
}
