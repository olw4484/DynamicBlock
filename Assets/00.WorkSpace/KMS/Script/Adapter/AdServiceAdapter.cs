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

    public void InitAds(bool userConsent) { /* CMP/ATT 등 처리 위치 */ }

    // ====== 상태 조회 ======
    public bool IsRewardedReady() => _ad != null && _ad.Reward != null && _ad.Reward.IsReady;
    public bool IsInterstitialReady() => _ad != null && _ad.Interstitial != null && _ad.Interstitial.IsReady;

    public bool IsRewardCooldownActive(out float remainSec)
    {
        remainSec = 0f;
        if (_ad == null) return false; // AdManager 없으면 쿨타임 없음으로 간주
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

    // ====== 노출 요청 ======
    public void ShowRewarded(Action onReward, Action onClosed = null, Action onFailed = null)
    {
        // 쿨타임/미준비면 바로 실패 콜백 (UI 측에서 GiveUp로 흐름 처리)
        if (!CanOfferReviveNow())
        {
            try { onFailed?.Invoke(); } catch { }
            return;
        }

        // AdManager의 안전 래퍼(Co_ShowRewardedSafely)로 위임 (watchdog/guard 포함)
        _ad?.ShowRewarded(onReward, onClosed, onFailed);
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        // 인터스티셜은 AdManager 쪽 안전 래퍼를 그대로 사용
        _ad?.ShowInterstitial(onClosed);
    }

    // ====== 배너/리프레시 ======
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
