using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// ================================
// Project : DynamicBlock
// Script  : SoundManager.cs
// Desc    : BGM 1채널 + SFX 다중(보이스 제한 3) 사운드 관리자
// Note    : [DisallowMultipleComponent] + DontDestroyOnLoad
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/SoundManager")]
public partial class SoundManager : MonoBehaviour, IManager
{
    // =====================================
    // # Fields (Serialized / Private)
    // =====================================
    [Header("BGM")]
    [SerializeField] private AudioMixerGroup _bgmMixer; // 선택(없어도 OK)
    [SerializeField] private float _bgmDefaultFade = 0.5f;

    [Header("SFX")]
    [SerializeField] private AudioMixerGroup _sfxMixer; // 선택
    [SerializeField, Range(1, 16)] private int _sfxVoices = 3; // 동시 재생 제한
    [SerializeField] private bool _preventSpam = true;
    [SerializeField, Range(0, 500)] private int _defaultCooldownMs = 60; // 동일클립 쿨다운(옵션)

    [Header("Clip")]
    [SerializeField] private AudioClip _sfxPlace;   // 조각 배치
    [SerializeField] private AudioClip _sfxClear;   // 라인 클리어(이미 사용 중이면 유지)
    [SerializeField] private AudioClip _sfxGameOver;

    private AudioSource _bgmSource;
    private readonly List<AudioSource> _sfxSources = new();  // 보이스 풀
    private readonly Dictionary<AudioClip, float> _lastPlayTime = new(); // 스팸방지

    // 각 소스의 메타(우선순위/시작시각)를 저장
    private class VoiceMeta { public int priority; public float startTime; public AudioClip clip; }
    private readonly Dictionary<AudioSource, VoiceMeta> _voiceMeta = new();

    // =====================================
    // # Lifecycle (IManager)
    // =====================================
    public void PreInit() { /* 외부 참조/리소스 연결 */ }

    public void Init()
    {
        DontDestroyOnLoad(gameObject);

        // BGM Source
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.outputAudioMixerGroup = _bgmMixer;

        // SFX Voices
        for (int i = 0; i < _sfxVoices; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // 2D. 필요시 3D로 변경
            src.outputAudioMixerGroup = _sfxMixer;
            _sfxSources.Add(src);
            _voiceMeta[src] = new VoiceMeta { priority = int.MinValue, startTime = -999f, clip = null };
        }
    }

    public void PostInit()
    {
        // 이벤트 연결 예시
        EventBus.OnBoardCleared += () => {/* 예: 클리어 전용 SFX는 외부에서 호출 */};
        EventBus.OnPiecePlaced += HandlePiecePlaced;
        EventBus.OnBoardCleared += HandleBoardCleared;
        EventBus.OnGameOver += HandleGameOver;
    }

    private void OnDestroy()
    {
        EventBus.OnBoardCleared -= () => { };
        EventBus.OnGameOver -= () => { };
        EventBus.OnPiecePlaced -= HandlePiecePlaced;
        EventBus.OnBoardCleared -= HandleBoardCleared;
        EventBus.OnGameOver -= HandleGameOver;
    }

    // =====================================
    // # Event Handlers
    // =====================================
    private void HandlePiecePlaced()
    {
        // 우선순위 예: 배치음은 낮~중 (0~1)
        PlaySFX(_sfxPlace, priority: 1);
    }

    private void HandleBoardCleared()
    {
        // 클리어는 조금 더 강조(우선순위↑)
        PlaySFX(_sfxClear, priority: 2);
    }

    private void HandleGameOver()
    {
        PlaySFX(_sfxGameOver, priority: 3);
        StopBGM(); // 페이드아웃 설정 있으면 인자 전달
    }

    // =====================================
    // # BGM Control
    // =====================================
    public void PlayBGM(AudioClip clip, float fadeSec = -1f)
    {
        if (clip == null) return;
        if (fadeSec < 0f) fadeSec = _bgmDefaultFade;

        if (_bgmSource.isPlaying)
        {
            if (_bgmSource.clip == clip) return;
            StartCoroutine(Co_SwapBGM(clip, fadeSec));
        }
        else
        {
            _bgmSource.clip = clip;
            StartCoroutine(Co_Fade(_bgmSource, 0f, 1f, fadeSec, playOnStart: true));
        }
    }

