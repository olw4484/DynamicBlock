using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AudioServiceExtensions
{
    public static void PlayButtonClick(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Button);

    public static void PlayClassicStageEnter(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Classic_StageEnter);

    public static void PlayStageEnter(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Adven_StageEnter);

    public static void PlayClassicGameOver(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Classic_GameOver, vibrate: true);

    public static void PlayClassicNewRecord(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Classic_NewRecord, vibrate: true);

    public static void PlayAdvenFail(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Adven_Fail, vibrate: true);

    public static void PlayAdvenClear(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_Adven_Clear, vibrate: true);

    public static void PlayBlockSelect(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_BlockSelect);

    public static void PlayBlockPlace(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_BlockPlace, vibrate: true);

    public static void PlayContinueTimeCheck(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_ContinueTimeCheck);

    public static void PlayClearAllBlock(this IAudioService a)
        => a.PlaySe(AudioManager.Instance.SE_ClearAllBlock, vibrate: true);
}
