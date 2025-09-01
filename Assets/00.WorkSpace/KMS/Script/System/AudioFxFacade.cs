using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioFxFacade : MonoBehaviour
{
    [SerializeField] private SoundLane soundLane;
    [SerializeField] private EffectLane effectLane;

    public void EnqueueSound(int id, int delay = 0) =>
        soundLane.Enqueue(new SoundEvent(id, delay));

    public void EnqueueEffect(int id, Vector3 pos, int delay = 0) =>
        effectLane.Enqueue(new EffectEvent(id, pos, delay));

    void Update()
    {
        // 프레임 시작 동기화
        soundLane.TickBegin();
        effectLane.TickBegin();
    }

    void LateUpdate()
    {
        // 페이즈 순서 보장: 이펙트 → 사운드
        effectLane.Consume();
        soundLane.Consume();
    }

    public void ClearAll()
    {
        soundLane.ClearAll();
        effectLane.ClearAll();
    }
}
