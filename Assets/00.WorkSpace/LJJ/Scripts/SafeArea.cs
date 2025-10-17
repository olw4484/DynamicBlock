using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafe;

    [SerializeField] int extraTopPx = 0;      // 상단 여백(px)
    [SerializeField] int extraBottomPx = 0;   // 하단 여백(px) (배너 등)
    [SerializeField] int extraLeftPx = 0;
    [SerializeField] int extraRightPx = 0;
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
    public void SetExtraLeft(int px)
    {
        extraLeftPx = Mathf.Max(0, px);
        Apply();
    }
    public void SetExtraRight(int px)
    {
        extraRightPx = Mathf.Max(0, px);
        Apply();
    }

    void Apply()
    {
        var safe = Screen.safeArea;

        // 상/하 여백
        if (useTop && extraTopPx > 0) safe.yMax -= extraTopPx;
        if (useBottom && extraBottomPx > 0) safe.yMin += extraBottomPx;

        // 좌/우 여백 (추가)
        if (useRight && extraRightPx > 0) safe.xMax -= extraRightPx;
        if (useLeft && extraLeftPx > 0) safe.xMin += extraLeftPx;

        // 역전 방지 (높이/너비 최소값 보정)
        if (safe.yMax < safe.yMin + 1f)
        {
            float mid = (safe.yMin + safe.yMax) * 0.5f;
            safe.yMin = mid - 0.5f; safe.yMax = mid + 0.5f;
        }
        if (safe.xMax < safe.xMin + 1f)
        {
            float mid = (safe.xMin + safe.xMax) * 0.5f;
            safe.xMin = mid - 0.5f; safe.xMax = mid + 0.5f;
        }

        float w = Screen.width, h = Screen.height;
        Vector2 min = safe.position, max = safe.position + safe.size;
        min.x /= w; min.y /= h; max.x /= w; max.y /= h;

        if (!useLeft) min.x = 0f;
        if (!useBottom) min.y = 0f;
        if (!useRight) max.x = 1f;
        if (!useTop) max.y = 1f;

        rt.anchorMin = min;
        rt.anchorMax = max;

        // (옵션) safe를 무시하는 쪽에도 extra px를 강제로 적용하고 싶다면 아래처럼:
        // rt.offsetMin = new Vector2(!useLeft ?  extraLeftPx  : 0f, !useBottom ? extraBottomPx : 0f);
        // rt.offsetMax = new Vector2(!useRight ? -extraRightPx : 0f, !useTop    ? -extraTopPx  : 0f);
        // 아니라면 0으로 유지
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        lastSafe = Screen.safeArea;
    }
}
