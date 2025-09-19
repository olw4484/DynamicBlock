using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class RewardAdController
{
#if UNITY_ANDROID
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/1712485313";
#else
    private const string TEST_REWARDED = "unexpected_platform";
#endif

    private string RewardId =>
#if TEST_ADS || DEVELOPMENT_BUILD
        TEST_REWARDED;
#else
        AdIds.Rewarded;
#endif

    private RewardedAd _loadedAd;
    private Action _externalOnReward;

    public bool IsReady { get; private set; }
    public event Action Rewarded;
    public event Action Closed;
    public event Action Failed;

    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = (testDeviceIds == null) ? null : new System.Collections.Generic.List<string>(testDeviceIds);
        var conf = new RequestConfiguration.Builder()
            .SetTestDeviceIds(list)
            .build();
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

            _loadedAd = ad;
            IsReady = true;
            Debug.Log("[Rewarded] Load success");
            HookEvents(_loadedAd);
        });
    }

    public void DestroyAd()
    {
        if (_loadedAd == null) return;
        _loadedAd.Destroy();
        _loadedAd = null;
        IsReady = false;
        _externalOnReward = null;
    }

    public void ShowAd(Action onReward = null)
    {
        if (_loadedAd == null || !_loadedAd.CanShowAd() || !IsReady)
        {
            Init();
            return;
        }

        _externalOnReward = onReward;
        IsReady = false;

        _loadedAd.Show(reward =>
        {
            Rewarded?.Invoke();
            _externalOnReward?.Invoke();
        });
    }

    private void HookEvents(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdFullScreenContentClosed += () =>
        {
            Closed?.Invoke();
            _externalOnReward = null;
            IsReady = false;
            Init();
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Failed?.Invoke();
            _externalOnReward = null;
            IsReady = false;
            Init();
        };
    }
}
