using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AdServiceAdapter : IManager, IAdService
{
    public int Order => 45;
    EventQueue _bus;
    public void SetDependencies(EventQueue bus) { _bus = bus; }

    void IManager.PreInit() { }

    void IManager.Init() { }

    public void PostInit()
    {
        _bus.Subscribe<RewardedContinueRequest>(_ => {
            _bus.Publish(new AdPlaying());
            ShowRewarded(success => {
                if (success) _bus.Publish(new ContinueGranted());
                _bus.Publish(new AdFinished());
            });
        }, false);
    }

    // 실제 SDK 호출 부 (가짜 구현 예)
    public void ShowRewarded(Action<bool> onDone)
    {
        // TODO: AdMob SDK 연동 후 콜백으로 onDone(true/false)
        // 데모용:
        onDone?.Invoke(true);
    }


}
