using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PhaseBuffer<T>
{
    private System.Collections.Generic.List<(int due, T item)> _produce = new(128);
    private System.Collections.Generic.List<(int due, T item)> _consume = new(128);
    private int _tick;

    // ������ ���� �� ȣ��
    public void TickBegin() => _tick++;

    // ���� ������ ������
    public void Enqueue(in T item, int delayFrames = 0) => _produce.Add((_tick + delayFrames, item));

    // �����Ӵ� budget���� �Һ�. ���� �� �� �� �ڵ� �̿�
    public void Consume(int budget, System.Action<T> run)
    {
        // swap
        var tmp = _consume; _consume = _produce; _produce = tmp;
        _produce.Clear();

        int used = 0;
        for (int i = 0; i < _consume.Count; i++)
        {
            var (due, it) = _consume[i];
            if (due <= _tick && used < budget)
            {
                run(it);
                used++;
            }
            else
            {
                _produce.Add((due, it)); // ���� ���������� �̿�
            }
        }
        _consume.Clear();
    }

    public void Clear() { _produce.Clear(); _consume.Clear(); }
}