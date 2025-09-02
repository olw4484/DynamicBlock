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
        // ������ ���� ����ȭ
        soundLane.TickBegin();
        effectLane.TickBegin();
    }

    void LateUpdate()
    {
        // ������ ���� ����: ����Ʈ �� ����
        effectLane.Consume();
        soundLane.Consume();
    }

    public void ClearAll()
    {
        soundLane.ClearAll();
        effectLane.ClearAll();
    }
}
