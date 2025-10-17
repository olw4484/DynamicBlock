using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAdService : IManager
{
    void InitAds(bool userConsent);

    bool IsRewardedReady();
    void ShowRewarded(System.Action onReward, System.Action onClosed = null, System.Action onFailed = null);

    bool IsInterstitialReady();
    void ShowInterstitial(System.Action onClosed = null);

    void ToggleBanner(bool show);
    void Refresh();

    bool IsRewardCooldownActive(out float remainSec);
    bool CanOfferReviveNow();
}