using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    [SerializeField] bool freeReviveWhenAdsUnavailable = true;
    bool _rewardFired;
    public void OnClickRevive()
    {
        Game.Audio.PlayButtonClick();

        var ads = Game.Ads;
        // 1) ���� �غ� �ȵ�
        if (!(ads?.IsRewardedReady() ?? false))
        {
            if (freeReviveWhenAdsUnavailable)
            {
                Debug.LogWarning("[ReviveBridge] Reward not ready �� FREE REVIVE (dev)");
                PublishRevive();
                return;
            }
            ads?.Refresh();
            Debug.LogWarning("[ReviveBridge] Reward not ready");
            return;
        }

        // 2) ���� �õ�
        _rewardFired = false;
        ads.ShowRewarded(
            onReward: () =>
            {
                _rewardFired = true;
                PublishRevive();
            },
            onClosed: () =>
            {
                // �Ϻ� SDK�� onClosed�� ���� ��찡 ���� �� ���� ���� ��ȸ ���
                if (freeReviveWhenAdsUnavailable && !_rewardFired)
                {
                    Debug.LogWarning("[ReviveBridge] Closed without reward �� FREE REVIVE (dev)");
                    PublishRevive();
                }
                // �г� �ݱ�� ReviveScreen/UIManager�� ���
            },
            onFailed: () =>
            {
                if (freeReviveWhenAdsUnavailable)
                {
                    Debug.LogWarning("[ReviveBridge] Reward failed �� FREE REVIVE (dev)");
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