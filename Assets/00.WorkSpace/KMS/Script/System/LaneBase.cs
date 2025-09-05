using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class LaneBase<TEvent> : MonoBehaviour
{
    [Header("Budget / Cooldown")]
    [SerializeField] protected int budgetPerFrame = 8;
    [SerializeField] protected int defaultCooldownMs = 60;

    protected readonly Queue<TEvent> _queue = new();
    protected readonly Dictionary<int, float> _nextAllowed = new(); // key → next time (ms)

    public void Enqueue(TEvent e) => _queue.Enqueue(e);

    // 프레임 시작에 호출 (Facade.Update)
    public virtual void TickBegin() { /* 필요시 누적 시간 관리 */ }

    // LateUpdate 등 “소비 페이즈”에서 호출
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
                // 쿨다운 미충족 등으로 스킵 → 다음 이벤트 기회 위해 큐 뒤로 회전
                _queue.Dequeue();
                _queue.Enqueue(e);
                break; // 무한루프 방지
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

    // 실제 실행은 파생 클래스가 결정
    protected abstract bool TryConsume(TEvent e);
}
