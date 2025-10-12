using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;
using System.Collections;

public sealed class SplashCoordinator : MonoBehaviour
{
    [SerializeField] string splashPanel = "Splash";
    [SerializeField] string nextPanel = "Main";
    [SerializeField] float minShowSec = 1.5f;
    [SerializeField] bool allowTapSkip = true;

    // 에디터에서 직접 할당하거나, 비워두면 런타임에 찾아서 씀
    [SerializeField] ServiceNoticeGate noticeGate;

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

            _bus.PublishAfter(new ServiceNoticeCheck(), 0.05f);
        }, replaySticky: false);

    }
    IEnumerator CoOpenNoticeNextFrame()
    {
        yield return null;
        var gate = FindFirstObjectByType<ServiceNoticeGate>(FindObjectsInactive.Include);
        gate?.TryOpenIfNeeded();
    }
}
