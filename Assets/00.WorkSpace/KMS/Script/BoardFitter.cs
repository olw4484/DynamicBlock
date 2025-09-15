using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoardFitter : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform safeArea;      // SafeArea 루트
    public RectTransform board;         // 실제 보드(정사각)
    public RectTransform topHUD;        // 상단 HUD 루트
    public RectTransform bottomTray;    // 하단 트레이 루트

    [Header("Margins (비율/px 혼합)")]
    [Range(0f, 0.25f)] public float topReserveRatio = 0.12f;    // 상단 영역 비율 예약
    [Range(0f, 0.25f)] public float bottomReserveRatio = 0.12f; // 하단 영역 비율 예약
    public float minTopPx = 80f;      // 너무 좁을 때 최소 px 확보
    public float minBottomPx = 120f;

    [Header("Board Size Clamp")]
    [Range(0.4f, 1f)] public float maxBoardHeightRatio = 0.62f; // 세로가 짧을 때 보드가 SafeArea 높이의 몇 %까지 허용할지

    Rect lastSafe;
    Vector2 lastSize;

    void LateUpdate()
    {
        // 안전하게 변경 감지
        if (safeArea == null || board == null) return;

        var rect = GetWorldRect(safeArea);
        if (rect.size != lastSize || Screen.safeArea != lastSafe)
        {
            Fit(rect);
            lastSize = rect.size;
            lastSafe = Screen.safeArea;
        }
    }

    void Fit(Rect safeWorld)
    {
        // SafeArea의 가용 폭/높이
        float W = safeWorld.width;
        float H = safeWorld.height;

        // 상/하 예약치 계산 (비율 기반 + 최소 px 보정)
        float topRes = Mathf.Max(H * topReserveRatio, minTopPx);
        float botRes = Mathf.Max(H * bottomReserveRatio, minBottomPx);

        // 보드가 사용할 수 있는 최대 정사각 변 길이
        float maxByHeight = (H - topRes - botRes);
        maxByHeight = Mathf.Max(0f, maxByHeight) * maxBoardHeightRatio;

        float size = Mathf.Min(W, maxByHeight);

        // 보드 사이즈/위치 적용 (SafeArea 내부 좌표로)
        board.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        board.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

        // 중앙 정렬: 안전영역 내부 pivot(0.5,0.5) 가정
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
        board.pivot = new Vector2(0.5f, 0.5f);
        board.anchoredPosition = Vector2.zero;

        // 상단/하단 루트는 앵커로 고정 (예: 상단=Top stretch, 하단=Bottom stretch)
        if (topHUD)
        {
            topHUD.anchorMin = new Vector2(0f, 1f);
            topHUD.anchorMax = new Vector2(1f, 1f);
            topHUD.pivot = new Vector2(0.5f, 1f);
            topHUD.sizeDelta = new Vector2(0f, topRes);
            topHUD.anchoredPosition = Vector2.zero;
        }
        if (bottomTray)
        {
            bottomTray.anchorMin = new Vector2(0f, 0f);
            bottomTray.anchorMax = new Vector2(1f, 0f);
            bottomTray.pivot = new Vector2(0.5f, 0f);
            bottomTray.sizeDelta = new Vector2(0f, botRes);
            bottomTray.anchoredPosition = Vector2.zero;
        }
    }

    // SafeArea의 월드 Rect 얻기
    Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var min = corners[0];
        var max = corners[2];
        return new Rect(min, max - min);
    }
}
