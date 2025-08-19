using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using System;

// ================================
// Project : DynamicBlock
// Script  : EventQueue.cs
// Desc    : 이벤트 버스/큐 (즉시/지연/스티키/타이머/스레드세이프)
// ================================

public sealed class EventQueue : IManager, ITickable, ITeardown
{
    public int Order => 0;

    // 타입별 핸들러
    private readonly Dictionary<Type, List<Delegate>> _handlers = new(64);
    // 프레임 처리용 큐 (메인스레드)
    private readonly Queue<object> _queue = new(128);
    // 워커 스레드 입력 큐
    private readonly ConcurrentQueue<object> _threadQueue = new();
    // 스티키(마지막 상태 캐시)
    private readonly Dictionary<Type, object> _sticky = new(32);

    // 지연 발행(타이머) 관리
    private struct Scheduled
    {
        public float due;   // 만기 시간(Time.time + delay)
        public object evt;
    }
    private readonly List<Scheduled> _scheduled = new(64);

    public void PreInit()
    {
        _handlers.Clear();
        _queue.Clear();
        while (_threadQueue.TryDequeue(out _)) { }
        _sticky.Clear();
        _scheduled.Clear();
    }

    public void Init() { /* 필요 시 예약 초기화 */ }
    public void PostInit() { /* 소비자가 구독함 */ }

    public void Tick(float dt)
    {
        // 1) 워커 스레드 입력 병합
        while (_threadQueue.TryDequeue(out var fromWorker))
            _queue.Enqueue(fromWorker);

        // 2) 타이머 만기 이벤트 이동
        float now = Time.time;
        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (_scheduled[i].due <= now)
            {
                _queue.Enqueue(_scheduled[i].evt);
                _scheduled.RemoveAt(i);
            }
        }

        // 3) 일반 큐 처리
        int count = _queue.Count;
        for (int i = 0; i < count; i++)
            Dispatch(_queue.Dequeue());
    }

    public void Teardown()
    {
        _handlers.Clear();
        _queue.Clear();
        while (_threadQueue.TryDequeue(out _)) { }
        _sticky.Clear();
        _scheduled.Clear();
    }

    // ===============================
    // # API - 발행
    // ===============================
    public void Publish<T>(T evt) => _queue.Enqueue(evt);
    public void PublishImmediate<T>(T evt) => Dispatch(evt);

    public void PublishSticky<T>(T evt, bool alsoEnqueue = true)
    {
        _sticky[typeof(T)] = evt!;
        if (alsoEnqueue) _queue.Enqueue(evt!);
    }

    public void PublishAfter<T>(T evt, float delaySec)
    {
        _scheduled.Add(new Scheduled { due = Time.time + Mathf.Max(0f, delaySec), evt = evt! });
    }

    // 워커 스레드에서 안전
    public void EnqueueFromAnyThread<T>(T evt) => _threadQueue.Enqueue(evt!);

    // ===============================
    // # API - 구독
    // ===============================
    public void Subscribe<T>(Action<T> handler, bool replaySticky = true)
    {
        var t = typeof(T);
        if (!_handlers.TryGetValue(t, out var list))
            _handlers[t] = list = new List<Delegate>(8);

        list.Add(handler);

        // Sticky 즉시 재생
        if (replaySticky && _sticky.TryGetValue(t, out var last))
            handler((T)last);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var t = typeof(T);
        if (_handlers.TryGetValue(t, out var list))
            list.Remove(handler);
    }

    // ===============================
    // # 내부
    // ===============================
    private void Dispatch(object evt)
    {
        var t = evt.GetType();
        if (!_handlers.TryGetValue(t, out var list)) return;

        // 단순 루프 (필요 시 복사본 사용)
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is Action<object> obj) obj(evt);
            else list[i].DynamicInvoke(evt);
        }
    }

    // Sticky 초기화
    public void ClearSticky<T>() => _sticky.Remove(typeof(T));
    public void ClearAllSticky() => _sticky.Clear();
}

// 샘플 이벤트
public readonly struct ScoreChanged { public readonly int value; public ScoreChanged(int v) => value = v; }
public readonly struct ComboChanged { public readonly int value; public ComboChanged(int v) => value = v; }