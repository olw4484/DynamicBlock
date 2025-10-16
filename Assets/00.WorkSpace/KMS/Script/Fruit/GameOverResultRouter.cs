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
    private bool _handledThisDeath;
    private bool _suppressResults;
    private float _suppressUntil = 0f;
    private void OnEnable()
    {
        _shownOnce = false;
        _handledThisDeath = false;
        _suppressResults = false;

        if (hideAllOnEnable)
        {
            if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
            adventurePresenter?.HideAllPublic();
        }

        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;

            _onGoc = OnGameOverConfirmed;
            _onCont = _ => { _handledThisDeath = false; _suppressResults = false; Debug.Log("[ResultRouter] ContinueGranted → handled=false, suppress=OFF"); };
            _onCleared = OnAdventureCleared;
            _onFailed = OnAdventureFailed;
            _onReset = r =>
            {
                _shownOnce = false;
                _handledThisDeath = false;
                _suppressResults = false;

                _bus.ClearSticky<GameOver>();
                _bus.ClearSticky<GameOverConfirmed>();
                _bus.ClearSticky<AdventureStageCleared>();
                _bus.ClearSticky<AdventureStageFailed>();
            };

            // 다운 직후: 결과 라우팅 잠금
            _bus.Subscribe<PlayerDowned>(_ =>
            {
                _suppressResults = true;
                _shownOnce = false;
                if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
                adventurePresenter?.HideAllPublic();
                Debug.Log("[ResultRouter] PlayerDowned → suppress=ON");
            }, replaySticky: false);

            // 광고 시작: 결과 라우팅 잠금
            _bus.Subscribe<AdPlaying>(_ =>
            {
                _suppressResults = true;
                if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
                adventurePresenter?.HideAllPublic();
                Debug.Log("[ResultRouter] AdPlaying → suppress=ON");
            }, replaySticky: false);

            // 부활 확정 시: 해제
            _bus.Subscribe<RevivePerformed>(_ =>
            {
                _suppressResults = false;
                _handledThisDeath = false;
                Debug.Log("[ResultRouter] RevivePerformed → suppress=OFF, handled=false");
            }, replaySticky: false);

            _bus.Subscribe<ContinueGranted>(_ =>
            {
                _handledThisDeath = false;
                _suppressResults = true;
                _suppressUntil = Time.realtimeSinceStartup + 1.5f;
                HardCloseAllResults();
                // 스티키 이벤트도 정리
                _bus.ClearSticky<GameOver>();
                _bus.ClearSticky<GameOverConfirmed>();
                _bus.ClearSticky<AdventureStageCleared>();
                _bus.ClearSticky<AdventureStageFailed>();
                Debug.Log("[ResultRouter] ContinueGranted → suppress ON + hard close + clear stickies");
            }, replaySticky: false);

            // 포기(결과로 가는 루트): 해제
            _bus.Subscribe<GiveUpRequest>(_ =>
            {
                if (AdStateProbe.IsFullscreenShowing || AdStateProbe.IsRevivePending || ReviveGate.IsArmed)
                {
                    Debug.Log("[ResultRouter] Ignore GiveUpRequest during revive/ad");
                    return;
                }
                _suppressResults = false;
                Debug.Log("[ResultRouter] GiveUpRequest → suppress OFF (no revive/ad)");
            }, replaySticky: false);


            // 광고 종료: 토큰(보상) 없으면 결과 허용, 있으면(부활) 그대로 막힘 유지 로직은 RevivePerformed에서 해제
            _bus.Subscribe<AdFinished>(_ =>
            {
                if (!AdReviveToken.HasPending())
                {
                    _suppressResults = false;
                    Debug.Log("[ResultRouter] AdFinished (no token) → suppress=OFF");
                }
                else
                {
                    Debug.Log("[ResultRouter] AdFinished (token pending) → keep suppress");
                }
            }, replaySticky: false);

            _bus.Subscribe(_onGoc, replaySticky: false); // ★ 스티키 재생 금지
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

    private void HardCloseAllResults()
    {
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
        adventurePresenter?.HideAllPublic();
        _shownOnce = false;
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

    // 부활/연출이 끝나고 확정될 때(최종 분기)
    private void OnGameOverConfirmed(GameOverConfirmed e)
    {
        if (Time.realtimeSinceStartup < _suppressUntil)
        {
            Debug.Log("[ResultRouter] Suppress by time-guard after revive");
            _handledThisDeath = false;
            return;
        }

        if (_suppressResults || AdStateProbe.IsFullscreenShowing || AdStateProbe.IsRevivePending || ReviveGate.IsArmed)
        {
            Debug.Log($"[ResultRouter] Suppress GOC: sup={_suppressResults} full={AdStateProbe.IsFullscreenShowing} revive={AdStateProbe.IsRevivePending} gate={ReviveGate.IsArmed}");
            _handledThisDeath = false;
            return;
        }

        // 결과 라우팅 차단 조건(광고/부활/게이트/토큰/수동 suppress)
        bool blocked =
            _suppressResults ||
            AdStateProbe.IsFullscreenShowing ||
            AdStateProbe.IsRevivePending ||
            ReviveGate.IsArmed ||
            AdReviveToken.HasPending();

        Debug.Log($"[ResultRouter] GOC recv handled={_handledThisDeath} score={e.score} newBest={e.isNewBest} reason={e.reason} " +
                  $"blocked={blocked} sup={_suppressResults} full={AdStateProbe.IsFullscreenShowing} revive={AdStateProbe.IsRevivePending} " +
                  $"gate={ReviveGate.IsArmed} token={AdReviveToken.HasPending()}");

        if (blocked) return;           // 광고/부활 관련 상태면 무조건 무시
        if (_handledThisDeath) return; // 중복 방지
        _handledThisDeath = true;

        var mode = MapManager.Instance?.CurrentMode ?? GameMode.Classic;
        if (mode != GameMode.Adventure) return;

        var mm = MapManager.Instance;
        var sm = Game.Save;
        var md = mm?.CurrentMapData;

        var kind = (md != null) ? md.goalKind : (mm != null ? mm.CurrentGoalKind : MapGoalKind.Score);
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
        yield return null; // 다음 프레임
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);

        if (cleared) adventurePresenter?.ShowClearPublic(kind, score);
        else adventurePresenter?.ShowFailPublic(kind, score);
    }
}
