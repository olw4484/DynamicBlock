using System.Threading;
using UnityEngine;

public static class GameOverGate
{
    // ���� ������: ���� ���� ������(���� ����)
    static int _seq;
    // "�̹� �����"�� �ǹ��ϴ� ���� ��ū �����
    public static int Token;

    // ������/���� ���忡���� �α�
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
    /// ref�� ���� ��ū�� 0�� ���� 1ȸ �����ϰ� true. �̹� ���õǾ� ������ false.
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
