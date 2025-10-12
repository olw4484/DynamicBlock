using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

public sealed class SplashCoordinator : MonoBehaviour
{
    [SerializeField] string splashPanel = "Splash";
    [SerializeField] string nextPanel = "Main";
    [SerializeField] float minShowSec = 1.5f;
    [SerializeField] bool allowTapSkip = true;

    EventQueue _bus;
    bool _finished;

    static UIManager UI => (Game.UI as UIManager) ?? Object.FindFirstObjectByType<UIManager>();

    void OnEnable() => StartCoroutine(GameBindingUtil.WaitAndRun(() => Bind(Game.Bus)));

    void Bind(EventQueue bus)
    {
        _bus = bus;

        var onSplash = new PanelToggle(splashPanel, true);
        _bus.PublishSticky(onSplash, alsoEnqueue: false);
        _bus.PublishImmediate(onSplash);

        _bus.PublishAfter(new SplashFinish(), minShowSec);

        if (allowTapSkip)
            _bus.Subscribe<SplashSkipRequest>(_ => { if (!_finished) _bus.PublishImmediate(new SplashFinish()); }, replaySticky: false);

        _bus.Subscribe<PreloadDone>(_ => { if (!_finished) _bus.PublishImmediate(new SplashFinish()); }, replaySticky: false);

        _bus.Subscribe<SplashFinish>(_ =>
        {
            if (_finished) return;
            _finished = true;

            // UI 강제 종료 & 전환 (지연 무시)
            var ui = UI;
            if (ui)
            {
                ui.ClosePanelImmediate(splashPanel);
                if (!string.IsNullOrEmpty(nextPanel))
                    ui.SetPanel(nextPanel, true, ignoreDelay: true);
            }
            else
            {
                var offSplash = new PanelToggle(splashPanel, false);
                _bus.PublishSticky(offSplash, alsoEnqueue: false);
                _bus.PublishImmediate(offSplash);

                if (!string.IsNullOrEmpty(nextPanel))
                {
                    var onNext = new PanelToggle(nextPanel, true);
                    _bus.PublishSticky(onNext, alsoEnqueue: false);
                    _bus.PublishImmediate(onNext);
                }
            }

            _bus.PublishImmediate(new AppSplashFinished());

        }, replaySticky: false);
    }
}
