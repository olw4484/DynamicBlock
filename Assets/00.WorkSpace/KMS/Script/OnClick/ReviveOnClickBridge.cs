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
        if (ads == null) { FreeReviveOrGiveUp("[ReviveBridge] Ads service missing"); return; }

        if (!ads.IsRewardedReady())
        {
            Debug.LogWarning("[ReviveBridge] Reward not ready → refresh & bail");
            ads.Refresh();
            FreeReviveOrGiveUp("[ReviveBridge] not ready");
            return;
        }

        _waitingAd = true;
        _rewardFired = false;

        ads.ShowRewarded(
            onReward: () =>
            {
                _rewardFired = true;
                Debug.Log("[ReviveBridge] Reward granted (flag set). Will act on Closed.");
            },
            onClosed: () =>
            {
                try
                {
                    // ContinueGranted + RevivePerformed 를 발행하므로 여기선 아무것도 발행 X
                    if (_rewardFired)
                    {
                        Debug.Log("[ReviveBridge] Closed with reward → handled by RewardAdController");
                    }
                    else
                    {
                        FreeReviveOrGiveUp("[ReviveBridge] Closed without reward");
                    }
                }
                finally { _waitingAd = false; }
            },
            onFailed: () =>
            {
                Debug.LogWarning("[ReviveBridge] Reward failed");
                _waitingAd = false;
                FreeReviveOrGiveUp("[ReviveBridge] onFailed");
            }
        );
    }

    void FreeReviveOrGiveUp(string reasonLog)
    {
        if (!freeReviveWhenAdsUnavailable)
        {
            if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
            return;
        }

        Debug.LogWarning($"{reasonLog} → FREE REVIVE (dev)");

        if (!ReviveGate.IsArmed) ReviveGate.Arm(2f);
        Game.Bus?.PublishImmediate(new ContinueGranted());
        Game.Bus?.PublishImmediate(new RevivePerformed());
    }
}
