using UnityEngine;

public static class UIStateProbe
{
    public static volatile bool IsResultOpen;
    public static volatile bool IsReviveOpen;
    public static volatile bool IsAnyModalOpen;

    public static volatile bool ReviveGraceActive;

    public static bool ResultGuardActive => Time.realtimeSinceStartup < _resultGuardUntil;
    static float _resultGuardUntil;

    public static void ArmResultGuard(float seconds)
    {
        _resultGuardUntil = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
    }
    public static void DisarmResultGuard()
    {
        _resultGuardUntil = -1f;
    }

    public static void ArmOrExtendResultGuard(float seconds)
    {
        var until = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
        if (until > _resultGuardUntil) _resultGuardUntil = until;
    }

    public static void ArmReviveGrace(float sec)
    {
        MonoRunner.Run(CoArmGrace(sec));
    }
    static System.Collections.IEnumerator CoArmGrace(float sec)
    {
        ReviveGraceActive = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, sec));
        ReviveGraceActive = false;
    }
    public static void ResetAllShields()
    {
        IsResultOpen = false;
        IsReviveOpen = false;
        IsAnyModalOpen = false;
        ReviveGraceActive = false;
        DisarmResultGuard();
    }
    public static void DisarmReviveGrace()
    {
        ReviveGraceActive = false;
    }
}

