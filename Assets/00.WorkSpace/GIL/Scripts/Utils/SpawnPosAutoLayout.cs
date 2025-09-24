using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPosAutoLayout : MonoBehaviour
{
    [Header("References")]
    public RectTransform shapesPanel;          // 손패가 위치하는 패널(슬롯들의 부모)
    public List<RectTransform> slots;          // BlockSpawnPos1~3 의 RectTransform
    public RectTransform blockPrefab;          // 손패 블록 프리팹(크기 참고용)

    [Header("Layout")]
    [Tooltip("SafeArea 안쪽 좌우 여백(px)")]
    public float horizontalPadding = 10f;
    [Tooltip("슬롯 사이 최소 간격(px)")]
    public float minSpacing = 5f;
    [Tooltip("프리팹 크기 대신 수동값 사용 (0이면 프리팹크기 사용)")]
    public float overrideBlockSize = 0f;

    // 내부 상태
    readonly List<float> _initialY = new(); // 슬롯마다 원래 y를 기억해 유지
    Vector2 _lastPanelSize;
    Rect _lastSafeRect;

    void Reset()
    {
        shapesPanel = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        CacheInitialY();
        Recalc();
    }

#if UNITY_EDITOR
    void Update()
    {
        // 에디터에서도 사이즈 바뀌면 즉시 반영
        if (!Application.isPlaying) RecalcIfChanged();
    }
#endif

    void OnRectTransformDimensionsChange() => Recalc();

    void CacheInitialY()
    {
        _initialY.Clear();
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
            _initialY.Add(slots[i] ? slots[i].anchoredPosition.y : 0f);
    }

    public void RecalcIfChanged()
    {
        if (!shapesPanel) return;
        var safe = GetLocalSafeRect(shapesPanel);
        if (_lastPanelSize != shapesPanel.rect.size || _lastSafeRect != safe)
        {
            Recalc();
        }
    }

    public void Recalc()
    {
        if (!shapesPanel || slots == null || slots.Count == 0) return;

        var safe = GetLocalSafeRect(shapesPanel); // shapesPanel 로컬좌표의 SafeArea
        _lastPanelSize = shapesPanel.rect.size;
        _lastSafeRect = safe;

        // 블록 너비 계산 (UI px)
        float blockW = overrideBlockSize > 0f
            ? overrideBlockSize
            : (blockPrefab ? blockPrefab.rect.width : 100f); // 없으면 대략값

        // 사용 가능한 가로폭
        float usable = Mathf.Max(0f, safe.width - horizontalPadding * 2f);
        int n = 0; // 유효 슬롯 수 (null 제외)
        foreach (var s in slots) if (s) n++;

        if (n == 0) return;

        // 간격 계산: 최소 간격을 보장하면서 중앙 정렬
        float spacing = n > 1 ? Mathf.Max(minSpacing, (usable - n * blockW) / (n - 1)) : 0f;

        // 전체 폭이 usable보다 크면, spacing을 강제로 줄여 맞춘다
        float total = n * blockW + (n - 1) * spacing;
        if (total > usable && n > 1)
        {
            spacing = Mathf.Max(0f, (usable - n * blockW) / (n - 1));
            total = n * blockW + (n - 1) * spacing;
        }

        // 시작 x (로컬좌표) : SafeArea 왼쪽 + 패딩 + 중앙 보정 + 반블록
        float startX = safe.xMin + horizontalPadding + (usable - total) * 0.5f + blockW * 0.5f;

        // 슬롯 배치 (각 슬롯의 원래 y는 유지)
        int idx = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (!slot) continue;

            float x = startX + idx * (blockW + spacing);
            float y = (i < _initialY.Count) ? _initialY[i] : slot.anchoredPosition.y;

            slot.anchoredPosition = new Vector2(x, y);
            idx++;
        }
    }

    // Screen.safeArea를 shapesPanel 로컬좌표로 변환
    static Rect GetLocalSafeRect(RectTransform target)
    {
        var canvas = target.GetComponentInParent<Canvas>();
        var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        Rect sa = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(target, sa.position, cam, out var p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(target, sa.position + sa.size, cam, out var p1);
        return Rect.MinMaxRect(p0.x, p0.y, p1.x, p1.y);
    }
}
