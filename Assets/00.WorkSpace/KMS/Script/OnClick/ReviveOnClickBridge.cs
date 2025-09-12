using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    public void OnClickRevive()
    {
        Game.Audio.PlayButtonClick();

        if (!(Game.Ads?.IsRewardedReady() ?? false))
        {
            Game.Ads?.Refresh();
            Debug.LogWarning("[ReviveBridge] Reward not ready");
            return;
        }

        Game.Ads.ShowRewarded(
            onReward: () => { if (Game.IsBound) Game.Bus.PublishImmediate(new ReviveRequest()); },
            onClosed: () => { /* �г� �ݱ� ���� ReviveScreen�� ��� */ },
            onFailed: () => { if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest()); }
        );
    }

    public void OnClickGiveUp()
    {
        Game.Audio.PlayButtonClick();
        if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
    }
}
