using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioServiceAdapter : IAudioService
{
    public int Order => 50;
    private AudioManager _am;

    public void PreInit() { }
    public void Init()
    {
        _am = AudioManager.Instance;
        if (_am == null)
            Debug.LogWarning("[AudioServiceAdapter] AudioManager.Instance is null.");
    }
    public void PostInit() { }
    public void Shutdown() { }

    // BGM/SE 공통
    public void PlayBgm(AudioClip clip) { if (_am) _am.PlayBGM(clip); }
    public void StopBgm() { if (_am) _am.StopBGM(); }
    public void SetBgmVolume(float v) { if (_am) _am.SetBGMVolume(v); }
    public void PlaySe(AudioClip c, bool v) { if (_am) _am.PlaySE(c, v); }
    public void SetSeVolume(float v) { if (_am) _am.SetSEVolume(v); }

    // 도메인 헬퍼
    public void PlayLineClear(int n) { if (_am) _am.PlayLineClearSE(n); }
    public void PlayClearCombo(int n) { if (_am) _am.PlayClearComboSE(n); }
    public void PlayClearAllBlock() { if (_am) _am.PlayClearAllBlockSE(); }

    public void PlayBlockSelect() { if (_am) _am.PlayBlockSelectSE(); }
    public void PlayBlockPlace() { if (_am) _am.PlayBlockPlaceSE(); }
    public void PlayStageEnter() { if (_am) _am.PlayStageEnterSE(); }
    public void PlayButtonClick() { if (_am) _am.PlayButtonClickSE(); }

    public void PlayClassicGameOver() { if (_am) _am.PlayClassicGameOverSE(); }
    public void PlayClassicNewRecord() { if (_am) _am.PlayClassicNewRecordSE(); }
    public void PlayAdvenFail() { if (_am) _am.PlayAdvenFailSE(); }
    public void PlayAdvenClear() { if (_am) _am.PlayAdvenClearSE(); }
    public void PlayContinueTimeCheck() { if (_am) _am.PlayContinueTimeCheckSE(); }
}

