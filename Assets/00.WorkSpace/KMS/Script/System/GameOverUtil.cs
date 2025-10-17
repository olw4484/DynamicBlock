using UnityEngine;
using System.Collections;

public static class GameOverUtil
{
    static bool _pending;
    static int _pScore; static bool _pBest; static string _pReason;
    static uint _nonce;

    static float _suppressUntil;
    public static void SuppressFor(float sec, string why = null)
    {
        _suppressUntil = Mathf.Max(_suppressUntil, Time.realtimeSinceStartup + Mathf.Max(0f, sec));
        _pending = false;
        _nonce++;
        Debug.Log($"[GameOver] Suppressed for {sec:0.##}s ({why ?? "-"})");
    }
    public static bool IsSuppressedNow => Time.realtimeSinceStartup < _suppressUntil;
    public static void ClearSuppression() => _suppressUntil = 0f;

    static bool IsBlocked()
    {
        return
            AdStateProbe.IsFullscreenShowing ||
            AdStateProbe.IsRevivePending ||
            ReviveGate.IsArmed ||
            UIStateProbe.IsResultOpen ||
            UIStateProbe.IsReviveOpen ||
            UIStateProbe.ResultGuardActive ||
            UIStateProbe.IsAnyModalOpen ||
            UIStateProbe.ReviveGraceActive;
    }

    static string GetActiveBlockers()
    {
        var sb = new System.Text.StringBuilder();
        void Add(string n, bool v) { if (v) { if (sb.Length > 0) sb.Append(", "); sb.Append(n); } }
        Add("Fullscreen", AdStateProbe.IsFullscreenShowing);
        Add("RevivePending", AdStateProbe.IsRevivePending);
        Add("ReviveGate", ReviveGate.IsArmed);
        Add("ResultOpen", UIStateProbe.IsResultOpen);
        Add("ReviveOpen", UIStateProbe.IsReviveOpen);
        Add("Modal", UIStateProbe.IsAnyModalOpen);
        Add("ReviveGrace", UIStateProbe.ReviveGraceActive);
        Add("ResultGuard", UIStateProbe.ResultGuardActive);
        Add("Suppressed", IsSuppressedNow);
        return sb.Length == 0 ? "-" : sb.ToString();
    }

    static IEnumerator Co_RetryAfterSafeWindow(uint my)
    {
        yield return null;

        float nextLog = Time.realtimeSinceStartup + 1f;
        while (IsBlocked() || IsSuppressedNow)
        {
            if (Time.realtimeSinceStartup >= nextLog)
            {
                Debug.Log("[GameOver] still blocked/suppressed by: " + GetActiveBlockers());
                nextLog += 1f;
            }
            yield return null;
        }

        _pending = false;
        if (my != _nonce) { Debug.Log("[GameOver] pending canceled"); yield break; }
        if (IsSuppressedNow) { Debug.Log("[GameOver] retry suppressed"); yield break; }

        TryPublish(_pScore, _pBest, _pReason + " (retry)");
    }

    public static void PublishGameOverOnce(int score, bool isNewBest, string reason)
    {
        if (IsSuppressedNow)
        {
            Debug.Log($"[GameOver] Suppressed ¡æ drop publish. reason={reason}");
            return;
        }

        if (IsBlocked())
        {
            _pScore = score; _pBest = isNewBest; _pReason = reason;
            if (!_pending)
            {
                _pending = true;
                uint my = _nonce;
                MonoRunner.Run(Co_RetryAfterSafeWindow(my));
            }
            return;
        }

        TryPublish(score, isNewBest, reason);
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

    public static void CancelPending(string why = null)
    {
        _pending = false;
        _nonce++;
        Debug.Log($"[GameOver] CancelPending ({why ?? "-"})");
    }

    public static void ResetAll(string why = null)
    {
        _pending = false;
        _nonce++;
        Debug.Log($"[GameOver] ResetAll ({why ?? "-"})");
    }
}
