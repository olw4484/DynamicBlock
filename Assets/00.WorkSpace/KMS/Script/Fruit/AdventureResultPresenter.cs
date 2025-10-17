using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class AdventureResultPresenter : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject ADResult_Canvas;
    [SerializeField] private string panelKey = "Adventure_Result";

    [Header("Clear")]
    [SerializeField] private GameObject ClearBG;
    [SerializeField] private GameObject ADResult_ClearPanel;
    [SerializeField] private TMP_Text Clear_ResultScore;
    [SerializeField] private GameObject Clear_Button;

    [Header("Fail")]
    [SerializeField] private GameObject FailBG;
    [SerializeField] private GameObject ADResult_FailPanel;
    [SerializeField] private TMP_Text Fail_ResultScore;
    [SerializeField] private GameObject Fail_Button;

    // 모드 별 UI 그룹 + 점수 슬라이더/라벨 + 과일 합계라벨
    [Header("Mode Groups")]
    [SerializeField] private GameObject scoreGroup;      // 점수 모드용 그룹
    [SerializeField] private GameObject fruitGroup;      // 과일 모드용 그룹

    [Header("Score Group UI")]
    [SerializeField] private Slider scoreProgress;       // 가운데 슬라이더
    [SerializeField] private TMP_Text scoreProgressText; // "현재/목표"

    [Header("Fruit Group UI")]
    [SerializeField] private TMP_Text fruitTotalText;    // "수집/목표" 총합
    [SerializeField] private bool useFruitTotalText = false;

    [Header("Fruit Layout")]
    [SerializeField] private FruitBadgeLayoutRows fruitLayout; // 과일 뱃지 2열 배치

    private EventQueue _bus;
    private Coroutine _scoreTween;
    private bool _wroteResultThisRun = false;
    private bool _isOpen;

    private bool InAdventure => MapManager.Instance?.CurrentMode == GameMode.Adventure;

    [ContextMenu("Dump ADResult State")]
    void DumpState()
    {
        Debug.Log(
          $"[ADResult.Dump] " +
          $"scoreGroup={(scoreGroup ? scoreGroup.activeSelf.ToString() : "null")} " +
          $"fruitGroup={(fruitGroup ? fruitGroup.activeSelf.ToString() : "null")} " +
          $"ClearBG={(ClearBG ? ClearBG.activeSelf.ToString() : "null")} " +
          $"FailBG={(FailBG ? FailBG.activeSelf.ToString() : "null")}");
    }
    void Awake()
    {
        if (!scoreGroup || !fruitGroup)
            Debug.LogError("[ADResult] scoreGroup/fruitGroup reference missing in inspector!");
        HideAll();
    }

    private void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;
            if (_bus == null) return;

            _bus.Subscribe<AdventureStageCleared>(OnCleared, replaySticky: false);
            _bus.Subscribe<AdventureStageFailed>(OnFailed, replaySticky: false);
            _bus.Subscribe<GameResetRequest>(OnGameReset, replaySticky: false);

            if (MapManager.Instance?.CurrentMode != GameMode.Adventure)
            {
                _bus.ClearSticky<AdventureStageCleared>();
                _bus.ClearSticky<AdventureStageFailed>();
            }
        }));
    }
    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<AdventureStageCleared>(OnCleared);
        _bus.Unsubscribe<AdventureStageFailed>(OnFailed);
        _bus.Unsubscribe<GameResetRequest>(OnGameReset);
    }

    private void OnGameReset(GameResetRequest _)
    {
        _bus.ClearSticky<AdventureStageCleared>();
        _bus.ClearSticky<AdventureStageFailed>();
        _bus.ClearSticky<GameOverConfirmed>();
        _bus.ClearSticky<GameOver>();
        HideAll();
        _wroteResultThisRun = false;
        _isOpen = false;
    }

    private void OpenPanel()
    {
        if (!InAdventure) return;   // 비어드벤처에서 열지 않음
        _bus?.PublishImmediate(new PanelToggle(panelKey, true));
    }

    private void ClosePanel() => _bus?.PublishImmediate(new PanelToggle(panelKey, false));

    // 이벤트에서 kind/score 둘 다 받도록 변경
    private void OnCleared(AdventureStageCleared e)
    {
        if (!InAdventure) { Debug.Log("[ADResult] Ignore CLEARED (not Adventure)"); return; }
        ShowClear(ResolveKind(e.kind), e.finalScore);
    }
    private void OnFailed(AdventureStageFailed e)
    {
        if (!InAdventure) { Debug.Log("[ADResult] Ignore FAILED (not Adventure)"); return; }
        ShowFail(ResolveKind(e.kind), e.finalScore);
    }

    // 외부에서 직접 호출할 수 있도록 오버로드도 변경
    public void ShowClearPublic(MapGoalKind kind, int score) => ShowClear(kind, score);
    public void ShowFailPublic(MapGoalKind kind, int score) => ShowFail(kind, score);
    public void HideAllPublic() => HideAll();

    private void ShowClear(MapGoalKind kind, int score)
    {
        if (_isOpen) return;
        _isOpen = true;
        kind = ResolveKind(kind);
        OpenPanel();
        //if (ADResult_Canvas) ADResult_Canvas.SetActive(true);

        if (Fail_Button) Fail_Button.SetActive(false);
        if (FailBG) FailBG.SetActive(false);
        if (ADResult_FailPanel) ADResult_FailPanel.SetActive(false);

        if (ClearBG) ClearBG.SetActive(true);
        if (Clear_Button) Clear_Button.SetActive(true);
        if (ADResult_ClearPanel) ADResult_ClearPanel.SetActive(true);
        if (Clear_ResultScore) Clear_ResultScore.text = score.ToString("N0");
        // Firestore 기록 (성공)
        if (!_wroteResultThisRun)
        {
            int idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0; // 0-based
            string stageName = StageManager.Instance?.GetCurrentStageName() ?? $"Stage_{idx0}";
            FirestoreManager.EnsureInitialized();
            FirestoreManager.Instance?.WriteStageData(idx0, true, stageName);
            _wroteResultThisRun = true;
        }

        // 결과 노출 로깅
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "clear");

        // 버튼 리스너 바인딩(중복 방지)
        var btn = Clear_Button ? Clear_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();

            int currIdx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;
            int nextIdx0 = currIdx0 + 1; // 다음 스테이지 (0-based)

            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.LogEvent("Clear_Confirm");
                // 클릭스루 방지: 코루틴에서 다음 프레임에 처리
                StartCoroutine(Co_GoNextAdventure(nextIdx0, currIdx0));
            });
        }

        if (EventSystem.current && btn) EventSystem.current.SetSelectedGameObject(btn.gameObject);

        ApplyModeUI(kind, score);

        StartCoroutine(Co_ShowInterstitialAfterOpen());
    }

    private void ShowFail(MapGoalKind kind, int score)
    {
        if (_isOpen) return;
        _isOpen = true;
        Debug.Log($"[ADResult] FAILED kind={kind} score={score}");
        kind = ResolveKind(kind);
        OpenPanel();
        //if (ADResult_Canvas) ADResult_Canvas.SetActive(true);

        if (Clear_Button) Clear_Button.SetActive(false);
        if (ClearBG) ClearBG.SetActive(false);
        if (ADResult_ClearPanel) ADResult_ClearPanel.SetActive(false);

        if (FailBG) FailBG.SetActive(true);
        if (Fail_Button) Fail_Button.SetActive(true);
        if (ADResult_FailPanel) ADResult_FailPanel.SetActive(true);
        if (Fail_ResultScore) Fail_ResultScore.text = score.ToString("N0");

        // Firestore 기록(실패)
        if (!_wroteResultThisRun)
        {
            int idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0; // 0-based
            string stageName = StageManager.Instance?.GetCurrentStageName() ?? $"Stage_{idx0}";
            FirestoreManager.EnsureInitialized();
            FirestoreManager.Instance?.WriteStageData(idx0, false, stageName);
            _wroteResultThisRun = true;
        }

        // 결과 노출 로깅
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "fail");

        // 버튼 리스너 바인딩(중복 방지)
        var btn = Fail_Button ? Fail_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();

            // 바인딩 시점 스냅샷(0-based)
            int capturedIdx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;

            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.RetryLog(false);
                // 클릭스루 방지: 코루틴에서 다음 프레임에 처리
                StartCoroutine(Co_RetryAdventure(capturedIdx0));
            });
        }

        if (EventSystem.current && btn) EventSystem.current.SetSelectedGameObject(btn.gameObject);

        ApplyModeUI(kind, score);

        StartCoroutine(Co_ShowInterstitialAfterOpen());
    }

    // =======================
    // 코루틴
    // =======================
    private IEnumerator Co_RetryAdventure(int idx0)
    {
        yield return null;

        // 완전 초기화
        StageManager.ForceCleanRunStateAndBoard();

        // Reset 전에 모드/골세팅/리바이브 정책 '선지정'
        var mm = MapManager.Instance;
        if (mm)
        {
            mm.SetGameMode(GameMode.Adventure);
            var kind = mm.CurrentMapData ? mm.CurrentMapData.goalKind : MapGoalKind.Score;
            mm.SetGoalKind(kind);
        }
        ReviveRouter.I?.SetPolicyFromMode(GameMode.Adventure);

        // Reset 실행 및 완료 대기
        var bus = Game.Bus;
        bool resetDone = false;
        System.Action<GameResetDone> onDone = null;
        onDone = _ => { resetDone = true; Game.Bus.Unsubscribe(onDone); };
        Game.Bus.Subscribe(onDone, replaySticky: false);
        bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));
        while (!resetDone) yield return null;

        // Adventure 결과 패널 닫고 게임 진입
        var ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        ui?.SetPanel(panelKey, false);   // "Adventure_Result"
        ui?.SetPanel("Game", true);

        StageManager.Instance.EnterStageByIndex(idx0, AdventureEnterPolicy.ForceNew, "RetryFromResult");
    }

    private IEnumerator Co_GoNextAdventure(int nextIdx0, int prevIdx0)
    {
        yield return null;

        StageManager.ForceCleanRunStateAndBoard();

        var bus = Game.Bus;
        bool resetDone = false;
        System.Action<GameResetDone> onDone = null;
        onDone = _ => { resetDone = true; Game.Bus.Unsubscribe(onDone); };
        Game.Bus.Subscribe(onDone, replaySticky: false);
        bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));
        while (!resetDone) yield return null;

        var ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        ui?.SetPanel(panelKey, false);
        ui?.SetPanel("Game", true);

        StageManager.Instance.SetStageClearAndActivateNext();
        StageManager.Instance.EnterStageByIndex(nextIdx0, AdventureEnterPolicy.ForceNew, "NextFromResult");
    }

    private IEnumerator Co_ShowInterstitialAfterOpen()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        AdManager.Instance.ShowInterstitial();
    }

    private void HideAll()
    {
        //if (ADResult_Canvas) ADResult_Canvas.SetActive(false);
        if (ClearBG) ClearBG.SetActive(false);
        if (ADResult_ClearPanel) ADResult_ClearPanel.SetActive(false);
        if (FailBG) FailBG.SetActive(false);
        if (ADResult_FailPanel) ADResult_FailPanel.SetActive(false);
        if (Clear_Button) Clear_Button.SetActive(false);
        if (Fail_Button) Fail_Button.SetActive(false);

        if (scoreGroup) scoreGroup.SetActive(false);
        if (fruitGroup) fruitGroup.SetActive(false);
        if (_scoreTween != null) { StopCoroutine(_scoreTween); _scoreTween = null; }
        if (scoreProgress) scoreProgress.value = 0f;
        if (scoreProgressText) scoreProgressText.text = "";
        if (Clear_ResultScore) Clear_ResultScore.text = "";
        if (Fail_ResultScore) Fail_ResultScore.text = "";
        _wroteResultThisRun = false;
    }

    // 핵심: 모드별 UI 세팅 & 슬라이더/라벨 채우기
    private void ApplyModeUI(MapGoalKind kind, int finalScore)
    {
        bool isScore = (kind == MapGoalKind.Score);
        if (scoreGroup) scoreGroup.SetActive(isScore);
        if (fruitGroup) fruitGroup.SetActive(!isScore);

        if (isScore)
        {
            int goal = Mathf.Max(1, MapManager.Instance?.CurrentMapData?.scoreGoal ?? 1);
            ConfigureScoreSlider(goal, finalScore);
        }
        else
        {
            if (_scoreTween != null) { StopCoroutine(_scoreTween); _scoreTween = null; }
            if (scoreProgress) scoreProgress.value = 0f;

            // 과일 UI는 프레임 끝에서 갱신(타이밍 이슈 방지)
            StartCoroutine(Co_RepaintFruitUIEndOfFrame());
        }
    }

    private IEnumerator Co_RepaintFruitUIEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        RebuildFruitBadgesForResult();
        if (useFruitTotalText) UpdateFruitProgressText();  // 원하면 합계만 표시
    }
    private void ConfigureScoreSlider(int goal, int curr)
    {
        if (!scoreProgress) return;

        // 0..1 정규화만 사용
        scoreProgress.wholeNumbers = false;
        scoreProgress.minValue = 0f;
        scoreProgress.maxValue = 1f;

        float norm = Mathf.Clamp01(curr / (float)goal);

        if (_scoreTween != null) StopCoroutine(_scoreTween);
        _scoreTween = StartCoroutine(TweenSlider(scoreProgress, norm, 0.25f));

        if (scoreProgressText)
            scoreProgressText.text = $"{curr:N0}/{goal:N0}";
    }

    private IEnumerator TweenSlider(Slider s, float to, float dur)
    {
        float from = s.value, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            s.value = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        s.value = to;
    }

    // 과일 모드에서 총합 "수집/목표" 갱신
    private void UpdateFruitProgressText()
    {
        var mm = MapManager.Instance;
        if (!mm) { if (fruitTotalText) fruitTotalText.text = "-"; return; }

        var codes = mm.ActiveFruitCodes;
        int totalTarget = 0, totalCurrent = 0;

        if (codes != null)
        {
            foreach (var code in codes)
            {
                totalTarget += mm.GetInitialFruitGoalByCode(code);
                totalCurrent += mm.GetFruitCurrentByCode(code);
            }

            if (fruitTotalText) fruitTotalText.text = $"{totalCurrent}/{totalTarget}";
        }
    }
    private void RebuildFruitBadgesForResult()
    {
        if (!fruitLayout) return;

        var mm = MapManager.Instance;
        var codes = mm?.ActiveFruitCodes;
        if (mm == null || codes == null || codes.Count == 0)
        { fruitLayout.Show(null); return; }

        var list = new List<FruitReq>(codes.Count);
        foreach (var code in codes)
        {
            int target = mm.GetInitialFruitGoalByCode(code);
            int current = mm.GetFruitCurrentByCode(code);
            bool done = current >= target;
            Sprite icon = mm.GetFruitIconByCode(code);

            // 1) "모은 개수"를 보여주고 싶으면 current 사용
            //list.Add(new FruitReq { sprite = icon, count = current, achieved = done });

            // 2) "남은 개수"를 보여주고 싶으면 위 한 줄 대신 아래 사용
            int remain = mm.GetFruitRemainingByCode(code);
            list.Add(new FruitReq { sprite = icon, count = remain, achieved = done });
        }
        fruitLayout.Show(list.ToArray());
    }

    private MapGoalKind ResolveKind(MapGoalKind fromEvent)
    {
        var mm = MapManager.Instance;
        if (fromEvent != MapGoalKind.None) return fromEvent;

        var byMap = mm?.CurrentMapData?.goalKind ?? MapGoalKind.Score;
        // 맵은 Score라고 쓰여 있는데 과일 미션 코드가 실시간으로 존재하면 과일로 보정
        if (byMap == MapGoalKind.Score && (mm?.ActiveFruitCodes?.Count ?? 0) > 0)
            return MapGoalKind.Fruit;

        return byMap;
    }

    private static void ForceCleanRunStateAndBoard()
    {
        var sm = MapManager.Instance?.saveManager;
        sm?.ClearRunState(save: true);
        sm?.SkipNextSnapshot("AdventureForceNew");
        sm?.SuppressSnapshotsFor(1.0f);

        GridManager.Instance?.ResetBoardToEmpty();
        ScoreManager.Instance?.ResetRuntime();

        var bs = UnityEngine.Object.FindFirstObjectByType<BlockStorage>();
        if (bs) bs.ClearHand();

        ReviveGate.Disarm();
        AdStateProbe.IsRevivePending = false;
        UIStateProbe.ResetAllShields();
        GameOverUtil.ResetAll("adventure-force-new");
    }
}
