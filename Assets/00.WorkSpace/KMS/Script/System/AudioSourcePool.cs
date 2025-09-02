using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// ===== 사운드 풀 (동시 5개 Ring) =====
public sealed class AudioSourcePool : MonoBehaviour
{
    [SerializeField] private int poolSize = 5;
    private readonly List<AudioSource> _pool = new();
    private int _cursor;

    [SerializeField] private AudioMixerGroup _mixer; // 선택

    void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject($"AudioSrc_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            if (_mixer) src.outputAudioMixerGroup = _mixer;
            _pool.Add(src);
        }
    }

    public AudioSource Rent()
    {
        var src = _pool[_cursor];
        _cursor = (_cursor + 1) % _pool.Count;
        return src;
    }

    public void StopAndClearAll()
    {
        foreach (var s in _pool) { s.Stop(); s.clip = null; }
    }
}

// ===== 사운드 레인 =====
public sealed class SoundLane : MonoBehaviour
{
    [Header("Budget / Cooldown")]
    [SerializeField] private int budgetPerFrame = 5;
    [SerializeField] private int defaultCooldownMs = 60; // 동일 SFX 최소 간격

    [Header("Clips")]
    [SerializeField] private AudioClip[] clips; // id = index

    [Header("Deps")]
    [SerializeField] private AudioSourcePool pool;

    private readonly PhaseBuffer<SoundEvent> _buf = new();
    private readonly Dictionary<int, double> _lastPlayedDsp = new(); // 쿨다운 체크용
    private readonly HashSet<int> _playedThisFrame = new(); // 코얼레싱

    // 외부에서 호출
    public void Enqueue(in SoundEvent e) => _buf.Enqueue(e, e.delay);

    public void TickBegin()
    {
        _playedThisFrame.Clear();
        _buf.TickBegin();
    }

    public void Consume()
    {
        _buf.Consume(budgetPerFrame, Play);
    }

    private void Play(SoundEvent e)
    {
        // 코얼레싱: 같은 프레임 동일 id 1회만
        if (_playedThisFrame.Contains(e.id)) return;

        // 쿨다운(DSP time 기준)
        var now = AudioSettings.dspTime;
        var cdSec = defaultCooldownMs / 1000.0;
        if (_lastPlayedDsp.TryGetValue(e.id, out var last) && (now - last) < cdSec) return;

        var clip = (e.id >= 0 && e.id < clips.Length) ? clips[e.id] : null;
        if (!clip) return;

        var src = pool.Rent();
        src.clip = clip;
        src.volume = 1f;
        src.pitch = 1f;
        // 타이밍 안정: 즉시 재생 또는 PlayScheduled(now)
        src.Play();

        _lastPlayedDsp[e.id] = now;
        _playedThisFrame.Add(e.id);
        StartCoroutine(ReturnWhenDone(src));
    }

    private IEnumerator ReturnWhenDone(AudioSource s)
    {
        yield return new WaitWhile(() => s.isPlaying);
        s.clip = null; // 풀 반환시 초기화
    }

    public void ClearAll()
    {
        _buf.Clear();
        pool.StopAndClearAll();
        _playedThisFrame.Clear();
        _lastPlayedDsp.Clear();
    }
}
