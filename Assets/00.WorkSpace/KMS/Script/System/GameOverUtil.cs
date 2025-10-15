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
        bool blocking = AdStateProbe.IsFullscreenShowing || AdStateProbe.IsRevivePending;

        if (blocking)
        {
            _pScore = score; _pBest = isNewBest; _pReason = reason;
            if (!_pending)
            {
                _pending = true;
                MonoRunner.Run(Co_RetryAfterSafeWindow());
            }
            return;
        }

        TryPublish(score, isNewBest, reason);
    }

    static IEnumerator Co_RetryAfterSafeWindow()
    {
        // 최대 2초 기다리되, 배너는 무시. 전면/부활 대기만 본다.
        float until = Time.realtimeSinceStartup + 2f;
        while (AdStateProbe.IsFullscreenShowing || AdStateProbe.IsRevivePending)
        {
            if (Time.realtimeSinceStartup > until) break;
            yield return null;
        }

        _pending = false;

        TryPublish(_pScore, _pBest, _pReason + " (retry)");
    }

    static void TryPublish(int score, bool isNewBest, string reason)
    {
        if (!GameOverGate.TryPublishOnce(ref GameOverGate.Token, reason))
        {
            Debug.Log($"[GameOver] Blocked by gate. reason={reason}");
            return;
        }

        Debug.Log($"[GameOver] PublishOnce: {score}, best={isNewBest}, reason={reason}");
        Game.Bus?.PublishImmediate(new GameOverConfirmed(score, isNewBest, reason));
    }
}

