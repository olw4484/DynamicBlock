using System;
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
    }

    public void PostInit() { }
    public void Shutdown() { }

    public void InitAds(bool userConsent) { /* CMP/ATT �� ó�� ��ġ */ }

    // ====== ���� ��ȸ ======
    public bool IsRewardedReady() => _ad != null && _ad.Reward != null && _ad.Reward.IsReady;
    public bool IsInterstitialReady() => _ad != null && _ad.Interstitial != null && _ad.Interstitial.IsReady;

    public bool IsRewardCooldownActive(out float remainSec)
    {
        remainSec = 0f;
        if (_ad == null) return false; // AdManager ������ ��Ÿ�� �������� ����
        if (_ad.RewardTime <= 0) return false;

        var now = DateTime.UtcNow;
        if (now < _ad.NextRewardTime)
        {
            remainSec = (float)(_ad.NextRewardTime - now).TotalSeconds;
            return true;
        }
        return false;
    }
    public bool CanOfferReviveNow()
    {
        if (_ad == null) return false;
        float _;
        return IsRewardedReady() && !IsRewardCooldownActive(out _);
    }

    // ====== ���� ��û ======
    public void ShowRewarded(Action onReward, Action onClosed = null, Action onFailed = null)
    {
        // ��Ÿ��/���غ�� �ٷ� ���� �ݹ� (UI ������ GiveUp�� �帧 ó��)
        if (!CanOfferReviveNow())
        {
            try { onFailed?.Invoke(); } catch { }
            return;
        }

        // AdManager�� ���� ����(Co_ShowRewardedSafely)�� ���� (watchdog/guard ����)
        _ad?.ShowRewarded(onReward, onClosed, onFailed);
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        // ���ͽ�Ƽ���� AdManager �� ���� ���۸� �״�� ���
        _ad?.ShowInterstitial(onClosed);
    }

    // ====== ���/�������� ======
    public void ToggleBanner(bool show)
    {
        if (_ad == null) return;
        _ad.ToggleBanner(show);
    }

    public void Refresh()
    {
        _ad?.Refresh();
    }
}
