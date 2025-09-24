using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    // ����� ���� ���� �����̺� ����ϰ�, ���� ���忡�� ������ ���ϴ�.
    [SerializeField] bool freeReviveWhenAdsUnavailable = true;

    bool _rewardFired;
    bool _waitingAd;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
    void Awake()
    {
        // ������ ���忡�� �����ϰ� ���� �����̺� ����
        freeReviveWhenAdsUnavailable = false;
    }
#endif

    public void OnClickRevive()
    {
        if (_waitingAd) return; // �ߺ� Ŭ�� ����
        Game.Audio.PlayButtonClick();

        // AdManager(IAdService) Ȯ��
        var ads = Game.Ads; // �Ǵ� AdManager.Instance
        if (ads == null)
        {
            FallbackOrGiveUp("[ReviveBridge] Ads service missing");
            return;
        }

        // �غ� üũ
        if (!ads.IsRewardedReady())
        {
            Debug.LogWarning("[ReviveBridge] Reward not ready");
            ads.Refresh(); // ���� �ε� ����

            if (freeReviveWhenAdsUnavailable)
            {
                Debug.LogWarning("[ReviveBridge] FREE REVIVE (dev, not ready)");
                PublishRevive();
            }
            return;
        }

        // ���� �õ�
        _waitingAd = true;
        _rewardFired = false;

        ads.ShowRewarded(
            onReward: () =>
            {
                _rewardFired = true;
                // ���� ��� ���� (���� �ݹ� ��ٸ� �ʿ� ����)
                PublishRevive();
            },
            onClosed: () =>
            {
                // ���� ����. �Ϻ� SDK/�ܸ����� ���� �ݹ��� �� �� ���� ������ ���� �߿� ��ȸ ���
                if (!_rewardFired && freeReviveWhenAdsUnavailable)
                {
                    Debug.LogWarning("[ReviveBridge] Closed without reward �� FREE REVIVE (dev)");
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
            Debug.LogWarning($"{reasonLog} �� FREE REVIVE (dev)");
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
