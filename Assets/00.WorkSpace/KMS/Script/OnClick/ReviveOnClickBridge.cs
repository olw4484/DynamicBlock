using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    [SerializeField] bool freeReviveWhenAdsUnavailable = true;
    bool _rewardFired;
    public void OnClickRevive()
    {
        Game.Audio.PlayButtonClick();

        var ads = Game.Ads;
        // 1) 광고 준비 안됨
        if (!(ads?.IsRewardedReady() ?? false))
        {
            if (freeReviveWhenAdsUnavailable)
            {
                Debug.LogWarning("[ReviveBridge] Reward not ready → FREE REVIVE (dev)");
                PublishRevive();
                return;
            }
            ads?.Refresh();
            Debug.LogWarning("[ReviveBridge] Reward not ready");
            return;
        }

        // 2) 광고 시도
        _rewardFired = false;
        ads.ShowRewarded(
            onReward: () =>
            {
                _rewardFired = true;
                PublishRevive();
            },
            onClosed: () =>
            {
                // 일부 SDK는 onClosed만 오는 경우가 있음 → 개발 모드면 우회 허용
                if (freeReviveWhenAdsUnavailable && !_rewardFired)
                {
                    Debug.LogWarning("[ReviveBridge] Closed without reward → FREE REVIVE (dev)");
                    PublishRevive();
                }
                // 패널 닫기는 ReviveScreen/UIManager가 담당
            },
            onFailed: () =>
            {
                if (freeReviveWhenAdsUnavailable)
                {
                    Debug.LogWarning("[ReviveBridge] Reward failed → FREE REVIVE (dev)");
                    PublishRevive();
                }
                else
                {
                    if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
                }
            }
        );
    }

    void PublishRevive()
    {
        if (Game.IsBound) Game.Bus.PublishImmediate(new ReviveRequest());
    }

    public void OnClickGiveUp()
    {
        Game.Audio.PlayButtonClick();
        if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
    }
}