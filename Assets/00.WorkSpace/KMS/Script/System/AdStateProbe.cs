using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AdStateProbe
{
    static bool _fs, _revive, _banner;

    public static bool IsFullscreenShowing
    {
        get => _fs;
        set { if (_fs != value) { _fs = value; UnityEngine.Debug.Log($"[AdProbe] Fullscreen={value}"); } }
    }

    public static bool IsRevivePending
    {
        get => _revive;
        set { if (_revive != value) { _revive = value; UnityEngine.Debug.Log($"[AdProbe] RevivePending={value}"); } }
    }

    public static bool IsBannerShowing
    {
        get => _banner;
        set { if (_banner != value) { _banner = value; UnityEngine.Debug.Log($"[AdProbe] Banner={value}"); } }
    }
}