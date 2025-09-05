using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : IAudioService.cs
// Desc    : 사운드 외부 API 계약
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

    // Domain Helpers (퍼즐 게임 전용)
    void PlayLineClear(int lineCount); // 1~6줄
    void PlayBlockSelect();
    void PlayBlockPlace();
    void PlayStageEnter();
    void PlayButtonClick();
}
