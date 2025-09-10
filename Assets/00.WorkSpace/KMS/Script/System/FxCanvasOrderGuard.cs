using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(Canvas))]
public sealed class FxCanvasGuard : MonoBehaviour
{
    [SerializeField] int order = 32760;
    Canvas c;

    void Awake() { Cache(); Force(); }
    void OnEnable() { Cache(); Force(); }
    void LateUpdate() { Keep(); }

    void Cache()
    {
        if (!c) c = GetComponent<Canvas>();
    }

    void Force()
    {
        if (!c) return;
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = Mathf.Clamp(order, short.MinValue + 1, short.MaxValue - 1);

        if (transform.parent != null)
            transform.SetAsLastSibling();
    }

    void Keep()
    {
        if (!c) return;

        if (!c.overrideSorting)
        {
            Debug.LogWarning("[FxCanvasGuard] overrideSorting was OFF. Re-enabling.\n" +
                             StackTraceUtility.ExtractStackTrace());
            c.overrideSorting = true;
        }

        if (c.sortingOrder != order)
            c.sortingOrder = order;

        // 루트일 수 있으니 null 체크 후 처리
        if (transform.parent != null)
        {
            int last = transform.parent.childCount - 1;
            if (transform.GetSiblingIndex() != last)
                transform.SetAsLastSibling();
        }
    }
}
