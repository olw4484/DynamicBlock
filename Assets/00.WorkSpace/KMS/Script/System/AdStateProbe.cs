using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AdStateProbe
{
    public static bool IsAdShowing =>
        AdPauseGuard.IsAdShowing;

    public static bool IsRevivePending =>
        AdReviveToken.HasPending() || ReviveGate.IsArmed;
}
