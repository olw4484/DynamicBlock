using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using System.Collections;
using UnityEngine;

public sealed class GameOverResultRouter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject classicGameOverRoot;        // Classic_Game_Over 루트
    [SerializeField] private GameObject classicNewBestRoot;
    [SerializeField] private AdventureResultPresenter adventurePresenter; // ADResult_Canvas가 붙은 프리팹

    [Header("Options")]
    [SerializeField] private bool hideAllOnEnable = true;

    private EventQueue _bus;

    private System.Action<GameOverConfirmed> _onGoc;
    private System.Action<ContinueGranted> _onCont;
    private System.Action<AdventureStageCleared> _onCleared;
    private System.Action<AdventureStageFailed> _onFailed;
    private System.Action<GameResetRequest> _onReset;

    private bool _shownOnce;
    private bool _revived;
    bool _handledThisDeath;

    private void OnEnable()
    {
        _shownOnce = false;
        _revived = false;
        _handledThisDeath = false;

        if (hideAllOnEnable)
        {
            if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
            adventurePresenter?.HideAllPublic();
        }

        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;

            _onGoc = OnGameOverConfirmed;
            _onCont = _ => { _handledThisDeath = false; _revived = true; };
            _onCleared = OnAdventureCleared;
            _onFailed = OnAdventureFailed;
            _onReset = r =>
            {
                _shownOnce = false;
                _handledThisDeath = false;
                _revived = (r.reason == ResetReason.Restart) || _revived;
                _bus.ClearSticky<GameOver>();
                _bus.ClearSticky<GameOverConfirmed>();
                _bus.ClearSticky<AdventureStageCleared>();
                _bus.ClearSticky<AdventureStageFailed>();
            };

            _bus.Subscribe(_onGoc, replaySticky: true);
            _bus.Subscribe(_onCont, replaySticky: false);
            _bus.Subscribe(_onCleared, replaySticky: false);
            _bus.Subscribe(_onFailed, replaySticky: false);
            _bus.Subscribe(_onReset, replaySticky: false);
        }));
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        if (_onGoc != null) _bus.Unsubscribe(_onGoc);
        if (_onCont != null) _bus.Unsubscribe(_onCont);
        if (_onCleared != null) _bus.Unsubscribe(_onCleared);
        if (_onFailed != null) _bus.Unsubscribe(_onFailed);
        if (_onReset != null) _bus.Unsubscribe(_onReset);
    }
    private void OnAdventureCleared(AdventureStageCleared e)
    {
        if (_shownOnce) return;
        if ((MapManager.Instance?.CurrentMode ?? GameMode.Classic) != GameMode.Adventure) return;

        _shownOnce = true;
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
        StartCoroutine(Co_ShowAdResultNextFrame(true, e.kind, e.finalScore));
    }


    private void OnAdventureFailed(AdventureStageFailed e)
    {
        if (_shownOnce) return;
        if ((MapManager.Instance?.CurrentMode ?? GameMode.Classic) != GameMode.Adventure) return;

        _shownOnce = true;
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
        StartCoroutine(Co_ShowAdResultNextFrame(false, e.kind, e.finalScore));
    }

    // 공통: 부활/연출이 끝나고 확정될 때(최종 분기)
    private void OnGameOverConfirmed(GameOverConfirmed e)
    {
        if (_handledThisDeath) return;
        _handledThisDeath = true;

        if (_revived) { _revived = false; return; }

        var mode = MapManager.Instance?.CurrentMode ?? GameMode.Classic;

        if (mode != GameMode.Adventure) return;

        var mm = MapManager.Instance;
        var sm = Game.Save;
        var md = mm?.CurrentMapData;

        MapGoalKind kind = md ? md.goalKind : (mm != null ? mm.CurrentGoalKind : MapGoalKind.Score);
        bool cleared = ComputeAdventureCleared(kind, md);

        if (cleared)
        {
            var ev = new AdventureStageCleared(kind, e.score);
            _bus.PublishSticky(ev, alsoEnqueue: false);
            _bus.PublishImmediate(ev);
            Sfx.StageClear();
        }
        else
        {
            var ev = new AdventureStageFailed(kind, e.score);
            _bus.PublishSticky(ev, alsoEnqueue: false);
            _bus.PublishImmediate(ev);
            Sfx.Stagefail();
        }

        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
        sm?.ClearRunState(true);
    }

    private bool ComputeAdventureCleared(MapGoalKind kind, MapData md)
    {
        switch (kind)
        {
            case MapGoalKind.Score:
                int goal = Mathf.Max(1, md?.scoreGoal ?? 1);
                int cur = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
                return cur >= goal;

            case MapGoalKind.Fruit:
                return MapManager.Instance != null && MapManager.Instance.IsAllFruitCleared();

            default:
                return false;
        }
    }

    private IEnumerator Co_ShowAdResultNextFrame(bool cleared, MapGoalKind kind, int score)
    {
        yield return null; // 다음 프레임에 덮어씀
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);

        if (cleared) adventurePresenter?.ShowClearPublic(kind, score);
        else adventurePresenter?.ShowFailPublic(kind, score);
    }
}
