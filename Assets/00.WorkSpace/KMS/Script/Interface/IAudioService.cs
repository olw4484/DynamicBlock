using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : IAudioService.cs
// Desc    : ���� �ܺ� API ���
// ================================

public interface IAudioService
{
    void PlayBGM(AudioClip clip, bool loop = true, float volume = 1f);
    void StopBGM(float fadeSec = 0.25f);
    void PlaySE(AudioClip clip, float volume = 1f);
    void SetVolume(float bgmVolume, float seVolume); // 0~1
}
