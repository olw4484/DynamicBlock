using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : EventQueue.cs
// Desc    : �̺�Ʈ ����/ť (���/����/��ƼŰ/Ÿ�̸�/�����弼����)
// ================================

public sealed class EventQueue : IManager, ITickable, ITeardown
{

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    static readonly ProfilerMarker MK_Tick = new("EventQueue.Tick");
    static readonly ProfilerMarker MK_MergeThread = new("EventQueue.MergeThread");
    static readonly ProfilerMarker MK_Timers = new("EventQueue.Timers");
    static readonly ProfilerMarker MK_Dispatch = new("EventQueue.Dispatch");
    static readonly ProfilerMarker MK_Handlers = new("EventQueue.Handlers");
    static readonly ProfilerMarker MK_Taps = new("EventQueue.Taps");
#endif
    public int Order => 0;

    // Ÿ�Ժ� �ڵ鷯
    private readonly Dictionary<Type, List<Delegate>> _handlers = new(64);
    // ������ ó���� ť (���ν�����)
    private readonly Queue<object> _queue = new(128);
    // ��Ŀ ������ �Է� ť
    private readonly ConcurrentQueue<object> _threadQueue = new();
    // ��ƼŰ(������ ���� ĳ��)
    private readonly Dictionary<Type, object> _sticky = new(32);

    // ���� ����(Ÿ�̸�) ����
    private struct Scheduled
    {
        public float due;   // ���� �ð�(Time.time + delay)
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

    public void Init() { /* �ʿ� �� ���� �ʱ�ȭ */ }
    public void PostInit() { /* �Һ��ڰ� ������ */ }

    public void Tick(float dt)
    {
        UnityEngine.Debug.Log($"[EQ] Tick, q={_queue.Count}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (MK_Tick.Auto())
#endif
        {
            // 1) ��Ŀ ����
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (MK_MergeThread.Auto())
#endif
                while (_threadQueue.TryDequeue(out var fromWorker))
                    _queue.Enqueue(fromWorker);

            // 2) Ÿ�̸� ó��
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (MK_Timers.Auto())
#endif
            {
                float now = Time.time;
                for (int i = _scheduled.Count - 1; i >= 0; i--)
                    if (_scheduled[i].due <= now) { _queue.Enqueue(_scheduled[i].evt); _scheduled.RemoveAt(i); }
            }

            // 3) ť ����ġ
            int count = _queue.Count;
            for (int i = 0; i < count; i++)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using (MK_Dispatch.Auto())
#endif
                    Dispatch(_queue.Dequeue());
            }
        }
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
    // # API - ����
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private readonly List<System.Action<object>> _taps = new();

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public void AddTap(System.Action<object> tap)
    {
        if (tap != null) _taps.Add(tap);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public void RemoveTap(System.Action<object> tap)
    {
        _taps.Remove(tap);
    }
#endif

    // ��Ŀ �����忡�� ����
    public void EnqueueFromAnyThread<T>(T evt) => _threadQueue.Enqueue(evt!);

    // ===============================
    // # API - ����
    // ===============================
    public void Subscribe<T>(Action<T> handler, bool replaySticky = true)
    {
        var t = typeof(T);
        if (!_handlers.TryGetValue(t, out var list))
            _handlers[t] = list = new List<Delegate>(8);

        list.Add(handler);

        // Sticky ��� ���
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
    // # ����
    // ===============================
    private void Dispatch(object evt)
    {
        if (evt is GameResetRequest) UnityEngine.Debug.Log("[EQ] Dispatch(GameResetRequest)");
        var t = evt.GetType();
        if (_handlers.TryGetValue(t, out var list))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (MK_Handlers.Auto())
#endif
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Action<object> obj) obj(evt);
                    else list[i].DynamicInvoke(evt);
                }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (MK_Taps.Auto())
            for (int i = 0; i < _taps.Count; i++)
            {
                try { _taps[i]?.Invoke(evt); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
#endif
    }

    // Sticky �ʱ�ȭ
    public void ClearSticky<T>() => _sticky.Remove(typeof(T));
    public void ClearAllSticky() => _sticky.Clear();
}

// ���� �̺�Ʈ
public readonly struct ScoreChanged { public readonly int value; public ScoreChanged(int v) => value = v; }
public readonly struct ComboChanged { public readonly int value; public ComboChanged(int v) => value = v; }
public readonly struct GameOver
{
    public readonly int score; public readonly string reason;
    public GameOver(int score, string reason = null) { this.score = score; this.reason = reason; }
}
public readonly struct RewardedContinueRequest { }      // ���(Non-Sticky)
public readonly struct AdPlaying { }                    // ���� ����(�Է� ���)
public readonly struct AdFinished { }                   // ���� ����(�Է� ����)
public readonly struct ContinueGranted { }              // ��� ���
public readonly struct SaveRequested { }
public readonly struct LoadRequested { }
public readonly struct ResetRequested { }
public readonly struct GameDataChanged
{
    public readonly GameData data;
    public GameDataChanged(GameData d) { data = d; }
}
public readonly struct LinesCleared
{
    public readonly int rows, cols, total;
    public LinesCleared(int rows, int cols) { this.rows = rows; this.cols = cols; this.total = rows + cols; }
}
public readonly struct GridReady { public readonly int rows, cols; public GridReady(int r, int c) { rows = r; cols = c; } }
public readonly struct GameResetRequest { }   // ��ư �� ��û
public readonly struct GameResetting { }      // ���� ����(�Է����/��޴ݱ� ��)
public readonly struct GameResetDone { }      // ���� �Ϸ�(�Է�����/�г� ���� ��)