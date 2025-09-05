using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class NullSoundManager : IManager
{
    public int Order => 50;
    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }
    public void PlayBGM(AudioClip c, bool loop = true, float v = 1f) { }
    public void PlaySE(AudioClip c, float v = 1f) { }
    public void SetVolume(float bgm, float se) { }
    public void StopBGM(float fadeSec = 0.25F){}
}
