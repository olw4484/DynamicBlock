using UnityEngine;

public static class Diag
{
    // 개발/에디터에서만 출력
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void DumpAll(string tag)
    {
        bool token = AdReviveToken.HasPending();
        bool fs = AdStateProbe.IsFullscreenShowing;
        bool pending = AdStateProbe.IsRevivePending;
        bool armed = ReviveGate.IsArmed;
        bool latch = ReviveLatch.Active;

        bool grace = UIStateProbe.ReviveGraceActive;
        bool guard = UIStateProbe.ResultGuardActive;
        bool rOpen = UIStateProbe.IsResultOpen;
        bool vOpen = UIStateProbe.IsReviveOpen;
        bool anyMod = UIStateProbe.IsAnyModalOpen;

        bool suppress = token || fs || pending || armed || grace || guard;

        Debug.Log(
            $"[DUMP:{tag}] " +
            $"suppress={suppress} " +
            $"| token={token} fs={fs} pending={pending} armed={armed} latch={latch} " +
            $"| grace={grace} guard={guard} " +
            $"| resultOpen={rOpen} reviveOpen={vOpen} anyModal={anyMod}"
        );
    }
}
