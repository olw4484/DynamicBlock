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

    /// 한번만 호출되면 됨
    public static void EnsureInit()
    {
        if (_initCalled) return;
        _initCalled = true;

        // AndroidManifest에 APPLICATION_ID가 있어야 함
        MobileAds.Initialize(status =>
        {
            _ready = true;
            // 대기중 작업 플러시
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
