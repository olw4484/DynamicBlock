using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoardFitter : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform safeArea;      // SafeArea ��Ʈ
    public RectTransform board;         // ���� ����(���簢)
    public RectTransform topHUD;        // ��� HUD ��Ʈ
    public RectTransform bottomTray;    // �ϴ� Ʈ���� ��Ʈ

    [Header("Margins (����/px ȥ��)")]
    [Range(0f, 0.25f)] public float topReserveRatio = 0.12f;    // ��� ���� ���� ����
    [Range(0f, 0.25f)] public float bottomReserveRatio = 0.12f; // �ϴ� ���� ���� ����
    public float minTopPx = 80f;      // �ʹ� ���� �� �ּ� px Ȯ��
    public float minBottomPx = 120f;

    [Header("Board Size Clamp")]
    [Range(0.4f, 1f)] public float maxBoardHeightRatio = 0.62f; // ���ΰ� ª�� �� ���尡 SafeArea ������ �� %���� �������

    Rect lastSafe;
    Vector2 lastSize;

    void LateUpdate()
    {
        // �����ϰ� ���� ����
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
        // SafeArea�� ���� ��/����
        float W = safeWorld.width;
        float H = safeWorld.height;

        // ��/�� ����ġ ��� (���� ��� + �ּ� px ����)
        float topRes = Mathf.Max(H * topReserveRatio, minTopPx);
        float botRes = Mathf.Max(H * bottomReserveRatio, minBottomPx);

        // ���尡 ����� �� �ִ� �ִ� ���簢 �� ����
        float maxByHeight = (H - topRes - botRes);
        maxByHeight = Mathf.Max(0f, maxByHeight) * maxBoardHeightRatio;

        float size = Mathf.Min(W, maxByHeight);

        // ���� ������/��ġ ���� (SafeArea ���� ��ǥ��)
        board.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        board.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

        // �߾� ����: �������� ���� pivot(0.5,0.5) ����
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
        board.pivot = new Vector2(0.5f, 0.5f);
        board.anchoredPosition = Vector2.zero;

        // ���/�ϴ� ��Ʈ�� ��Ŀ�� ���� (��: ���=Top stretch, �ϴ�=Bottom stretch)
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

    // SafeArea�� ���� Rect ���
    Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var min = corners[0];
        var max = corners[2];
        return new Rect(min, max - min);
    }
}
