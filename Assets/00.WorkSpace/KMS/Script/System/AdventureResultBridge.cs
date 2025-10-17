using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class AdventureResultBridge : MonoBehaviour
{
    private EventQueue _bus;
    private bool _sentThisRun;

    private void OnEnable()
    {
        StartCoroutine(GameBindingUtil.WaitAndRun(() =>
        {
            _bus = Game.Bus;
            _bus.Subscribe<GameOverConfirmed>(OnGameOverConfirmed, replaySticky: false);
            _bus.Subscribe<GameResetDone>(OnGameResetDone, replaySticky: false);
        }));
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GameOverConfirmed>(OnGameOverConfirmed);
        _bus.Unsubscribe<GameResetDone>(OnGameResetDone);
    }

    private void OnGameResetDone(GameResetDone _)
    {
        _sentThisRun = false;
    }

    private void OnGameOverConfirmed(GameOverConfirmed e)
    {
        var mm = MapManager.Instance;
        if (!mm || mm.CurrentMode != GameMode.Adventure) return;
        if (_sentThisRun) return;

        var kind = mm.CurrentMapData ? mm.CurrentMapData.goalKind : MapGoalKind.Score;
        int score = e.score;

        Debug.Log($"[ADBridge] GOC°ÊAdventureStageFailed kind={kind} score={score}");
        _bus.PublishImmediate(new AdventureStageFailed(kind, score));
        _sentThisRun = true;
    }
}
