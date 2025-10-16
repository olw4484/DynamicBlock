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

    // ��� �� UI �׷� + ���� �����̴�/�� + ���� �հ��
    [Header("Mode Groups")]
    [SerializeField] private GameObject scoreGroup;      // ���� ���� �׷�
    [SerializeField] private GameObject fruitGroup;      // ���� ���� �׷�

    [Header("Score Group UI")]
    [SerializeField] private Slider scoreProgress;       // ��� �����̴�
    [SerializeField] private TMP_Text scoreProgressText; // "����/��ǥ"

    [Header("Fruit Group UI")]
    [SerializeField] private TMP_Text fruitTotalText;    // "����/��ǥ" ����
    [SerializeField] private bool useFruitTotalText = false;

    [Header("Fruit Layout")]
    [SerializeField] private FruitBadgeLayoutRows fruitLayout; // ���� ���� 2�� ��ġ

    private EventQueue _bus;
    private Coroutine _scoreTween;
    private System.Action<GameResetRequest> _onReset;
    private bool _wroteResultThisRun = false;
    private void Awake() => HideAll();

    private void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;
            _bus.Subscribe<AdventureStageCleared>(OnCleared, replaySticky: false);
            _bus.Subscribe<AdventureStageFailed>(OnFailed, replaySticky: false);
            _bus.Subscribe<GameResetRequest>(OnGameReset, replaySticky: false);
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
    }

    private void OpenPanel()
    {
        _bus?.PublishImmediate(new PanelToggle(panelKey, true));
    }
    private void ClosePanel()
    {
        _bus?.PublishImmediate(new PanelToggle(panelKey, false));
    }

    // �̺�Ʈ���� kind/score �� �� �޵��� ����
    private void OnCleared(AdventureStageCleared e)
    {
        Debug.Log($"[ADResult] CLEARED kind={e.kind}, score={e.finalScore}, mapGoal={MapManager.Instance?.CurrentMapData?.goalKind}");
        ShowClear(e.kind, e.finalScore);
    }
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
        // Firestore ��� (����)
        if (!_wroteResultThisRun)
        {
            int idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0; // 0-based
            string stageName = StageManager.Instance?.GetCurrentStageName() ?? $"Stage_{idx0}";
            FirestoreManager.EnsureInitialized();
            FirestoreManager.Instance?.WriteStageData(idx0, true, stageName);
            _wroteResultThisRun = true;
        }

        // ��� ���� �α�
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "clear");

        // ��ư ������ ���ε�(�ߺ� ����)
        var btn = Clear_Button ? Clear_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();

            int currIdx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;
            int nextIdx0 = currIdx0 + 1; // ���� �������� (0-based)

            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.LogEvent("Clear_Confirm");
                // Ŭ������ ����: �ڷ�ƾ���� ���� �����ӿ� ó��
                StartCoroutine(Co_GoNextAdventure(nextIdx0, currIdx0));
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

        // Firestore ���(����)
        if (!_wroteResultThisRun)
        {
            int idx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0; // 0-based
            string stageName = StageManager.Instance?.GetCurrentStageName() ?? $"Stage_{idx0}";
            FirestoreManager.EnsureInitialized();
            FirestoreManager.Instance?.WriteStageData(idx0, false, stageName);
            _wroteResultThisRun = true;
        }

        // ��� ���� �α�
        AnalyticsManager.Instance?.LogEvent("Result_Shown", "result", "fail");

        // ��ư ������ ���ε�(�ߺ� ����)
        var btn = Fail_Button ? Fail_Button.GetComponent<Button>() : null;
        if (btn)
        {
            btn.onClick.RemoveAllListeners();

            // ���ε� ���� ������(0-based)
            int capturedIdx0 = StageManager.Instance ? StageManager.Instance.GetCurrentStage() : 0;

            btn.onClick.AddListener(() =>
            {
                AnalyticsManager.Instance?.RetryLog(false);
                // Ŭ������ ����: �ڷ�ƾ���� ���� �����ӿ� ó��
                StartCoroutine(Co_RetryAdventure(capturedIdx0));
            });
        }

        if (EventSystem.current && btn) EventSystem.current.SetSelectedGameObject(btn.gameObject);

        ApplyModeUI(kind, score);
    }

    // =======================
    // �ڷ�ƾ
    // =======================
    private IEnumerator Co_RetryAdventure(int idx0)
    {
        // ���� ������ Ŭ������ ����
        yield return null;

        var ui = Object.FindFirstObjectByType<UIManager>();
        var sm = MapManager.Instance?.saveManager;
        var bus = Game.Bus;

        // ��Ÿ��/������ ����
        sm?.ClearRunState(save: true);
        sm?.SkipNextSnapshot("Restart");
        sm?.SuppressSnapshotsFor(1.0f);

        // ���� ����: UIManager�� ���ο��� Game/Main ���
        bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));

        // �� ������ ��ٷ� ��ȯ ������������ ��������
        yield return null;

        // ��� �гθ� Ȯ���� ����, Game �г��� Ȯ���� �Ҵ�(������)
        ui?.SetPanel(panelKey, false);   // "Adventure_Result"
        ui?.SetPanel("Game", true);      // �г� Ű ö�� Ȯ��!

        // ���� ���������� ����
        MapManager.Instance.EnterAdventureByIndex0(idx0);
    }

    private IEnumerator Co_GoNextAdventure(int nextIdx0, int prevIdx0)
    {
        yield return null;

        var ui = Object.FindFirstObjectByType<UIManager>();
        var sm = MapManager.Instance?.saveManager;
        var bus = Game.Bus;

        sm?.ClearRunState(save: true);
        sm?.SkipNextSnapshot("Restart");
        sm?.SuppressSnapshotsFor(1.0f);

        bus.PublishImmediate(new GameResetRequest("Game", ResetReason.Restart));
        yield return null;

        ui?.SetPanel(panelKey, false);
        ui?.SetPanel("Game", true);

        StageManager.Instance.SetStageClearAndActivateNext();
        MapManager.Instance.EnterAdventureByIndex0(nextIdx0);
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
        if (_scoreTween != null) { StopCoroutine(_scoreTween); _scoreTween = null; }
        if (scoreProgress) scoreProgress.value = 0f;
        if (scoreProgressText) scoreProgressText.text = "";
        if (Clear_ResultScore) Clear_ResultScore.text = "";
        if (Fail_ResultScore) Fail_ResultScore.text = "";
        _wroteResultThisRun = false;
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
            if (_scoreTween != null) { StopCoroutine(_scoreTween); _scoreTween = null; }
            if (scoreProgress) scoreProgress.value = 0f;

            // ���� UI�� ������ ������ ����(Ÿ�̹� �̽� ����)
            StartCoroutine(Co_RepaintFruitUIEndOfFrame());
        }
    }

    private IEnumerator Co_RepaintFruitUIEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        RebuildFruitBadgesForResult();
        if (useFruitTotalText) UpdateFruitProgressText();  // ���ϸ� �հ踸 ǥ��
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
