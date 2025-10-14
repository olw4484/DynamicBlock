using _00.WorkSpace.GIL.Scripts.Messages;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

public sealed class GameFlowBridge : MonoBehaviour
{
    private bool _emittedThisRun;

    void OnEnable()
    {
        Game.Bus?.Subscribe<GiveUpRequest>(OnGiveUp, replaySticky: false);
        Game.Bus?.Subscribe<GameEntered>(_ => _emittedThisRun = false, replaySticky: true);
        Game.Bus?.Subscribe<GameResetRequest>(_ => _emittedThisRun = false, replaySticky: false);
    }

    void OnDisable()
    {
        Game.Bus?.Unsubscribe<GiveUpRequest>(OnGiveUp);
        Game.Bus?.Unsubscribe<GameEntered>(_ => _emittedThisRun = false);
        Game.Bus?.Unsubscribe<GameResetRequest>(_ => _emittedThisRun = false);
    }

    void OnGiveUp(GiveUpRequest _)
    {
        int score = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
        int prevHi = Game.Save?.Data?.highScore ?? 0;
        bool tieIsNew = false;
        bool isNewBest = tieIsNew ? (score >= prevHi) : (score > prevHi);

        GameOverUtil.PublishGameOverOnce(score, isNewBest, "AdventureGiveUp");
    }
}

