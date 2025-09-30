using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using System.Collections;
using UnityEngine;

public sealed class GameOverResultRouter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject classicGameOverRoot;        // Classic_Game_Over ��Ʈ
    [SerializeField] private GameObject classicNewBestRoot;
    [SerializeField] private AdventureResultPresenter adventurePresenter; // ADResult_Canvas�� ���� ������

    [Header("Options")]
    [SerializeField] private bool hideAllOnEnable = true;

    private EventQueue _bus;
    private bool _shownOnce;

    private void OnEnable()
    {
        if (hideAllOnEnable)
        {
            if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
            adventurePresenter?.HideAllPublic();
        }

        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;
            // ���� Ȯ�� ��ȣ
            _bus.Subscribe<GameOverConfirmed>(OnGameOverConfirmed, replaySticky: true);

            _bus.Subscribe<AdventureStageCleared>(OnAdventureCleared, replaySticky: true);
            _bus.Subscribe<AdventureStageFailed>(OnAdventureFailed, replaySticky: true);
        }));
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GameOverConfirmed>(OnGameOverConfirmed);
        _bus.Unsubscribe<AdventureStageCleared>(OnAdventureCleared);
        _bus.Unsubscribe<AdventureStageFailed>(OnAdventureFailed);
    }

    // ����: ��庥ó ���� �̺�Ʈ�� �̹� ����� �� �ٷ� ó��
    private void OnAdventureCleared(AdventureStageCleared e)
    {
        if (_shownOnce) return;
        if ((MapManager.Instance?.CurrentMode ?? GameMode.Classic) != GameMode.Adventure) return;

        _shownOnce = true;
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
        adventurePresenter?.ShowClearPublic(e.kind, e.finalScore);
    }

    private void OnAdventureFailed(AdventureStageFailed e)
    {
        if (_shownOnce) return;
        if ((MapManager.Instance?.CurrentMode ?? GameMode.Classic) != GameMode.Adventure) return;

        _shownOnce = true;
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);
        adventurePresenter?.ShowFailPublic(e.kind, e.finalScore);
    }

    // ����: ��Ȱ/������ ������ Ȯ���� ��(���� �б�)
    private void OnGameOverConfirmed(GameOverConfirmed e)
    {
        var mm = MapManager.Instance;
        var sm = Game.Save;
        var mode = mm?.CurrentMode ?? GameMode.Classic;
        Debug.Log($"[ResultRouter] GOC: mode={mode}, score={e.score}, reason={e.reason}, newBest={e.isNewBest}");

        if (mode == GameMode.Adventure)
        {
            var md = mm?.CurrentMapData;
            var kind = md?.goalKind ?? MapGoalKind.Score;

            bool cleared = ComputeAdventureCleared(kind, md);

            // Sticky �� ���� (���ĵ� ���)
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
            // ����
            if (cleared) Sfx.StageClear();
            else Sfx.Stagefail();

            sm?.ClearRunState(true);
            return;
        }

        // ===== Classic =====
        sm?.UpdateClassicScore(e.score);
        sm?.ClearRunState(true);
        // 1) UI �б�
        if (classicNewBestRoot != null)
        {
            classicNewBestRoot.SetActive(e.isNewBest);
            if (classicGameOverRoot) classicGameOverRoot.SetActive(!e.isNewBest);
        }
        else
        {
            // �ű�� ��Ʈ�� ���ٸ� ���� ���ӿ��� ��Ʈ�� ON (������Ʈ ��Ȳ�� �°� �������� ȣ��� �ٲ㵵 OK)
            if (classicGameOverRoot) classicGameOverRoot.SetActive(true);
        }

        // 2) ���嵵 �б�
        if (e.isNewBest) Sfx.NewRecord();
        else Sfx.GameOver();

        // 3) ����/������ ���� (UI �� �ڿ� �ص� ����)
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
        yield return null; // ���� �����ӿ� ���
        if (classicGameOverRoot) classicGameOverRoot.SetActive(false);

        if (cleared) adventurePresenter?.ShowClearPublic(kind, score);
        else adventurePresenter?.ShowFailPublic(kind, score);
    }
}
