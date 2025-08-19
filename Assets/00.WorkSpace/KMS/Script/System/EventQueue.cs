using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using System;

// ================================
// Project : DynamicBlock
// Script  : EventQueue.cs
// Desc    : �̺�Ʈ ����/ť (���/����/��ƼŰ/Ÿ�̸�/�����弼����)
// ================================

public sealed class EventQueue : IManager, ITickable, ITeardown
{
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
        // 1) ��Ŀ ������ �Է� ����
        while (_threadQueue.TryDequeue(out var fromWorker))
            _queue.Enqueue(fromWorker);

        // 2) Ÿ�̸� ���� �̺�Ʈ �̵�
        float now = Time.time;
        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (_scheduled[i].due <= now)
            {
                _queue.Enqueue(_scheduled[i].evt);
                _scheduled.RemoveAt(i);
            }
        }

        // 3) �Ϲ� ť ó��
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
        var t = evt.GetType();
        if (!_handlers.TryGetValue(t, out var list)) return;

        // �ܼ� ���� (�ʿ� �� ���纻 ���)
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is Action<object> obj) obj(evt);
            else list[i].DynamicInvoke(evt);
        }
    }

    // Sticky �ʱ�ȭ
    public void ClearSticky<T>() => _sticky.Remove(typeof(T));
    public void ClearAllSticky() => _sticky.Clear();
}

// ���� �̺�Ʈ
public readonly struct ScoreChanged { public readonly int value; public ScoreChanged(int v) => value = v; }
public readonly struct ComboChanged { public readonly int value; public ComboChanged(int v) => value = v; }