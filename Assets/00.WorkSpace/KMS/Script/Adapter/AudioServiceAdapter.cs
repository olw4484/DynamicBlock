using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioServiceAdapter : IAudioService
{
    public int Order => 50; // Scene/BGM 뒤여도 무방 (DI로 직접 참조 줌)

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

    // 패턴형
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

    // 전체 일시정지 / 재시작
    public void StopAllSe() => AM?.StopAllSe();
    public void PauseAll() => AM?.PauseAll();
    public void ResumeAll() => AM?.ResumeAll();

    // 전용 루프 제어
    public void PlayContinueTimeCheckSE() => AM?.PlayContinueTimeCheckSE();
    public void StopContinueTimeCheckSE() => AM?.StopContinueTimeCheckSE();
}

