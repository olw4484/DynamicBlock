using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ================================
// Project : DynamicBlock
// Script  : PiecePreviewView.cs
// Desc    : 손패 프리뷰(조각 셀 UI) 표시
// Note    : Vector2Int[] 셀 좌표 기반으로 UGUI 셀들을 배치
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/PiecePreviewView")]
public class PiecePreviewView : MonoBehaviour
{
    // =====================================
    // # Fields (Serialized / Private)
    // =====================================
    [Header("Preview_Content (셀 부모)")]
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

    // 풀링
    private readonly List<Image> _pool = new();
    private int _activeCount = 0;

    // =====================================
    // # Public API
    // =====================================
    /// <summary>
    /// 조각 셀 좌표를 받아 프리뷰를 갱신한다.
    /// </summary>
    public void SetData(Vector2Int[] shape)
    {
        if (contentRoot == null || cellPrefab == null)
        {
            Debug.LogWarning("[PiecePreviewView] contentRoot or cellPrefab not set.");
            return;
        }

        // 1) 빈 데이터면 클리어
        if (shape == null || shape.Length == 0)
        {
            Clear();
            return;
        }

        // 2) 바운딩 박스 계산
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

        // 3) 셀 크기/간격으로 실제 픽셀 크기 계산
        float step = cellSize + cellSpacing;
        Vector2 contentSize = new Vector2(width * step - cellSpacing, height * step - cellSpacing);

        // 4) 오토 스케일 옵션: 큰 조각은 축소
        float scale = 1f;
        if (autoScaleToFit)
        {
            float sx = maxFrameSize.x / Mathf.Max(contentSize.x, 1f);
            float sy = maxFrameSize.y / Mathf.Max(contentSize.y, 1f);
            scale = Mathf.Min(1f, Mathf.Min(sx, sy));
        }

        // 5) 컨텐츠 중앙 정렬: (0,0)을 좌하단으로 배치한 뒤,
        //    contentRoot의 pivot이 (0.5,0.5)라고 가정하고 중앙으로 이동
        Vector2 pivotOffset = new Vector2(-contentSize.x * 0.5f, -contentSize.y * 0.5f);

        // 6) 풀 확보
        EnsurePool(shape.Length);

        // 7) 셀 배치
        _activeCount = 0;
        for (int i = 0; i < shape.Length; i++)
        {
            var local = shape[i];
            int nx = local.x - minX; // 정규화
            int ny = local.y - minY;

            float px = nx * step + pivotOffset.x;
            float py = ny * step + pivotOffset.y;

            var img = _pool[_activeCount++];
            var rt = (RectTransform)img.transform;

            img.color = cellColor;
            img.gameObject.SetActive(true);

            // 크기/위치 설정
            rt.SetParent(contentRoot, false);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = new Vector2(px, py);
        }

        // 8) 남은 풀 비활성화
        for (int i = _activeCount; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);

        // 9) 스케일 적용
        contentRoot.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// 셀 색상을 변경(테마/상태에 따라 동적으로).
    /// </summary>
    public void SetColor(Color c)
    {
        cellColor = c;
        for (int i = 0; i < _activeCount; i++)
            _pool[i].color = cellColor;
    }

    /// <summary>
    /// 프리뷰 초기화(모두 숨김).
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