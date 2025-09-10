using UnityEngine;

public sealed class ReviveOnClickBridge : MonoBehaviour
{
    public void OnClickRevive()
    {
        if (!(Game.Ads?.IsRewardedReady() ?? false))
        {
            Game.Ads?.Refresh();
            Debug.LogWarning("[ReviveBridge] Reward not ready");
            return;
        }

        Game.Ads.ShowRewarded(
            onReward: () => { if (Game.IsBound) Game.Bus.PublishImmediate(new ReviveRequest()); },
            onClosed: () => { /* 패널 닫기 등은 ReviveScreen이 담당 */ },
            onFailed: () => { if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest()); }
        );
    }

    public void OnClickGiveUp()
    {
        if (Game.IsBound) Game.Bus.PublishImmediate(new GiveUpRequest());
    }
}
