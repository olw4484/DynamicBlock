using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

public static class AdsInitGate
{
    static bool _initCalled;
    static bool _ready;
    static readonly List<Action> _pending = new();

    public static bool Ready => _ready;

    /// �ѹ��� ȣ��Ǹ� ��
    public static void EnsureInit()
    {
        if (_initCalled) return;
        _initCalled = true;

        // AndroidManifest�� APPLICATION_ID�� �־�� ��
        MobileAds.Initialize(status =>
        {
            _ready = true;
            // ����� �۾� �÷���
            foreach (var a in _pending) SafeInvoke(a);
            _pending.Clear();
            Debug.Log("[AdsInitGate] MobileAds initialized.");
        });
    }

    public static void WhenReady(Action action)
    {
        if (_ready) SafeInvoke(action);
        else _pending.Add(action);
    }

    static void SafeInvoke(Action a)
    {
        try { a?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
