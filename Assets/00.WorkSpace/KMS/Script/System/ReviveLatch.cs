using System;
using UnityEngine;

public static class ReviveLatch
{
    static bool _active;
    static float _until;

    public static bool Active => _active && Time.realtimeSinceStartup < _until;

    public static void Arm(float sec = 20f, string reason = null)
    {
        _active = true;
        _until = Time.realtimeSinceStartup + Mathf.Max(5f, sec);
        Debug.Log($"[ReviveLatch] ARM {sec:0.0}s ({reason ?? "-"})");
    }

    public static void Disarm(string reason = null)
    {
        _active = false;
        _until = 0f;
        Debug.Log($"[ReviveLatch] DISARM ({reason ?? "-"})");
    }
}