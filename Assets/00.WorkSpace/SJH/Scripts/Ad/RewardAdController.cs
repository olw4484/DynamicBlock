using GoogleMobileAds.Api;
using System;
using UnityEngine;

public class RewardAdController
{
	/*
	 * 보상 테스트 광고 : "ca-app-pub-3940256099942544/5224354917"
	 */

	private const string _rewardAdId = "ca-app-pub-3940256099942544/5224354917";
	private RewardedAd _loadedAd;
	private Action<Reward> _rewardAction;

    public bool IsReady { get; private set; }
    public event Action Rewarded;
    public event Action Closed;
    public event Action Failed;

    private Action _externalOnReward;

    public void Init()
    {
        DestroyAd();
        IsReady = false;

        Debug.Log("리워드 광고 로딩 시작");
        RewardedAd.Load(_rewardAdId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"리워드 광고 로딩 실패 : [{error}]");
                IsReady = false;
                return;
            }

            Debug.Log("리워드 광고 로딩 성공");
            _loadedAd = ad;
            IsReady = true;
            EventConnect(_loadedAd);
        });
    }
    public void DestroyAd()
    {
        if (_loadedAd == null) return;

        Debug.Log("리워드 광고 제거");
        _loadedAd.Destroy();
        _loadedAd = null;
        IsReady = false;
    }
    public void ShowAd(Action onReward = null)
    {
        // 시간 예외처리
        if (AdManager.Instance.NextRewardTime > DateTime.UtcNow)
        {
            Debug.Log("시간이 지나지 않아 광고 재생을 스킵");
			return;
		}

        if (_loadedAd == null || !_loadedAd.CanShowAd() || !IsReady)
        {
            Debug.Log("리워드 준비 안 됨 → 로드 시도");
            Init();
            return;
        }

        _externalOnReward = onReward;
        IsReady = false;

        _loadedAd.Show(reward =>
        {
            Debug.Log($"리워드 획득 : Type [{reward.Type}] / Amount [{reward.Amount}]");
            Rewarded?.Invoke();
            _externalOnReward?.Invoke();      // 외부 보상 콜백
        });
    }
    void EventConnect(RewardedAd ad)
    {
        if (ad == null) return;

        ad.OnAdPaid += (AdValue v) => Debug.Log($"리워드 광고 수익 {v.CurrencyCode}/{v.Value}");
        ad.OnAdImpressionRecorded += () => Debug.Log("리워드 광고 봄");
        ad.OnAdClicked += () => Debug.Log("리워드 광고 클릭");

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("리워드 광고 활성화");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("리워드 광고 닫음");
            IsReady = false;
            Closed?.Invoke();
            _externalOnReward = null; // 한 번 쓰고 정리
            Init(); // 다음 로드
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"리워드 광고 에러 : [{e}]");
            IsReady = false;
            Failed?.Invoke();
            _externalOnReward = null;
            Init();
        };
    }
    public void ShowAdSimple() => ShowAd();

    void WireEvents(RewardedAd ad)
    {
        ad.OnAdPaid += (AdValue v) => Debug.Log($"리워드 광고 수익 {v.CurrencyCode}/{v.Value}");
        ad.OnAdImpressionRecorded += () => Debug.Log("리워드 광고 봄");
        ad.OnAdClicked += () => Debug.Log("리워드 광고 클릭");

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("리워드 광고 활성화");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("리워드 광고 닫음");
            _externalOnReward = null;
            Closed?.Invoke();
            Init();
        };

        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogError($"리워드 광고 에러 : [{e}]");
            _externalOnReward = null;
            Failed?.Invoke();
            Init();
        };
    }
}
