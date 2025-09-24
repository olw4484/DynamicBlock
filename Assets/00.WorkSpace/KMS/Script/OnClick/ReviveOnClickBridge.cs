using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    // 디버그 때만 무료 리바이브 허용하고, 실제 빌드에선 강제로 끕니다.
    [SerializeField] bool freeReviveWhenAdsUnavailable = true;

    bool _rewardFired;
    bool _waitingAd;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
    void Awake()
    {
        // 릴리즈 빌드에선 안전하게 무료 리바이브 차단
        freeReviveWhenAdsUnavailable = false;
    }
#endif

    public void OnClickRevive()
    {
        if (_waitingAd) return; // 중복 클릭 방지
        Game.Audio.PlayButtonClick();

        // AdManager(IAdService) 확보
        var ads = Game.Ads; // 또는 AdManager.Instance
        if (ads == null)
        {
            FallbackOrGiveUp("[ReviveBridge] Ads service missing");
            return;
        }

        // 준비 체크
        if (!ads.IsRewardedReady())
        {
            Debug.LogWarning("[ReviveBridge] Reward not ready");
            ads.Refresh(); // 다음 로드 유도

            if (freeReviveWhenAdsUnavailable)
            {
                Debug.LogWarning("[ReviveBridge] FREE REVIVE (dev, not ready)");
                PublishRevive();
            }
            return;
        }

        // 광고 시도
        _waitingAd = true;
        _rewardFired = false;

        ads.ShowRewarded(
            onReward: () =>
            {
                _rewardFired = true;
                // 보상 즉시 지급 (닫힘 콜백 기다릴 필요 없음)
                PublishRevive();
            },
            onClosed: () =>
            {
                // 광고 닫힘. 일부 SDK/단말에서 보상 콜백이 안 올 수도 있으니 개발 중엔 우회 허용
                if (!_rewardFired && freeReviveWhenAdsUnavailable)
                {
                    Debug.LogWarning("[ReviveBridge] Closed without reward → FREE REVIVE (dev)");
                    PublishRevive();
                }
                _waitingAd = false;
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
        else
        {
            if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
        }
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
