using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    [SerializeField] bool freeReviveWhenAdsUnavailable = true;
    bool _rewardFired;
    bool _waitingAd;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
    void Awake() => freeReviveWhenAdsUnavailable = false;
#endif

    public void OnClickRevive()
    {
        if (_waitingAd) return;
        Game.Audio.PlayButtonClick();

        var ads = Game.Ads;
        if (ads == null) { FallbackOrGiveUp("[ReviveBridge] Ads service missing"); return; }

        if (!ads.IsRewardedReady())
        {
            Debug.LogWarning("[ReviveBridge] Reward not ready → refresh & bail");
            ads.Refresh();
            if (freeReviveWhenAdsUnavailable)
            {
                Debug.LogWarning("[ReviveBridge] FREE REVIVE (dev, not ready)");
                PublishRevive();
            }
            return;
        }

        _waitingAd = true;
        _rewardFired = false;

        ads.ShowRewarded(
            onReward: () =>
            {
                // 즉시 Revive 금지 - 플래그만
                _rewardFired = true;
                Debug.Log("[ReviveBridge] Reward granted (flag set). Will revive on Closed.");
            },
            onClosed: () =>
            {
                try
                {
                    if (_rewardFired)
                    {
                        Debug.Log("[ReviveBridge] Closed with reward → PublishRevive()");
                        PublishRevive();
                    }
                    else if (freeReviveWhenAdsUnavailable)
                    {
                        Debug.LogWarning("[ReviveBridge] Closed without reward → FREE REVIVE (dev)");
                        PublishRevive();
                    }
                }
                finally { _waitingAd = false; }
            },
            onFailed: () =>
            {
                Debug.LogWarning("[ReviveBridge] Reward failed");
                _waitingAd = false;
                FallbackOrGiveUp("[ReviveBridge] onFailed");
            }
        );
    }

    void FallbackOrGiveUp(string reasonLog)
    {
        if (freeReviveWhenAdsUnavailable)
        {
            Debug.LogWarning($"{reasonLog} → FREE REVIVE (dev)");
            PublishRevive();
        }
        else if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
    }

    void PublishRevive()
    {
        if (Game.IsBound) Game.Bus.PublishImmediate(new ReviveRequest());
    }
}
