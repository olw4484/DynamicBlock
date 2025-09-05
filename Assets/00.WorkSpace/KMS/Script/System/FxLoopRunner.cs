using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FxLoopRunner : MonoBehaviour
{
    [SerializeField] private EffectLane effectLane;
    [SerializeField] private SoundLane soundLane;

    // 전역 시스템 - 유지
    [SerializeField] private bool dontDestroyOnLoad = true;

    private void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        // 인스펙터에 비어 있으면 자동 탐색(실수 방지)
        if (!effectLane) effectLane = FindFirstObjectByType<EffectLane>();
        if (!soundLane) soundLane = FindFirstObjectByType<SoundLane>();
    }

    void Update()
    {
        effectLane?.TickBegin();
        soundLane?.TickBegin();
    }

    void LateUpdate()
    {
        // 이펙트 → 사운드 순서 보장
        effectLane?.Consume();
        soundLane?.Consume();
    }
}