    public void StopBGM(float fadeSec = -1f)
    {
        if (fadeSec < 0f) fadeSec = _bgmDefaultFade;
        if (_bgmSource.isPlaying)
            StartCoroutine(Co_Fade(_bgmSource, _bgmSource.volume, 0f, fadeSec, stopOnEnd: true));
    }

    private IEnumerator Co_SwapBGM(AudioClip next, float fadeSec)
    {
        yield return Co_Fade(_bgmSource, _bgmSource.volume, 0f, fadeSec, stopOnEnd: true);
        _bgmSource.clip = next;
        yield return Co_Fade(_bgmSource, 0f, 1f, fadeSec, playOnStart: true);
    }

    private IEnumerator Co_Fade(AudioSource src, float from, float to, float dur, bool playOnStart = false, bool stopOnEnd = false)
    {
        if (playOnStart) src.Play();
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = dur <= 0f ? 1f : Mathf.Clamp01(t / dur);
            src.volume = Mathf.Lerp(from, to, k);
            yield return null;
        }
        src.volume = to;
        if (stopOnEnd) src.Stop();
    }

    // =====================================
    // # SFX Control (보이스 제한 + 우선순위 + 스팸 방지)
    // =====================================
    /// <summary>
    /// SFX 재생 (우선순위가 낮으면 교체 대상이 됨)
    /// </summary>
    public void PlaySFX(AudioClip clip, int priority = 0, int cooldownMs = -1)
    {
        if (clip == null) return;
        if (cooldownMs < 0) cooldownMs = _defaultCooldownMs;

        // 스팸 방지(옵션)
        if (_preventSpam && cooldownMs > 0)
        {
            float now = Time.unscaledTime * 1000f;
            if (_lastPlayTime.TryGetValue(clip, out var last) && now - last < cooldownMs) return;
            _lastPlayTime[clip] = now;
        }

        // 1) 빈 소스 찾기
        var free = _sfxSources.Find(s => !s.isPlaying);
        if (free != null)
        {
            UseVoice(free, clip, priority);
            return;
        }

        // 2) 교체 대상 선택(우선순위 낮은 것 → 오래된 것)
        AudioSource candidate = null;
        int minPriority = int.MaxValue;
        float oldest = float.MaxValue;

        foreach (var src in _sfxSources)
        {
            var meta = _voiceMeta[src];
            if (meta.priority < minPriority)
            {
                minPriority = meta.priority;
                oldest = meta.startTime;
                candidate = src;
            }
            else if (meta.priority == minPriority && meta.startTime < oldest)
            {
                oldest = meta.startTime;
                candidate = src;
            }
        }

        // 새 우선순위가 더 높을 때만 교체 (같거나 낮으면 무시)
        if (priority > minPriority)
        {
            UseVoice(candidate, clip, priority);
        }
        // else : 버림 (정책에 따라 오래된 것 무조건 교체하도록 바꿀 수도 있음)
    }

    /// <summary>
    /// 내부: 보이스 사용/메타 업데이트
    /// </summary>
    private void UseVoice(AudioSource src, AudioClip clip, int priority)
    {
        src.clip = clip;
        src.Play();
        var meta = _voiceMeta[src];
        meta.priority = priority;
        meta.startTime = Time.unscaledTime; // 시작 시각
        meta.clip = clip;
    }

    // 3D 위치 재생(옵션)
    public void PlaySFXAt(AudioClip clip, Vector3 worldPos, int priority = 0, int cooldownMs = -1)
    {
        // 간단 구현: 2D 풀 대신 OneShot 3D
        // (정밀하게 하려면 3D 전용 풀을 따로 두고 관리)
        AudioSource.PlayClipAtPoint(clip, worldPos);
        // 필요 시도 voice limit 정책으로 통합 가능
    }
}
