using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : IAudioService.cs
// Desc    : 사운드 외부 API 계약
// ================================

public interface IAudioService : IManager
{
    void PlayBgm(AudioClip clip);
    void StopBgm();
    void SetBgmVolume(float v);
    void PlaySe(AudioClip clip, bool vibrate = false);
    void SetSeVolume(float v);
    void PlayLineClear(int lineCount);
    void PlayClearCombo(int n);
    void PlayClearAllBlock();
    void PlayBlockSelect();
    void PlayBlockPlace();
    void PlayStageEnter();
    void PlayButtonClick();
    void PlayClassicGameOver();
    void PlayClassicNewRecord();
    void PlayAdvenFail();
    void PlayAdvenClear();
    void PlayContinueTimeCheck();
}
