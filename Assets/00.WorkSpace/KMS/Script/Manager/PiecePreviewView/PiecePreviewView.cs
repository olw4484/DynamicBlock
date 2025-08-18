using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ================================
// Project : DynamicBlock
// Script  : PiecePreviewView.cs
// Desc    : ���� ������(���� �� UI) ǥ��
// Note    : Vector2Int[] �� ��ǥ ������� UGUI ������ ��ġ
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/PiecePreviewView")]
public class PiecePreviewView : MonoBehaviour
{
    // =====================================
    // # Fields (Serialized / Private)
    // =====================================
    [Header("Preview_Content (�� �θ�)")]
    [SerializeField] private RectTransform contentRoot;

    [Header("Sell_Prefab(UI Image)")]
    [SerializeField] private GameObject cellPrefab;

    [Header("Sell_Size")]
    [SerializeField, Min(1f)] private float cellSize = 24f;
    [SerializeField, Min(0f)] private float cellSpacing = 2f;

    [Header("Sell_Color")]
    [SerializeField] private Color cellColor = Color.white;

    [Header("AutoScale")]
    [SerializeField] private bool autoScaleToFit = true;
    [SerializeField] private Vector2 maxFrameSize = new Vector2(120f, 120f);

    // Ǯ��
    private readonly List<Image> _pool = new();
    private int _activeCount = 0;

    // =====================================
    // # Public API
    // =====================================
    /// <summary>
    /// ���� �� ��ǥ�� �޾� �����並 �����Ѵ�.
    /// </summary>
    public void SetData(Vector2Int[] shape)
    {
        if (contentRoot == null || cellPrefab == null)
        {
            Debug.LogWarning("[PiecePreviewView] contentRoot or cellPrefab not set.");
            return;
        }

        // 1) �� �����͸� Ŭ����
        if (shape == null || shape.Length == 0)
        {
            Clear();
            return;
        }

        // 2) �ٿ�� �ڽ� ���
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in shape)
        {
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }

        int width = (maxX - minX + 1);
        int height = (maxY - minY + 1);

        // 3) �� ũ��/�������� ���� �ȼ� ũ�� ���
        float step = cellSize + cellSpacing;
        Vector2 contentSize = new Vector2(width * step - cellSpacing, height * step - cellSpacing);

        // 4) ���� ������ �ɼ�: ū ������ ���
        float scale = 1f;
        if (autoScaleToFit)
        {
            float sx = maxFrameSize.x / Mathf.Max(contentSize.x, 1f);
            float sy = maxFrameSize.y / Mathf.Max(contentSize.y, 1f);
            scale = Mathf.Min(1f, Mathf.Min(sx, sy));
        }

        // 5) ������ �߾� ����: (0,0)�� ���ϴ����� ��ġ�� ��,
        //    contentRoot�� pivot�� (0.5,0.5)��� �����ϰ� �߾����� �̵�
        Vector2 pivotOffset = new Vector2(-contentSize.x * 0.5f, -contentSize.y * 0.5f);

        // 6) Ǯ Ȯ��
        EnsurePool(shape.Length);

        // 7) �� ��ġ
        _activeCount = 0;
        for (int i = 0; i < shape.Length; i++)
        {
            var local = shape[i];
            int nx = local.x - minX; // ����ȭ
            int ny = local.y - minY;

            float px = nx * step + pivotOffset.x;
            float py = ny * step + pivotOffset.y;

            var img = _pool[_activeCount++];
            var rt = (RectTransform)img.transform;

            img.color = cellColor;
            img.gameObject.SetActive(true);

            // ũ��/��ġ ����
            rt.SetParent(contentRoot, false);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = new Vector2(px, py);
        }

        // 8) ���� Ǯ ��Ȱ��ȭ
        for (int i = _activeCount; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);

        // 9) ������ ����
        contentRoot.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// �� ������ ����(�׸�/���¿� ���� ��������).
    /// </summary>
    public void SetColor(Color c)
    {
        cellColor = c;
        for (int i = 0; i < _activeCount; i++)
            _pool[i].color = cellColor;
    }

    /// <summary>
    /// ������ �ʱ�ȭ(��� ����).
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);

        _activeCount = 0;
    }

    // =====================================
    // # Helpers (Pool)
    // =====================================
    private void EnsurePool(int required)
    {
        while (_pool.Count < required)
        {
            var go = Instantiate(cellPrefab, contentRoot);
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            go.SetActive(false);
            _pool.Add(img);
        }
    }
}