using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasDebug : MonoBehaviour
{
    void OnEnable()
    {
        var cv = GetComponentInParent<Canvas>();
        if (!cv) { Debug.Log($"[CanvasDebug] {name} ¡æ (no canvas)"); return; }
        Debug.Log($"[CanvasDebug] {name} ¡æ Canvas={cv.name}, mode={cv.renderMode}, override={cv.overrideSorting}, order={cv.sortingOrder}");
    }
}