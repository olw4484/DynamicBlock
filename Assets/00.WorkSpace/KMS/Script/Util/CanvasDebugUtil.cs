using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CanvasDebugUtil
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void DumpOnceOnStart()
    {
        DumpCanvasStack("OnStart");
    }

    public static void DumpCanvasStack(string tag = "")
    {
        var canvases = GameObject.FindObjectsOfType<Canvas>(true)
            .Select(c => new
            {
                name = c.name,
                c.renderMode,
                c.sortingOrder,
                c.overrideSorting,
                sibling = c.transform.GetSiblingIndex(),
                goActive = c.gameObject.activeInHierarchy
            })
            .OrderBy(x => x.sibling)
            .ThenBy(x => x.sortingOrder);

        Debug.Log($"[CanvasDump:{tag}] ----");
        foreach (var cv in canvases)
            Debug.Log($"#{cv.sibling} order={cv.sortingOrder} override={cv.overrideSorting} mode={cv.renderMode} active={cv.goActive} name={cv.name}");
    }
}