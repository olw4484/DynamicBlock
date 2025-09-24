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
        // AdManager�� ���ο��� Initialize �� �� ��Ʈ�ѷ� Init ȣ��
    }
    public void PostInit() { }
    public void Shutdown() { }

    public void InitAds(bool userConsent) { /* CMP/ATT ó�� ���� */ }

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

        // �ܺ� onReward�� ��Ʈ�ѷ��� �Ѱ� ����
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
        // �ʿ� �� ��ε� ���� Ʈ����
        _ad?.Interstitial?.Init();
        _ad?.Reward?.Init();
        // ��ʴ� �ڵ� �ε�/��� ������ ���� �ʿ� ����
    }
}