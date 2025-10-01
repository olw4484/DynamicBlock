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

    // ��� �߰�: ��� �� UI �׷� + ���� �����̴�/�� + ���� �հ��
    [Header("Mode Groups")]
    [SerializeField] private GameObject scoreGroup;      // ���� ���� �׷�
    [SerializeField] private GameObject fruitGroup;      // ���� ���� �׷�

    [Header("Score Group UI")]
    [SerializeField] private Slider scoreProgress;       // ��� �����̴�
    [SerializeField] private TMP_Text scoreProgressText; // "����/��ǥ"

    [Header("Fruit Group UI")]
    [SerializeField] private TMP_Text fruitTotalText;    // "����/��ǥ" ���� (���� ������ ������Ʈ ������Ʈ�� ����)

    [Header("Fruit Layout")]
    [SerializeField] private FruitBadgeLayoutRows fruitLayout; // ���� ���� 2�� ��ġ

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
        // UIManager�� �޾Ƽ� SetPanel("Adventure_Result", true) ����
        _bus?.PublishImmediate(new PanelToggle("Adventure_Result", true));
    }
    private void ClosePanel()
    {
        _bus?.PublishImmediate(new PanelToggle("Adventure_Result", false));
    }

    // �̺�Ʈ���� kind/score �� �� �޵��� ����
    private void OnCleared(AdventureStageCleared e) => ShowClear(e.kind, e.finalScore);
    private void OnFailed(AdventureStageFailed e) => ShowFail(e.kind, e.finalScore);

    // �ܺο��� ���� ȣ���� �� �ֵ��� �����ε嵵 ����
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

        // ��� ���� �α�(����)
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "clear");

        // ��ư ������ ���ε�(�ߺ� ����)
        var btn = Clear_Button ? Clear_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.LogEvent("Clear_Confirm");
                ClosePanel();
                // TODO: ���� �������� ���� �޽���/�� ��ȯ ȣ��

                // RestartOnClick.cs���� �����
                var sm = MapManager.Instance?.saveManager;

                var bus = Game.Bus;

                // 1) ���� ���� Ȯ���� ���� + ������ ����
                sm?.ClearRunState(save: true);
                sm?.SkipNextSnapshot("Restart");
                sm?.SuppressSnapshotsFor(1.0f);

                // 2) ���� �̺�Ʈ (BlockStorage.ResetRuntime ��)
                bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));

                string[] closeList = {"Adventure_Result"};

                // 3) UI ����/��ȯ
                RestartFlow.SoftReset("Game", closeList);
                
                StageManager.Instance.SetCurrentStage(StageManager.Instance.GetCurrentStage());
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

        // ��� ���� �α�
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "fail");

        // ��ư ������ ���ε�(�ߺ� ����)
        var btn = Fail_Button ? Fail_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.RetryLog(false);   // Adventure ��õ�
                ClosePanel();
                // TODO: ��Ʈ���� �޽���/�� ���� ȣ��
                // GIL_Add : 
                // ���� �������� ������ϱ�
                // RestartOnClick.cs���� �����
                var sm = MapManager.Instance?.saveManager;

                var bus = Game.Bus;

                // 1) ���� ���� Ȯ���� ���� + ������ ����
                sm?.ClearRunState(save: true);
                sm?.SkipNextSnapshot("Restart");
                sm?.SuppressSnapshotsFor(1.0f);

                // 2) ���� �̺�Ʈ (BlockStorage.ResetRuntime ��)
                bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));

                string[] closeList = {"Adventure_Result"};

                // 3) UI ����/��ȯ
                RestartFlow.SoftReset("Game", closeList);

                // end) ���� �������� �� �����ϱ�
                var curStage = StageManager.Instance.GetCurrentStage();
                MapManager.Instance.EnterStage(curStage + 1);
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

    // �ٽ�: ��庰 UI ���� & �����̴�/�� ä���
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

        // 0..1 ����ȭ�� ���
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

    // ���� ��忡�� ���� "����/��ǥ" ����
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

            // 1) "���� ����"�� �����ְ� ������ current ���
            //list.Add(new FruitReq { sprite = icon, count = current, achieved = done });

            // 2) "���� ����"�� �����ְ� ������ �� �� �� ��� �Ʒ� ���
            int remain = mm.GetFruitRemainingByCode(code);
            list.Add(new FruitReq { sprite = icon, count = remain, achieved = done });
        }
        fruitLayout.Show(list.ToArray());
    }
}
