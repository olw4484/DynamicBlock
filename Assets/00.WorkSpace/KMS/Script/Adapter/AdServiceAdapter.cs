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

    // ���� SDK ȣ�� �� (��¥ ���� ��)
    public void ShowRewarded(Action<bool> onDone)
    {
        // TODO: AdMob SDK ���� �� �ݹ����� onDone(true/false)
        // �����:
        onDone?.Invoke(true);
    }


}
