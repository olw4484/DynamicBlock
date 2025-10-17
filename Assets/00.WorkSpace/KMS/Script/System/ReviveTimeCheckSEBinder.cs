using UnityEngine;

public sealed class ReviveTimeCheckSEBinder : MonoBehaviour
{
    bool _subscribed;

    void OnEnable()
    {
        Game.Audio?.PlayContinueTimeCheckSE();
        Subscribe();
    }

    void OnDisable()
    {
        Game.Audio?.StopContinueTimeCheckSE();
        Unsubscribe();
    }

    void Subscribe()
    {
        if (!Game.IsBound || _subscribed) return;
        _subscribed = true;

        Game.Bus.Subscribe<ContinueGranted>(_ => StopNow(), replaySticky: false);
        Game.Bus.Subscribe<RevivePerformed>(_ => StopNow(), replaySticky: false);
        Game.Bus.Subscribe<GiveUpRequest>(_ => StopNow(), replaySticky: false);
        Game.Bus.Subscribe<GameOverConfirmed>(_ => StopNow(), replaySticky: true);
        Game.Bus.Subscribe<AdFinished>(_ => StopNow(), replaySticky: false);
    }

    void Unsubscribe()
    {
        if (!Game.IsBound || !_subscribed) return;
        _subscribed = false;

        Game.Bus.Unsubscribe<ContinueGranted>(_ => StopNow());
        Game.Bus.Unsubscribe<RevivePerformed>(_ => StopNow());
        Game.Bus.Unsubscribe<GiveUpRequest>(_ => StopNow());
        Game.Bus.Unsubscribe<GameOverConfirmed>(_ => StopNow());
        Game.Bus.Unsubscribe<AdFinished>(_ => StopNow());
    }

    void StopNow()
    {
        Game.Audio?.StopContinueTimeCheckSE();
    }
}
