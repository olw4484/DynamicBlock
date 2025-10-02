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
    private bool _shownOnce;
    private bool _revived;

    private void OnEnable()
    {
        _shownOnce = false;
        if (hideAllOnEnable)
        {
            if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
            adventurePresenter?.HideAllPublic();
        }

        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;

            _bus.Subscribe<GameOverConfirmed>(OnGameOverConfirmed, replaySticky: false);
            _bus.Subscribe<AdventureStageCleared>(OnAdventureCleared, replaySticky: false);
            _bus.Subscribe<AdventureStageFailed>(OnAdventureFailed, replaySticky: false);

            _bus.Subscribe<GameResetRequest>(r => {
                _shownOnce = false;
                _revived = (r.reason == ResetReason.Restart);
                _bus.ClearSticky<GameOver>();
                _bus.ClearSticky<GameOverConfirmed>();
                _bus.ClearSticky<AdventureStageCleared>();
                _bus.ClearSticky<AdventureStageFailed>();
            }, replaySticky: false);
        }));
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GameOverConfirmed>(OnGameOverConfirmed);
        _bus.Unsubscribe<AdventureStageCleared>(OnAdventureCleared);
        _bus.Unsubscribe<AdventureStageFailed>(OnAdventureFailed);
        _bus.Unsubscribe<GameResetRequest>(OnGameResetRequest);
    }

    private void OnGameResetRequest(GameResetRequest _)
    {
        // 새 게임 진입 직전 항상 깨끗이
        _shownOnce = false;
        _bus.ClearSticky<AdventureStageCleared>();
        _bus.ClearSticky<AdventureStageFailed>();
        _bus.ClearSticky<GameOverConfirmed>();
        _bus.ClearSticky<GameOver>();
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
        if (_revived) { _revived = false; return; }
        var mm = MapManager.Instance;
        var sm = Game.Save;
        var mode = mm?.CurrentMode ?? GameMode.Classic;
        Debug.Log($"[ResultRouter] GOC: mode={mode}, score={e.score}, reason={e.reason}, newBest={e.isNewBest}");

        if (mode == GameMode.Adventure)
        {
            var md = mm?.CurrentMapData;
            MapGoalKind kind = MapGoalKind.Score;
             if (md != null) kind = md.goalKind;
             else if (mm != null) kind = mm.CurrentGoalKind;

            bool cleared = ComputeAdventureCleared(kind, md);

            // Sticky 로 발행 (놓쳐도 재생)
            if (cleared)
            {
                var ev = new AdventureStageCleared(kind, e.score);
                _bus.PublishSticky(ev, alsoEnqueue: false);
                _bus.PublishImmediate(ev);
            }
            else
            {
                var ev = new AdventureStageFailed(kind, e.score);
                _bus.PublishSticky(ev, alsoEnqueue: false);
                _bus.PublishImmediate(ev);
            }
            if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
            // 사운드
            if (cleared) Sfx.StageClear();
            else Sfx.Stagefail();

            sm?.ClearRunState(true);
            return;
        }

        // ===== Classic =====
        sm?.UpdateClassicScore(e.score);
        sm?.ClearRunState(true);
        // 1) UI 분기
        if (classicNewBestRoot != null)
        {
            classicNewBestRoot.SetActive(e.isNewBest);
            if (classicGameOverRoot) classicGameOverRoot.SetActive(!e.isNewBest);
        }
        else
        {
            // 신기록 루트가 없다면 기존 게임오버 루트만 ON (프로젝트 상황에 맞게 프리젠터 호출로 바꿔도 OK)
            if (classicGameOverRoot) classicGameOverRoot.SetActive(true);
        }

        // 2) 사운드도 분기
        if (e.isNewBest) Sfx.NewRecord();
        else Sfx.GameOver();

        // 3) 저장/런상태 정리 (UI 켠 뒤에 해도 무방)
        sm?.UpdateClassicScore(e.score);
        sm?.ClearRunState(true);

        Sfx.GameOver();
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
