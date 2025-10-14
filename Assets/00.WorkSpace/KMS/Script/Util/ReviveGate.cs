using UnityEngine;

public static class ReviveGate
{
    static float _until = -999f;

    /// <summary>���ݺ��� seconds ���� ����Ʈ Ȱ��</summary>
    public static void Arm(float seconds = 1.0f)
    {
        _until = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
        Debug.Log($"[ReviveGate] ARM for {seconds:0.###}s");
    }

    /// <summary>����Ʈ ��� ����</summary>
    public static void Disarm()
    {
        _until = -999f; // ���ŷ� ������ �ٷ� ��Ȱ��
        Debug.Log("[ReviveGate] DISARM");
    }

    /// <summary>����Ʈ�� ���� Ȱ������</summary>
    public static bool IsArmed => Time.realtimeSinceStartup < _until;
}