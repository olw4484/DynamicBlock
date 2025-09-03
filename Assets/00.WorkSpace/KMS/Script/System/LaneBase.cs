using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class LaneBase<TEvent> : MonoBehaviour
{
    [Header("Budget / Cooldown")]
    [SerializeField] protected int budgetPerFrame = 8;
    [SerializeField] protected int defaultCooldownMs = 60;

    protected readonly Queue<TEvent> _queue = new();
    protected readonly Dictionary<int, float> _nextAllowed = new(); // key �� next time (ms)

    public void Enqueue(TEvent e) => _queue.Enqueue(e);

    // ������ ���ۿ� ȣ�� (Facade.Update)
    public virtual void TickBegin() { /* �ʿ�� ���� �ð� ���� */ }

    // LateUpdate �� ���Һ� ��������� ȣ��
    public void Consume()
    {
        int consumed = 0;
        while (_queue.Count > 0 && consumed < budgetPerFrame)
        {
            var e = _queue.Peek();
            if (TryConsume(e))
            {
                _queue.Dequeue();
                consumed++;
            }
            else
            {
                // ��ٿ� ������ ������ ��ŵ �� ���� �̺�Ʈ ��ȸ ���� ť �ڷ� ȸ��
                _queue.Dequeue();
                _queue.Enqueue(e);
                break; // ���ѷ��� ����
            }
        }
    }

    protected bool IsCooledDown(int key)
    {
        float now = Time.realtimeSinceStartup * 1000f;
        if (_nextAllowed.TryGetValue(key, out var next) && now < next) return false;
        _nextAllowed[key] = now + defaultCooldownMs;
        return true;
    }

    // ���� ������ �Ļ� Ŭ������ ����
    protected abstract bool TryConsume(TEvent e);
}
