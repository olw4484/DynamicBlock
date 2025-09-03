using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform)), RequireComponent(typeof(RawImage))]
public class FullscreenRawImage : MonoBehaviour
{
    public bool keepOnTop = true;
    public bool ignoreLayout = true;

    void Awake()
    {
        var rt = GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var ri = GetComponent<RawImage>();
        ri.raycastTarget = false;
        ri.uvRect = new Rect(0, 0, 1, 1);

        if (ignoreLayout)
        {
            var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
    }

    void LateUpdate()
    {
        if (keepOnTop) transform.SetAsLastSibling();
    }
}
