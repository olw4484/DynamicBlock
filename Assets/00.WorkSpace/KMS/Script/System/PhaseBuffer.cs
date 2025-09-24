using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PhaseBuffer<T>
{
    private System.Collections.Generic.List<(int due, T item)> _produce = new(128);
    private System.Collections.Generic.List<(int due, T item)> _consume = new(128);
    private int _tick;

    // 프레임 시작 시 호출
    public void TickBegin() => _tick++;

    // 지연 프레임 스케줄
    public void Enqueue(in T item, int delayFrames = 0) => _produce.Add((_tick + delayFrames, item));

    // 프레임당 budget개만 소비. 실행 못 한 건 자동 이월
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
                _produce.Add((due, it)); // 다음 프레임으로 이월
            }
        }
        _consume.Clear();
    }

    public void Clear() { _produce.Clear(); _consume.Clear(); }
}