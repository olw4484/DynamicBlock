using UnityEngine;

public class GridFitter : MonoBehaviour
{
    public RectTransform board;   // ���� ��ü Rect
    public RectTransform[] cells; // �׸��� ĭ�� (���� ���� or ���� ����)
    public int gridCount = 8;     // ��: 8x8 ����

    void LateUpdate()
    {
        if (!board) return;

        float size = board.rect.width;        // ����� ���簢
        float cellSize = size / gridCount;    // �� ĭ ũ��

        foreach (var cell in cells)
        {
            if (!cell) continue;
            cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize);
            cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize);
        }
    }
}
