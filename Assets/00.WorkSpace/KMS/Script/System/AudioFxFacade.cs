using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioFxFacade : MonoBehaviour
{
    [SerializeField] private SoundLane soundLane;

    public void EnqueueSound(int id, int delay = 0) =>
        soundLane.Enqueue(new SoundEvent(id, delay));

    void Update() => soundLane.TickBegin();
    void LateUpdate() => soundLane.Consume();
}
