using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FxLoopRunner : MonoBehaviour
{
    [SerializeField] private EffectLane effectLane;
    [SerializeField] private SoundLane soundLane;

    // ���� �ý��� - ����
    [SerializeField] private bool dontDestroyOnLoad = true;

    private void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        // �ν����Ϳ� ��� ������ �ڵ� Ž��(�Ǽ� ����)
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
        // ����Ʈ �� ���� ���� ����
        effectLane?.Consume();
        soundLane?.Consume();
    }
}
