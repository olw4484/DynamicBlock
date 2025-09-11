using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FxLoopRunner : MonoBehaviour
{
    [SerializeField] private EffectLane effectLane;
    [SerializeField] private SoundLane soundLane;

    // 전역 시스템 - 유지
    [SerializeField] private bool dontDestroyOnLoad = true;

    // IAudioService는 순수 C# 매니저로 전역(Game.Bind)에서 가져온다
    private IAudioService audio;

    void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        // 레인 자동 탐색(실수 방지)
        if (!effectLane) effectLane = FindFirstObjectByType<EffectLane>();
        if (!soundLane) soundLane = FindFirstObjectByType<SoundLane>();

        TryBindAudio();
        TryWireLane();
    }

    void OnEnable()
    {
        // 씬 재활성화 등으로 끊겼을 수 있으니 한 번 더
        TryBindAudio();
        TryWireLane();
    }

    void Update()
    {
        effectLane?.TickBegin();
        soundLane?.TickBegin();

        // 런타임 중에도 한번은 바인딩 재시도 (성능 고려해 드물게만)
        if (audio == null || soundLane == null)
        {
            TryBindAudio();
            TryWireLane();
        }
    }

    void LateUpdate()
    {
        // 이펙트 → 사운드 순서 보장
        effectLane?.Consume();
        soundLane?.Consume();
    }

    private void TryBindAudio()
    {
        if (audio != null) return;

        // 1) Game.Bind로 주입된 전역 우선
        if (Game.IsBound && Game.Audio != null)
        {
            audio = Game.Audio;
            return;
        }

        // 2) 예비: ManagerGroup에서 직접 Resolve
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
