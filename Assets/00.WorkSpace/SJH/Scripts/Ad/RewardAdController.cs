using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;
using UnityEngine;

public class RewardAdController
{
    // ==========================
    // 1) 유닛ID 분기
    // ==========================
#if UNITY_ANDROID
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/5224354917";
    private const string PROD_REWARDED = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX"; // TODO: 실제 ID
#elif UNITY_IOS
    private const string TEST_REWARDED = "ca-app-pub-3940256099942544/1712485313";
    private const string PROD_REWARDED = "ca-app-pub-XXXXXXXXXXXXXXX/XXXXXXXXXX"; // TODO: 실제 ID
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

    // ==========================
    // 2) 상태
    // ==========================
    private RewardedAd _loadedAd;
    private Action _externalOnReward; // 외부 보상 콜백

    public bool IsReady { get; private set; }
    public event Action Rewarded; // 내부 이벤트(보상 지급 시)
    public event Action Closed;   // 닫힘
    public event Action Failed;   // 실패

    // ==========================
    // 테스트 디바이스 강제
    // 앱 시작 시 1회 호출
    // ==========================
    public static void ConfigureTestDevices(params string[] testDeviceIds)
    {
#if TEST_ADS || DEVELOPMENT_BUILD
        var list = (testDeviceIds == null) ? null : new List<string>(testDeviceIds);
        var conf = new RequestConfiguration.Builder()
            .SetTestDeviceIds(list)
            .build();
        MobileAds.SetRequestConfiguration(conf);
#endif
    }

    // ==========================
    // 3) 초기화 & 로드
    // ==========================
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

        Debug.Log("[Rewarded] Destroy");
        _loadedAd.Destroy();
        _loadedAd = null;
        IsReady = false;
        _externalOnReward = null;
    }

    // ==========================
    // 4) 표시
    // ==========================
    public void ShowAd(Action onReward = null)
    {
        // 쿨다운(다음 가능 시간) 체크
        if (AdManager.Instance.NextRewardTime > DateTime.UtcNow)
        {
            Debug.Log("[Rewarded] Skipped due to cooldown");
            // 스킵 시 즉시 가능하게 보정
            AdManager.Instance.NextRewardTime = DateTime.UtcNow;
            return;
        }

        if (_loadedAd == null || !_loadedAd.CanShowAd() || !IsReady)
        {
            Debug.Log("[Rewarded] Not ready → Load");
            Init();
            return;
        }

        _externalOnReward = onReward;
        IsReady = false; // 1로딩 1표시 원칙

        _loadedAd.Show(reward =>
        {
            Debug.Log($"[Rewarded] Granted: Type={reward.Type}, Amount={reward.Amount}");
            Rewarded?.Invoke();      // 내부 이벤트
            _externalOnReward?.Invoke(); // 외부 보상 콜백
        });
    }

    public void ShowAdSimple() => ShowAd();

    // ==========================
    // 5) 이벤트 연결
    // ==========================
    private void HookEvents(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdPaid += (AdValue v) =>
            Debug.Log($"[Rewarded] Paid: {v.CurrencyCode}/{v.Value}");

        ad.OnAdImpressionRecorded += () =>
            Debug.Log("[Rewarded] Impression recorded");

        ad.OnAdClicked += () =>
            Debug.Log("[Rewarded] Clicked");

        ad.OnAdFullScreenContentOpened += () =>
            Debug.Log("[Rewarded] Opened");

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Rewarded] Closed");
            Closed?.Invoke();

            // 쿨다운(Reward 전용 간격이 있다면 사용)
            // 예: AdManager.Instance.RewardTime (초) 존재 가정
            if (AdManager.Instance != null && AdManager.Instance.RewardTime > 0)
                AdManager.Instance.NextRewardTime = DateTime.UtcNow.AddSeconds(AdManager.Instance.RewardTime);

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
