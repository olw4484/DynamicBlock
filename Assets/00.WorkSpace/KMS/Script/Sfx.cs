using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    static void Play(int id)
    {
        Debug.Log($"[SFX] Try Play id={id}, IsBound={Game.IsBound}, AudioFxNull? {Game.AudioFx == null}");
        if (!Game.IsBound || Game.AudioFx == null) return;
        Game.AudioFx.EnqueueSound(id);
    }
    public static void PlayId(int id) => Play(id);
    public static void Button() => Play((int)SfxId.ButtonClick);
    public static void BlockPlace() => Play((int)SfxId.BlockPlace);
    public static void BlockSelect() => Play((int)SfxId.BlockSelect);
    public static void StageEnter() => Play((int)SfxId.AdvenStageEnter);
    public static void Combo(int n) => Play(1010 + Mathf.Clamp(n, 1, 8));  // 1011~1018
    public static void LineClear(int n) => Play(1019 + Mathf.Clamp(n, 1, 6));  // 1020~1025
    public static void ClearAll() => Play((int)SfxId.ClearAllBlock);
    public static void NewRecord() => Play((int)SfxId.ClassicNewRecord);
    public static void GameOver() => Play((int)SfxId.ClassicGameOver);
    public static void ClassicStageEnter() => Play((int)SfxId.ClassicStageEnter);

    public static void StageClear() => Play((int)SfxId.AdvenClear);
    public static void Stagefail() => Play((int)SfxId.AdvenFail);
}
