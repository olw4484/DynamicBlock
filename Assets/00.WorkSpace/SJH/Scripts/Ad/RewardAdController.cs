using GoogleMobileAds.Api;
using System;
using System.Collections;
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
        AdIds.Rewarded;
#endif

    private RewardedAd _ad;
    private Action _externalOnReward;

    public bool IsReady { get; private set; }
    public bool IsLoading { get; private set; }

    public event Action Opened;
    public event Action Closed;
    public event Action Failed;
    public event Action Rewarded;
    private bool _granted;

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
        if (IsLoading || IsReady) return;

        DestroyAd();
        IsReady = false;
        IsLoading = true;

        Debug.Log("[Rewarded] Loading start. isFocused=" + Application.isFocused);
        RewardedAd.Load(RewardId, new AdRequest(), (ad, error) =>
        {
            IsLoading = false;
            if (error != null || ad == null)
            {
                Debug.LogError($"[Rewarded] Load failed: {(error == null ? "null" : error)}");
                // 네트워크/단말/광고계정 이슈: 여기로 들어오면 '초기화/로드 실패'
                IsReady = false;
                // 백오프 재시도는 유지
                if (Application.isFocused && AdManager.Instance != null)
                    AdManager.Instance.StartCoroutine(RetryAfter(10f));
                return;
            }
            Debug.Log("[Rewarded] Load success"); // ← 이게 찍히면 ‘초기화 성공 & 준비 완료’
            _ad = ad;
            IsReady = true;
            HookEvents(_ad);
        });
    }

    public void DestroyAd()
    {
        if (_ad == null) return;
        _ad.Destroy();
        _ad = null;
        _externalOnReward = null;
        _granted = false;
        IsReady = false;
        IsLoading = false;
    }

    public bool ShowAd(Action onReward = null, bool ignoreCooldown = false)
    {
        if (!ignoreCooldown && AdManager.Instance != null &&
            AdManager.Instance.NextRewardTime > DateTime.UtcNow)
        {
            Debug.Log("[Rewarded] Skipped due to cooldown");
            return false;
        }

        if (_ad == null || !_ad.CanShowAd() || !IsReady)
        {
            Debug.Log("[Rewarded] Not ready → Load");
            Init();
            return false;
        }

        _externalOnReward = onReward;
        _granted = false;
        IsReady = false;

        _ad.Show(reward =>
        {
            Debug.Log($"[Rewarded] Granted: {reward.Type} x{reward.Amount}");
            _granted = true;

            AdReviveToken.MarkGranted();

            try { Rewarded?.Invoke(); } catch { }
            try { onReward?.Invoke(); } catch { }
        });

        return true;
    }


    private void HookEvents(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdFullScreenContentOpened += () =>
        {
            ReviveGate.Arm(10f);
            Debug.Log("[Rewarded] Opened");
            try { Opened?.Invoke(); } catch { }
            if (Game.IsBound) Game.Bus.PublishImmediate(new AdPlaying());
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Rewarded] Closed");

            ReviveGate.Disarm();

            try { Closed?.Invoke(); } catch { }

            if (_granted)
            {
                _granted = false;

                Game.Save?.TryConsumeDownedPending(out _);

                Game.Bus?.PublishImmediate(new ContinueGranted());
                Game.Bus?.PublishImmediate(new RevivePerformed());
            }

            Game.Bus?.PublishImmediate(new AdFinished());

            _externalOnReward = null;
            IsReady = false;
            Init();
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"[Rewarded] Show error: {e}");
            try { Failed?.Invoke(); } catch { }
            if (Game.IsBound) Game.Bus.PublishImmediate(new AdFinished());
            _granted = false;
            _externalOnReward = null;
            IsReady = false;
            Init();

            ReviveGate.Disarm();
        };
    }
    private IEnumerator RetryAfter(float sec)
    {
        float until = Time.realtimeSinceStartup + sec;
        while (Time.realtimeSinceStartup < until) yield return null;
        if (!IsReady && !IsLoading && Application.isFocused) Init();
    }
}
