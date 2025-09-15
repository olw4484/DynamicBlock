using UnityEngine;

public class GridFitter : MonoBehaviour
{
    public RectTransform board;   // 보드 전체 Rect
    public RectTransform[] cells; // 그리드 칸들 (직접 참조 or 동적 생성)
    public int gridCount = 8;     // 예: 8x8 보드

    void LateUpdate()
    {
        if (!board) return;

        float size = board.rect.width;        // 보드는 정사각
        float cellSize = size / gridCount;    // 한 칸 크기

        foreach (var cell in cells)
        {
            if (!cell) continue;
            cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize);
            cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize);
        }
    }
}
