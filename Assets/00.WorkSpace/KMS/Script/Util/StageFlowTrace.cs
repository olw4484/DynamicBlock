using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class StageFlowTrace
{
    public static bool Verbose = true;
    private static int _seq;

    public static int NewSeq(string source)
    {
        var id = ++_seq;
        if (Verbose) Debug.Log($"[flow#{id}] BEGIN <{source}> frame={Time.frameCount} t={Time.time:F3}");
        return id;
    }

    public static void Log(int id, string msg, Object ctx = null)
    {
        if (!Verbose) return;
        if (ctx) Debug.Log($"[flow#{id}] {msg}", ctx);
        else Debug.Log($"[flow#{id}] {msg}");
    }

    public static string DumpButtons(IEnumerable<GameObject> buttons)
    {
        var sb = new StringBuilder();
        int i = 0;
        foreach (var go in buttons)
        {
            var state = go
                ? (go.GetComponent<EnterStageButton>()?.StageButtonState.ToString() ?? "no-EnterStageButton")
                : "null";
            sb.Append($"[{i}:{state}] ");
            i++;
        }
        return sb.ToString();
    }

    public static string DumpButtons(IEnumerable<Transform> buttons)
        => DumpButtons(buttons.Select(t => t ? t.gameObject : null));
    public static string DumpButtons(IEnumerable<Component> buttons)
        => DumpButtons(buttons.Select(c => c ? c.gameObject : null));
}
