using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// ================================
// Project : DynamicBlock
// Script  : SoundManager.cs
// Desc    : BGM 1ä�� + SFX ����(���̽� ���� 3) ���� ������
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
    [SerializeField] private AudioMixerGroup _bgmMixer; // ����(��� OK)
    [SerializeField] private float _bgmDefaultFade = 0.5f;

    [Header("SFX")]
    [SerializeField] private AudioMixerGroup _sfxMixer; // ����
    [SerializeField, Range(1, 16)] private int _sfxVoices = 3; // ���� ��� ����
    [SerializeField] private bool _preventSpam = true;
    [SerializeField, Range(0, 500)] private int _defaultCooldownMs = 60; // ����Ŭ�� ��ٿ�(�ɼ�)

    [Header("Clip")]
    [SerializeField] private AudioClip _sfxPlace;   // ���� ��ġ
    [SerializeField] private AudioClip _sfxClear;   // ���� Ŭ����(�̹� ��� ���̸� ����)
    [SerializeField] private AudioClip _sfxGameOver;

    private AudioSource _bgmSource;
    private readonly List<AudioSource> _sfxSources = new();  // ���̽� Ǯ
    private readonly Dictionary<AudioClip, float> _lastPlayTime = new(); // ���Թ���

    // �� �ҽ��� ��Ÿ(�켱����/���۽ð�)�� ����
    private class VoiceMeta { public int priority; public float startTime; public AudioClip clip; }
    private readonly Dictionary<AudioSource, VoiceMeta> _voiceMeta = new();

    // =====================================
    // # Lifecycle (IManager)
    // =====================================
    public void PreInit() { /* �ܺ� ����/���ҽ� ���� */ }

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
            src.spatialBlend = 0f; // 2D. �ʿ�� 3D�� ����
            src.outputAudioMixerGroup = _sfxMixer;
            _sfxSources.Add(src);
            _voiceMeta[src] = new VoiceMeta { priority = int.MinValue, startTime = -999f, clip = null };
        }
    }

    public void PostInit()
    {
        // �̺�Ʈ ���� ����
        EventBus.OnBoardCleared += () => {/* ��: Ŭ���� ���� SFX�� �ܺο��� ȣ�� */};
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
        // �켱���� ��: ��ġ���� ��~�� (0~1)
        PlaySFX(_sfxPlace, priority: 1);
    }

    private void HandleBoardCleared()
    {
        // Ŭ����� ���� �� ����(�켱������)
        PlaySFX(_sfxClear, priority: 2);
    }

    private void HandleGameOver()
    {
        PlaySFX(_sfxGameOver, priority: 3);
        StopBGM(); // ���̵�ƿ� ���� ������ ���� ����
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
    // # SFX Control (���̽� ���� + �켱���� + ���� ����)
    // =====================================
    /// <summary>
    /// SFX ��� (�켱������ ������ ��ü ����� ��)
    /// </summary>
    public void PlaySFX(AudioClip clip, int priority = 0, int cooldownMs = -1)
    {
        if (clip == null) return;
        if (cooldownMs < 0) cooldownMs = _defaultCooldownMs;

        // ���� ����(�ɼ�)
        if (_preventSpam && cooldownMs > 0)
        {
            float now = Time.unscaledTime * 1000f;
            if (_lastPlayTime.TryGetValue(clip, out var last) && now - last < cooldownMs) return;
            _lastPlayTime[clip] = now;
        }

        // 1) �� �ҽ� ã��
        var free = _sfxSources.Find(s => !s.isPlaying);
        if (free != null)
        {
            UseVoice(free, clip, priority);
            return;
        }

        // 2) ��ü ��� ����(�켱���� ���� �� �� ������ ��)
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

        // �� �켱������ �� ���� ���� ��ü (���ų� ������ ����)
        if (priority > minPriority)
        {
            UseVoice(candidate, clip, priority);
        }
        // else : ���� (��å�� ���� ������ �� ������ ��ü�ϵ��� �ٲ� ���� ����)
    }

    /// <summary>
    /// ����: ���̽� ���/��Ÿ ������Ʈ
    /// </summary>
    private void UseVoice(AudioSource src, AudioClip clip, int priority)
    {
        src.clip = clip;
        src.Play();
        var meta = _voiceMeta[src];
        meta.priority = priority;
        meta.startTime = Time.unscaledTime; // ���� �ð�
        meta.clip = clip;
    }

    // 3D ��ġ ���(�ɼ�)
    public void PlaySFXAt(AudioClip clip, Vector3 worldPos, int priority = 0, int cooldownMs = -1)
    {
        // ���� ����: 2D Ǯ ��� OneShot 3D
        // (�����ϰ� �Ϸ��� 3D ���� Ǯ�� ���� �ΰ� ����)
        AudioSource.PlayClipAtPoint(clip, worldPos);
        // �ʿ� �õ� voice limit ��å���� ���� ����
    }
}
