using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public sealed class AdServiceAdapter : IAdService
{
    public int Order => 55;
    private AdManager _ad;

    public void PreInit() { }

    public void Init()
    {
        _ad = AdManager.Instance;
        if (_ad == null)
            Debug.LogWarning("[AdServiceAdapter] AdManager.Instance is null. Is it in the scene?");
        // AdManager가 내부에서 Initialize → 각 컨트롤러 Init 호출
    }
    public void PostInit() { }
    public void Shutdown() { }

    public void InitAds(bool userConsent) { /* CMP/ATT 처리 지점 */ }

    public bool IsRewardedReady() => _ad?.Reward != null && _ad.Reward.IsReady;
    public bool IsInterstitialReady() => _ad?.Interstitial != null && _ad.Interstitial.IsReady;

    public void ShowRewarded(Action onReward, Action onClosed = null, Action onFailed = null)
    {
        if (!IsRewardedReady()) { onFailed?.Invoke(); return; }

        void OnR() { Unsub(); onReward?.Invoke(); }
        void OnC() { Unsub(); onClosed?.Invoke(); }
        void OnF() { Unsub(); onFailed?.Invoke(); }

        void Unsub()
        {
            _ad.Reward.Rewarded -= OnR;
            _ad.Reward.Closed -= OnC;
            _ad.Reward.Failed -= OnF;
        }

        _ad.Reward.Rewarded += OnR;
        _ad.Reward.Closed += OnC;
        _ad.Reward.Failed += OnF;

        // 외부 onReward도 컨트롤러에 넘겨 보장
        _ad.Reward.ShowAd(onReward);
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        if (!IsInterstitialReady()) { onClosed?.Invoke(); return; }

        void OnC()
        {
            _ad.Interstitial.Closed -= OnC;
            onClosed?.Invoke();
        }

        _ad.Interstitial.Closed += OnC;
        _ad.Interstitial.ShowAd();
    }

    public void ToggleBanner(bool show)
    {
        if (_ad?.Banner == null)
            return;

        if (show)
        {
            _ad.Banner.ShowAd();
        }
        else
        {
            _ad.Banner.HideAd();
        }
    }

    public void Refresh()
    {
        // 필요 시 재로딩 직접 트리거
        _ad?.Interstitial?.Init();
        _ad?.Reward?.Init();
        // 배너는 자동 로드/토글 구조라 보통 필요 없음
    }
}