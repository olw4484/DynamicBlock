using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioPool : MonoBehaviour
{
    public static AudioPool Instance;
    public int poolSize = 10;
    private List<AudioSource> sources = new List<AudioSource>();

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            sources.Add(src);
        }
    }

    public void PlayClip(AudioClip clip, float volume = 1f)
    {
        foreach (var src in sources)
        {
            if (!src.isPlaying)
            {
                src.clip = clip;
                src.volume = volume;
                src.Play();
                return;
            }
        }
        // 모두 사용 중이라면 첫 번째 소스를 강제로 재생 (혹은 무시)
        sources[0].clip = clip;
        sources[0].volume = volume;
        sources[0].Play();
    }
}
