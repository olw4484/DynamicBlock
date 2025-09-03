using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioServiceAdapter : IAudioService
{
    public int Order => 50;

    private AudioManager _am;

    //IManager 수명주기
    public void PreInit() { }
    public void Init()
    {
        _am = AudioManager.Instance;
        if (_am == null)
            Debug.LogWarning("[AudioServiceAdapter] AudioManager.Instance is null. Make sure it's in the scene.");
    }
    public void PostInit() { }
    public void Shutdown() { }

    //BGM
    public void PlayBgm(AudioClip clip)
    {
        if (_am) _am.PlayBGM(clip);
    }
    public void StopBgm()
    {
        if (_am) _am.StopBGM();
    }
    public void SetBgmVolume(float v)
    {
        if (_am) _am.SetBGMVolume(v);
    }

    //SE (Generic)
    public void PlaySe(AudioClip clip, bool vibrate = false)
    {
        if (_am) _am.PlaySE(clip, vibrate);
    }
    public void SetSeVolume(float v)
    {
        if (_am) _am.SetSEVolume(v);
    }

    //Domain Helpers 
    public void PlayLineClear(int lineCount)
    {
        if (_am) _am.PlayLineClearSE(lineCount);
    }
    public void PlayBlockSelect()
    {
        if (_am) _am.PlayBlockSelectSE();
    }
    public void PlayBlockPlace()
    {
        if (_am) _am.PlayBlockPlaceSE();
    }
    public void PlayStageEnter()
    {
        if (_am) _am.PlayStageEnterSE();
    }
    public void PlayButtonClick()
    {
        if (_am) _am.PlayButtonClickSE();
    }
}

