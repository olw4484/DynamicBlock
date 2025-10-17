using System.Threading;
using UnityEngine;

public static class GameOverGate
{
    // 내부 시퀀스: 발행 시점 추적용(단조 증가)
    static int _seq;
    // "이미 발행됨"을 의미하는 실제 토큰 저장소
    public static int Token;

    // 에디터/개발 빌드에서만 로그
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void D(string msg) => Debug.Log($"[GameOverGate] {msg}");

    public static void Reset(string reason = null)
    {
        Token = 0;
        D($"Reset. reason={reason ?? "-"}");
    }

    static int NextSeq() => Interlocked.Increment(ref _seq);

    /// <summary>
    /// ref로 받은 토큰이 0일 때만 1회 세팅하고 true. 이미 세팅되어 있으면 false.
    /// </summary>
    public static bool TryPublishOnce(ref int token, string reason = null, bool logStack = false)
    {
        if (token != 0)
        {
            D($"BLOCK: already set (token={token}) reason={reason ?? "-"}");
            if (logStack) D(UnityEngine.StackTraceUtility.ExtractStackTrace());
            return false;
        }

        token = NextSeq();
        D($"PASS : set token={token} reason={reason ?? "-"}");
        return true;
    }
}
