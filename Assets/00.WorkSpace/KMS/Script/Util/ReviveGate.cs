using UnityEngine;

public static class ReviveGate
{
    static float _until = -999f;

    /// <summary>지금부터 seconds 동안 게이트 활성</summary>
    public static void Arm(float seconds = 1.0f)
    {
        _until = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
        Debug.Log($"[ReviveGate] ARM for {seconds:0.###}s");
    }

    /// <summary>게이트 즉시 해제</summary>
    public static void Disarm()
    {
        _until = -999f; // 과거로 내려서 바로 비활성
        Debug.Log("[ReviveGate] DISARM");
    }

    /// <summary>게이트가 현재 활성인지</summary>
    public static bool IsArmed => Time.realtimeSinceStartup < _until;
}