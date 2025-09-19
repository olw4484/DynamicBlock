using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafe;

    [SerializeField] int extraTopPx = 0;      // 상단 여백(px)
    [SerializeField] int extraBottomPx = 0;   // 하단 여백(px) (배너 등)
    [SerializeField] bool useTop = true, useBottom = true, useLeft = true, useRight = true;

    void Awake() { rt = GetComponent<RectTransform>(); }
    void OnEnable() { Apply(); }
    void Start() { Apply(); }
    void Update() { if (lastSafe != Screen.safeArea) Apply(); }
    public void SetExtraTopPx(int px)
    {
        extraTopPx = Mathf.Max(0, px);
        Apply();
    }
    public void SetExtraBottomPx(int px)
    {
        extraBottomPx = Mathf.Max(0, px);
        Apply();
    }

    void Apply()
    {
        var safe = Screen.safeArea;

        if (useTop && extraTopPx > 0)
            safe.yMax -= extraTopPx;

        if (useBottom && extraBottomPx > 0)
            safe.yMin += extraBottomPx;

        if (safe.yMax < safe.yMin + 1f)
        {
            float mid = (safe.yMin + safe.yMax) * 0.5f;
            safe.yMin = mid - 0.5f;
            safe.yMax = mid + 0.5f;
        }

        float w = Screen.width, h = Screen.height;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= w; min.y /= h;
        max.x /= w; max.y /= h;

        if (!useLeft) min.x = 0f;
        if (!useBottom) min.y = 0f;
        if (!useRight) max.x = 1f;
        if (!useTop) max.y = 1f;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        lastSafe = Screen.safeArea;
    }
}
