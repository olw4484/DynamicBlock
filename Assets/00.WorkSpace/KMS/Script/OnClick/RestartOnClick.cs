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

                    if (MapManager.Instance.CurrentMode == GameMode.Adventure)
                    {
                        // 어드벤처는 0-based 유지 + 다음 프레임에 진입
                        int idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;
                        MapManager.Instance.StartCoroutine(CoEnterAdventureNextFrame(idx0, openPanelAfter));
                        return;
                    }

                    // 4) 클래식은 기존 코루틴 유지
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

    private IEnumerator CoEnterAdventureNextFrame(int idx0, string openPanel)
    {
        // 같은 프레임 클릭/리셋 충돌 방지
        yield return null;                 // 한 프레임 쉬고
        yield return new WaitForEndOfFrame(); // UI 정리까지 완료 보장(안정성↑)

        var bus = Game.Bus;

        // 게임 패널 다시 열기(클래식 경로와 동작 맞추기)
        bus.PublishSticky(new PanelToggle(openPanel, true), alsoEnqueue: false);
        bus.PublishImmediate(new PanelToggle(openPanel, true));
        bus.ClearSticky<GameOver>();

        // 0-based 전용 진입 (튜토리얼이 _mapList[0]일 때 안전하게 +1 보정됨)
        MapManager.Instance.EnterAdventureByIndex0(idx0);

        Debug.Log($"[AdventureRestart] re-enter idx0={idx0}");
    }

}
