using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioServiceAdapter : IAudioService
{
    public int Order => 50; // Scene/BGM �ڿ��� ���� (DI�� ���� ���� ��)

    private AudioManager AM
    {
        get
        {
            var am = AudioManager.Instance ?? Object.FindFirstObjectByType<AudioManager>();
            if (!am) Debug.LogError("[AudioServiceAdapter] AudioManager not found.");
            return am;
        }
    }

    // IManager
    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }

    // BGM
    public void PlayBgm(AudioClip clip) => AM?.PlayBGM(clip);
    public void StopBgm() => AM?.StopBGM();
    public void SetBgmVolume(float v) => AM?.SetBGMVolume(v);

    // SE (2D)
    public void PlaySe(AudioClip clip, bool vibrate = false)
        => AM?.PlaySE(clip, vibrate);

    // SE
    public void PlaySeAt(AudioClip clip, Vector3? worldPos = null, float volumeScale = 1f, float pitch = 1f, bool vibrate = false)
        => AM?.PlaySE(clip, vibrate);

    public void SetSeVolume(float v) => AM?.SetSEVolume(v);

    // ������
    public void PlayLineClear(int lineCount)
    {
        Debug.Log($"[AUD] PlayLineClear({lineCount}) @frame={Time.frameCount}");
        AM?.PlayLineClearSE(lineCount);
    }

    public void PlayClearCombo(int n)
    {
        Debug.Log($"[AUD] PlayClearCombo({n}) @frame={Time.frameCount}");
        AM?.PlayClearComboSE(n);
    }

    // ��ü �Ͻ����� / �����
    public void StopAllSe() => AM?.StopAllSe();
    public void PauseAll() => AM?.PauseAll();
    public void ResumeAll() => AM?.ResumeAll();

    // ���� ���� ����
    public void PlayContinueTimeCheckSE() => AM?.PlayContinueTimeCheckSE();
    public void StopContinueTimeCheckSE() => AM?.StopContinueTimeCheckSE();
}

