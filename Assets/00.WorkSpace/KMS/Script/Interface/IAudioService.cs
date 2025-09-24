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

    // SE (간단 2D)
    void PlaySe(AudioClip clip, bool vibrate = false);

    // SE (고급/3D)
    void PlaySeAt(AudioClip clip, Vector3? worldPos = null, float volumeScale = 1f, float pitch = 1f, bool vibrate = false);

    // 볼륨
    void SetSeVolume(float v);

    // 패턴형
    void PlayLineClear(int lineCount);
    void PlayClearCombo(int n);

    // 모든 SFX 정지/일시정지/재개
    void StopAllSe();
    void PauseAll();
    void ResumeAll();

    // 전용 루프 제어
    void PlayContinueTimeCheckSE();
    void StopContinueTimeCheckSE();
}
