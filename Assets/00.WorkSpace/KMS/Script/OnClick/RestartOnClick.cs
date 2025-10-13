using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;
using System.Collections;

public sealed class RestartOnClick : MonoBehaviour
{
    public enum RestartMode { SoftReset, ReloadSceneViaEvent, ReloadSceneDirect }

    [SerializeField] RestartMode mode = RestartMode.SoftReset;
    [SerializeField] string[] closePanels = { "Options", "GameOver" };
    [SerializeField] string openPanelAfter = "Game";
    [SerializeField] string gameplayScene = "Gameplay";
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] bool clearRunStateOnSoftReset = false;

    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    // UIManager 핸들러
    static UIManager UI => (Game.UI as UIManager) ?? Object.FindFirstObjectByType<UIManager>();

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;
        Sfx.Button();

        switch (mode)
        {
            case RestartMode.SoftReset:
                {
                    var sm = MapManager.Instance?.saveManager;
                    var bus = Game.Bus;

                    sm?.ClearRunState(save: true);
                    sm?.SkipNextSnapshot("Restart");
                    sm?.SuppressSnapshotsFor(1.0f);

                    // 엔진/런타임 리셋
                    bus.PublishImmediate(new GameResetRequest(openPanelAfter, ResetReason.Restart));

                    // UI 강제 정리/전환 (지연 무시)
                    var ui = UI;
                    if (ui)
                    {
                        if (closePanels != null)
                            foreach (var k in closePanels)
                                ui.ClosePanelImmediate(k);

                        if (!string.IsNullOrEmpty(openPanelAfter))
                            ui.SetPanel(openPanelAfter, true, ignoreDelay: true);
                    }
                    else
                    {
                        // (폴백) 기존 유틸
                        RestartFlow.SoftReset(openPanelAfter, closePanels);
                    }

                    if (MapManager.Instance.CurrentMode == GameMode.Adventure)
                    {
                        int idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;
                        MapManager.Instance.StartCoroutine(CoEnterAdventureNextFrame(idx0, openPanelAfter));
                        return;
                    }

                    MapManager.Instance.StartCoroutine(CoEnterClassicNextFrame());
                    break;
                }

            case RestartMode.ReloadSceneViaEvent:
                RestartFlow.ReloadViaEvent(gameplayScene);
                break;

            case RestartMode.ReloadSceneDirect:
                RestartFlow.ReloadDirect(gameplayScene);
                break;
        }
    }

    private IEnumerator CoEnterClassicNextFrame()
    {
        yield return null;

        var bus = Game.Bus;

        bus.PublishImmediate(new GameEnterRequest(GameMode.Classic, MapManager.ClassicEnterPolicy.ForceNew));
        bus.PublishImmediate(new GameEnterIntent(GameMode.Classic, forceLoadSave: false));

        MapManager.Instance.SetGameMode(GameMode.Classic);
        MapManager.Instance.RequestClassicEnter(MapManager.ClassicEnterPolicy.ForceNew);

        var ui = UI;
        if (ui) ui.SetPanel("Game", true, ignoreDelay: true);

        bus.PublishSticky(new PanelToggle("Game", true), alsoEnqueue: false);
        bus.PublishImmediate(new PanelToggle("Game", true));
        bus.ClearSticky<GameOver>();
    }

    private IEnumerator CoEnterAdventureNextFrame(int idx0, string openPanel)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        var bus = Game.Bus;

        var ui = UI;
        if (ui && !string.IsNullOrEmpty(openPanel))
            ui.SetPanel(openPanel, true, ignoreDelay: true);

        bus.PublishSticky(new PanelToggle(openPanel, true), alsoEnqueue: false);
        bus.PublishImmediate(new PanelToggle(openPanel, true));
        bus.ClearSticky<GameOver>();

        MapManager.Instance.EnterAdventureByIndex0(idx0);
        Debug.Log($"[AdventureRestart] re-enter idx0={idx0}");
    }
}
