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

    [SerializeField] private AudioMixerGroup _mixer;

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
