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

                    // 1) 저장 상태 확실히 삭제 + 스냅샷 억제
                    sm?.ClearRunState(save: true);
                    sm?.SkipNextSnapshot("Restart");
                    sm?.SuppressSnapshotsFor(1.0f);

                    // 2) 리셋 이벤트 (BlockStorage.ResetRuntime 등)
                    bus.PublishImmediate(new GameResetRequest(openPanelAfter, ResetReason.Restart));

                    // 3) UI 정리/전환
                    RestartFlow.SoftReset(openPanelAfter, closePanels);

                    // 3.5) 어드벤처 모드일 경우 어드벤쳐를 리셋하기.
                    if (MapManager.Instance.CurrentMode == GameMode.Adventure)
                    {
                        MapManager.Instance.EnterStage(StageManager.Instance.GetCurrentStage() + 1);
                        return;
                    }

                    // 4) 다음 프레임에 ‘클래식 입장’ 실행 (MapManager가 코루틴 host)
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

        bus.PublishSticky(new PanelToggle("Game", true), alsoEnqueue: false);
        bus.PublishImmediate(new PanelToggle("Game", true));
        bus.ClearSticky<GameOver>();
    }
}
