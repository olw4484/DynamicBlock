using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : AppLifecycleEmitter.cs
// Desc    : 일시정지/종료 시 자동 저장
// ================================

[DefaultExecutionOrder(-1000)]
public sealed class AppLifecycleEmitter : MonoBehaviour
{
    void OnApplicationPause(bool pause)
    {
        if (pause && Game.IsBound) Game.Bus.Publish(new SaveRequested());
    }
    void OnApplicationQuit()
    {
        if (Game.IsBound) Game.Bus.Publish(new SaveRequested());
    }
}
