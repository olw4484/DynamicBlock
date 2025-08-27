using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SplashCoordinator : MonoBehaviour
{
    [SerializeField] string splashPanel = "Splash";
    [SerializeField] string nextPanel = "Main";
    [SerializeField] float minShowSec = 1.5f;
    [SerializeField] bool allowTapSkip = true;

    EventQueue _bus;
    bool _finished;

    void OnEnable() => StartCoroutine(GameBindingUtil.WaitAndRun(() => Bind(Game.Bus)));

    void Bind(EventQueue bus)
    {
        _bus = bus;

        // 1) 시작: 스플래시 켜기 (Sticky + Immediate)
        var onSplash = new PanelToggle(splashPanel, true);
        _bus.PublishSticky(onSplash, alsoEnqueue: false);
        _bus.PublishImmediate(onSplash);

        // 2) 최소 노출 시간 뒤 자동 종료 예약
        _bus.PublishAfter(new SplashFinish(), minShowSec);

        // 3) 프리로드 완료/탭-스킵도 종료 트리거
        if (allowTapSkip)
            _bus.Subscribe<SplashSkipRequest>(_ => { if (!_finished) _bus.PublishImmediate(new SplashFinish()); }, replaySticky: false);
        _bus.Subscribe<PreloadDone>(_ => { if (!_finished) _bus.PublishImmediate(new SplashFinish()); }, replaySticky: false);

        // 4) 실제 종료 처리 (디듀프 가드)
        _bus.Subscribe<SplashFinish>(_ =>
        {
            if (_finished) return;
            _finished = true;

            var offSplash = new PanelToggle(splashPanel, false);
            _bus.PublishSticky(offSplash, alsoEnqueue: false);
            _bus.PublishImmediate(offSplash);

            if (!string.IsNullOrEmpty(nextPanel))
            {
                var onNext = new PanelToggle(nextPanel, true);
                _bus.PublishSticky(onNext, alsoEnqueue: false);
                _bus.PublishImmediate(onNext);
            }
        }, replaySticky: false);
    }
}
