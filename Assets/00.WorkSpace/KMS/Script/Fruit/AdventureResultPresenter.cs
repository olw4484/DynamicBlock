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
    [SerializeField] private Key Adventure_Result;

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

    // ▼▼ 추가: 모드 별 UI 그룹 + 점수 슬라이더/라벨 + 과일 합계라벨
    [Header("Mode Groups")]
    [SerializeField] private GameObject scoreGroup;      // 점수 모드용 그룹
    [SerializeField] private GameObject fruitGroup;      // 과일 모드용 그룹

    [Header("Score Group UI")]
    [SerializeField] private Slider scoreProgress;       // 가운데 슬라이더
    [SerializeField] private TMP_Text scoreProgressText; // "현재/목표"

    [Header("Fruit Group UI")]
    [SerializeField] private TMP_Text fruitTotalText;    // "수집/목표" 총합 (개별 뱃지는 프로젝트 컴포넌트에 연결)

    [Header("Fruit Layout")]
    [SerializeField] private FruitBadgeLayoutRows fruitLayout; // 과일 뱃지 2열 배치

    private EventQueue _bus;
    private Coroutine _scoreTween;

    private void Awake() => HideAll();

    private void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;
            _bus.Subscribe<AdventureStageCleared>(OnCleared, replaySticky: false);
            _bus.Subscribe<AdventureStageFailed>(OnFailed, replaySticky: false);
        }));
    }
    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<AdventureStageCleared>(OnCleared);
        _bus.Unsubscribe<AdventureStageFailed>(OnFailed);
    }

    private void OpenPanel()
    {
        // UIManager가 받아서 SetPanel("Adventure_Result", true) 수행
        _bus?.PublishImmediate(new PanelToggle("Adventure_Result", true));
    }
    private void ClosePanel()
    {
        _bus?.PublishImmediate(new PanelToggle("Adventure_Result", false));
    }

    // 이벤트에서 kind/score 둘 다 받도록 변경
    private void OnCleared(AdventureStageCleared e) => ShowClear(e.kind, e.finalScore);
    private void OnFailed(AdventureStageFailed e) => ShowFail(e.kind, e.finalScore);

    // 외부에서 직접 호출할 수 있도록 오버로드도 변경
    public void ShowClearPublic(MapGoalKind kind, int score) => ShowClear(kind, score);
    public void ShowFailPublic(MapGoalKind kind, int score) => ShowFail(kind, score);
    public void HideAllPublic() => HideAll();

    private void ShowClear(MapGoalKind kind, int score)
    {
        OpenPanel();
        if (ADResult_Canvas) ADResult_Canvas.SetActive(true);

        if (Fail_Button) Fail_Button.SetActive(false);
        if (FailBG) FailBG.SetActive(false);
        if (ADResult_FailPanel) ADResult_FailPanel.SetActive(false);

        if (ClearBG) ClearBG.SetActive(true);
        if (Clear_Button) Clear_Button.SetActive(true);
        if (ADResult_ClearPanel) ADResult_ClearPanel.SetActive(true);
        if (Clear_ResultScore) Clear_ResultScore.text = score.ToString("N0");

        // 결과 노출 로깅(선택)
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "clear");

        // 버튼 리스너 바인딩(중복 방지)
        var btn = Clear_Button ? Clear_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.LogEvent("Clear_Confirm");
                ClosePanel();
                // TODO: 다음 스테이지 진입 메시지/씬 전환 호출
                StageManager.Instance.SetCurrentStage(StageManager.Instance.GetCurrentStage() + 1);
                MapManager.Instance.EnterStage(StageManager.Instance.GetCurrentStage());
            });
        }

        if (EventSystem.current && btn) EventSystem.current.SetSelectedGameObject(btn.gameObject);

        ApplyModeUI(kind, score);
    }

    private void ShowFail(MapGoalKind kind, int score)
    {
        OpenPanel();
        if (ADResult_Canvas) ADResult_Canvas.SetActive(true);

        if (Clear_Button) Clear_Button.SetActive(false);
        if (ClearBG) ClearBG.SetActive(false);
        if (ADResult_ClearPanel) ADResult_ClearPanel.SetActive(false);

        if (FailBG) FailBG.SetActive(true);
        if (Fail_Button) Fail_Button.SetActive(true);
        if (ADResult_FailPanel) ADResult_FailPanel.SetActive(true);
        if (Fail_ResultScore) Fail_ResultScore.text = score.ToString("N0");

        // 결과 노출 로깅
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "fail");

        // 버튼 리스너 바인딩(중복 방지)
        var btn = Fail_Button ? Fail_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.RetryLog(false);   // Adventure 재시도
                ClosePanel();
                // TODO: 리트라이 메시지/씬 리셋 호출
                // GIL_Add : 
                // 현재 스테이지 재시작하기
                // RestartOnClick.cs에서 들고옴
                var sm = MapManager.Instance?.saveManager;

                var bus = Game.Bus;

                // 1) 저장 상태 확실히 삭제 + 스냅샷 억제
                sm?.ClearRunState(save: true);
                sm?.SkipNextSnapshot("Restart");
                sm?.SuppressSnapshotsFor(1.0f);

                // 2) 리셋 이벤트 (BlockStorage.ResetRuntime 등)
                bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));

                string[] closeList = {"Adventure_Result"};

                // 3) UI 정리/전환
                RestartFlow.SoftReset("Game", closeList);

                // end) 현재 스테이지 재 입장하기
                MapManager.Instance.EnterStage(StageManager.Instance.GetCurrentStage());
            });
        }

        if (EventSystem.current && btn) EventSystem.current.SetSelectedGameObject(btn.gameObject);

        ApplyModeUI(kind, score);
    }

    private void HideAll()
    {
        if (ADResult_Canvas) ADResult_Canvas.SetActive(false);
        if (ClearBG) ClearBG.SetActive(false);
        if (ADResult_ClearPanel) ADResult_ClearPanel.SetActive(false);
        if (FailBG) FailBG.SetActive(false);
        if (ADResult_FailPanel) ADResult_FailPanel.SetActive(false);
        if (Clear_Button) Clear_Button.SetActive(false);
        if (Fail_Button) Fail_Button.SetActive(false);

        if (scoreGroup) scoreGroup.SetActive(false);
        if (fruitGroup) fruitGroup.SetActive(false);
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
            RebuildFruitBadgesForResult();
            UpdateFruitProgressText();
        }
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
}
