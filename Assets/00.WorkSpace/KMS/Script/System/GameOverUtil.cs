using System.Collections;
using UnityEngine;

public static class GameOverUtil
{
    static bool _pending;
    static int _pScore;
    static bool _pBest;
    static string _pReason;

    public static void PublishGameOverOnce(int score, bool isNewBest, string reason)
    {
        if (AdStateProbe.IsAdShowing || AdStateProbe.IsRevivePending)
        {
            _pScore = score; _pBest = isNewBest; _pReason = reason;
            if (!_pending)
            {
                _pending = true;
                MonoRunner.Run(Co_RetryAfterSafeWindow());
            }
            return;
        }

        if (!GameOverGate.TryPublishOnce(ref GameOverGate.Token, reason)) return;

        Debug.Log($"[GameOver] PublishOnce: {score}, best={isNewBest}, reason={reason}");
        Game.Bus?.PublishImmediate(new GameOverConfirmed(score, isNewBest, reason));
    }

    static IEnumerator Co_RetryAfterSafeWindow()
    {
        yield return null;

        float until = Time.realtimeSinceStartup + 2f;
        while (AdStateProbe.IsAdShowing || AdStateProbe.IsRevivePending)
        {
            if (Time.realtimeSinceStartup > until) break;
            yield return null;
        }

        _pending = false;

        if (!(AdStateProbe.IsAdShowing || AdStateProbe.IsRevivePending))
        {
            PublishGameOverOnce(_pScore, _pBest, _pReason + " (retry)");
        }
    }
}
