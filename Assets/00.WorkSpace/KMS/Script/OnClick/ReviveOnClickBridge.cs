using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    [SerializeField] bool freeReviveWhenAdsUnavailable = false;
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

        if (!ads.CanOfferReviveNow())
        {
            Debug.LogWarning("[ReviveBridge] Revive not offerable now  refresh & bail");
            ads.Refresh();
            FreeReviveOrGiveUp("[ReviveBridge] gate denied");
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
                    // ContinueGranted + RevivePerformed  RewardAdController 
                    if (_rewardFired)
                        Debug.Log("[ReviveBridge] Closed with reward  handled by RewardAdController");
                    else
                        FreeReviveOrGiveUp("[ReviveBridge] Closed without reward");
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
                ReviveCleanup.ResetAll("revive_closed");
                if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest("revive_closed"));
                var ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
                ui?.OpenResultNowBecauseNoRevive();
                return;
            }

        Debug.LogWarning($"{reasonLog}  FREE REVIVE (dev)");
        if (!ReviveGate.IsArmed) ReviveGate.Arm(2f);
        Game.Bus?.PublishImmediate(new ContinueGranted());
        Game.Bus?.PublishImmediate(new RevivePerformed());
    }
}
