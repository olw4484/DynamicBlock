using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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