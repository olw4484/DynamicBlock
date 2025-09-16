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

    // SE (���� 2D)
    void PlaySe(AudioClip clip, bool vibrate = false);

    // SE (���/3D)
    void PlaySeAt(AudioClip clip, Vector3? worldPos = null, float volumeScale = 1f, float pitch = 1f, bool vibrate = false);

    // ����
    void SetSeVolume(float v);

    // ������
    void PlayLineClear(int lineCount);
    void PlayClearCombo(int n);

    // ��� SFX ����/�Ͻ�����/�簳
    void StopAllSe();
    void PauseAll();
    void ResumeAll();

    // ���� ���� ����
    void PlayContinueTimeCheckSE();
    void StopContinueTimeCheckSE();
}
