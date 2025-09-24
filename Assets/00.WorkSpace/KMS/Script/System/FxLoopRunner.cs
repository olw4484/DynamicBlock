using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FxLoopRunner : MonoBehaviour
{
    [SerializeField] private EffectLane effectLane;
    [SerializeField] private SoundLane soundLane;

    // ���� �ý��� - ����
    [SerializeField] private bool dontDestroyOnLoad = true;

    // IAudioService�� ���� C# �Ŵ����� ����(Game.Bind)���� �����´�
    private IAudioService audio;

    void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        // ���� �ڵ� Ž��(�Ǽ� ����)
        if (!effectLane) effectLane = FindFirstObjectByType<EffectLane>();
        if (!soundLane) soundLane = FindFirstObjectByType<SoundLane>();

        TryBindAudio();
        TryWireLane();
    }

    void OnEnable()
    {
        // �� ��Ȱ��ȭ ������ ������ �� ������ �� �� ��
        TryBindAudio();
        TryWireLane();
    }

    void Update()
    {
        effectLane?.TickBegin();
        soundLane?.TickBegin();

        // ��Ÿ�� �߿��� �ѹ��� ���ε� ��õ� (���� ����� �幰�Ը�)
        if (audio == null || soundLane == null)
        {
            TryBindAudio();
            TryWireLane();
        }
    }

    void LateUpdate()
    {
        // ����Ʈ �� ���� ���� ����
        effectLane?.Consume();
        soundLane?.Consume();
    }

    private void TryBindAudio()
    {
        if (audio != null) return;

        // 1) Game.Bind�� ���Ե� ���� �켱
        if (Game.IsBound && Game.Audio != null)
        {
            audio = Game.Audio;
            return;
        }

        // 2) ����: ManagerGroup���� ���� Resolve
        var mg = ManagerGroup.Instance;
        if (mg != null)
        {
            var resolved = mg.Resolve<IAudioService>();
            if (resolved != null) audio = resolved;
        }
    }

    private void TryWireLane()
    {
        if (soundLane != null && audio != null)
        {
            soundLane.SetDependencies(audio);
        }
        else
        {
            Debug.LogWarning($"[FxLoopRunner] Bind miss: soundLane? {soundLane != null}, audio? {audio != null}");
        }
    }
}
