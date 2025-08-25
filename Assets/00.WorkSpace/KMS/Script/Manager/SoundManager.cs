using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : SoundManager.cs
// Desc    : BGM/SE 라우팅 + 풀
// ================================

public sealed class SoundManager : IManager, ITickable, ITeardown
{
    public int Order => 50;

    private GameObject _root;
    private AudioSource _bgm;
    private readonly List<AudioSource> _sePool = new();
    private int _poolSize = 8;

    public void PreInit() { /* 오디오 옵션 로드 등 */ }

    public void Init()
    {
        _root = new GameObject("SoundRoot");
        Object.DontDestroyOnLoad(_root);

        _bgm = _root.AddComponent<AudioSource>();
        _bgm.loop = true;

        for (int i = 0; i < _poolSize; i++)
            _sePool.Add(_root.AddComponent<AudioSource>());
    }

    public void PostInit() { /* 이벤트 구독 필요시 */ }

    public void Tick(float dt) { /* 페이드 등 */ }

    public void Teardown()
    {
        if (_root != null) Object.Destroy(_root);
        _sePool.Clear();
    }

    // 외부 API
    public void PlayBGM(AudioClip clip, bool loop = true, float volume = 1f)
    {
        if (!clip) return;
        _bgm.clip = clip; _bgm.loop = loop; _bgm.volume = volume; _bgm.Play();
    }
    public void StopBGM() => _bgm.Stop();

    public void PlaySE(AudioClip clip, float volume = 1f)
    {
        if (!clip) return;
        foreach (var src in _sePool)
        {
            if (!src.isPlaying) { src.clip = clip; src.volume = volume; src.Play(); return; }
        }
        var extra = _root.AddComponent<AudioSource>();
        _sePool.Add(extra);
        extra.clip = clip; extra.volume = volume; extra.Play();
    }
}
