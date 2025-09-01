using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioFxFacade : MonoBehaviour
{
    [SerializeField] int soundBudget = 5, effectBudget = 5;
    private PhaseBuffer<SoundEvent> _snd = new();
    private PhaseBuffer<EffectEvent> _fx = new();

    public void EnqueueSound(int id, int delay = 0) => _snd.Enqueue(new SoundEvent(id, delay), delay);
    public void EnqueueEffect(int id, Vector3 p, int delay = 0) => _fx.Enqueue(new EffectEvent(id, p, delay), delay);

    void Update() { _snd.TickBegin(); _fx.TickBegin(); }
    void LateUpdate()
    {
        _fx.Consume(effectBudget, PlayEffect);   // 이펙트 먼저
        _snd.Consume(soundBudget, PlaySound);    // 그다음 사운드
    }
    void PlaySound(SoundEvent e) { /* AudioSourcePool.Get→Play→Release */ }
    void PlayEffect(EffectEvent e) { /* EffectPool.Get(id)→Play→Release */ }
}
