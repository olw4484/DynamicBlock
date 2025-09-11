using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AudioUI
{
    static AudioManager AM => AudioManager.Instance;

    public static void FlipBgm() { if (!AM) return; AM.SetBgmOn(!AM.IsBgmOn); Sfx.Button(); }
    public static void FlipSe() { if (!AM) return; AM.SetSeOn(!AM.IsSeOn); Sfx.Button(); }
    public static void FlipVibration() { if (!AM) return; AM.SetVibrateEnabled(!AM.VibrateEnabled); Sfx.Button(); }

    public static void SetBgm(bool on) { if (!AM) return; AM.SetBgmOn(on); Sfx.Button(); }
    public static void SetSe(bool on) { if (!AM) return; AM.SetSeOn(on); Sfx.Button(); }
    public static void SetVibration(bool on) { if (!AM) return; AM.SetVibrateEnabled(on); Sfx.Button(); }
}
