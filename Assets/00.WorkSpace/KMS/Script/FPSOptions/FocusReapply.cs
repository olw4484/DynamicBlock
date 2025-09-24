using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FocusReapply : MonoBehaviour
{
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) FrameSettings.Reapply();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) FrameSettings.Reapply();
    }
}
