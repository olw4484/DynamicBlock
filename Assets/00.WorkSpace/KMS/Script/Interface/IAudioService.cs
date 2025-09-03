using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : IAudioService.cs
// Desc    : ���� �ܺ� API ���
// ================================

public interface IAudioService : IManager
{
    // BGM
    void PlayBgm(AudioClip clip);
    void StopBgm();
    void SetBgmVolume(float v);

    // SE (Generic)
    void PlaySe(AudioClip clip, bool vibrate = false);
    void SetSeVolume(float v);

    // Domain Helpers (���� ���� ����)
    void PlayLineClear(int lineCount); // 1~6��
    void PlayBlockSelect();
    void PlayBlockPlace();
    void PlayStageEnter();
    void PlayButtonClick();
}
