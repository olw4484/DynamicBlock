using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public struct FruitReq
{
    public Sprite sprite;
    public int count;
    public bool achieved;
}

public sealed class FruitBadgeLayoutRows : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform topRow;
    [SerializeField] private RectTransform bottomRow;
    [SerializeField] private FruitBadge badgePrefab;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float rowSpacingY = 160f;   // 두 줄 간 Y 간격(캔버스 스케일러로 보정됨)
    [SerializeField, Min(0f)] private float itemSpacingX = 250f;  // 가로 간격
    [SerializeField, Range(0f, 0.45f)] private float trapezoidInsetPercent = 0.125f; // 윗줄 좌우 인셋(비율)
    [SerializeField] private bool enableTrapezoid = true;

    [Header("Row Offsets (center anchor 기준)")]
    [SerializeField] private float topRowYOffset = 100f;     // 위로 +100
    [SerializeField] private float bottomRowYOffset = -100f; // 아래로 -100

    private readonly List<FruitBadge> pool = new();

    public void Show(FruitReq[] reqs)
    {
        if (!badgePrefab || !topRow || !bottomRow) return;
        EnsureRowComponents();

        int n = reqs?.Length ?? 0;
        EnsurePool(n);
        HideAll();
        if (n <= 0) { ApplyTrapezoid(0, 0); return; }

        int topCount = Mathf.Min(n, 3);
        int bottomCount = n - topCount;

        int idx = 0;
        for (int i = 0; i < topCount; i++, idx++)
        {
            var v = pool[idx];
            v.transform.SetParent(topRow, false);
            v.gameObject.SetActive(true);
            v.Set(reqs[idx].sprite, reqs[idx].count, reqs[idx].achieved);
        }
        for (int i = 0; i < bottomCount; i++, idx++)
        {
            var v = pool[idx];
            v.transform.SetParent(bottomRow, false);
            v.gameObject.SetActive(true);
            v.Set(reqs[idx].sprite, reqs[idx].count, reqs[idx].achieved);
        }

        bottomRow.gameObject.SetActive(bottomCount > 0);

        var tp = topRow.anchoredPosition;
        tp.x = 0f;
        tp.y = (bottomCount > 0) ? topRowYOffset : 0f;
        topRow.anchoredPosition = tp;

        var bp = bottomRow.anchoredPosition;
        bp.x = 0f;
        bp.y = (bottomCount > 0) ? bottomRowYOffset : 0f;
        bottomRow.anchoredPosition = bp;

        ApplyTrapezoid(topCount, bottomCount);

        LayoutRebuilder.ForceRebuildLayoutImmediate(topRow);
        if (bottomCount > 0) LayoutRebuilder.ForceRebuildLayoutImmediate(bottomRow);
    }

    private void EnsurePool(int need)
    {
        while (pool.Count < need)
        {
            var v = Instantiate(badgePrefab);
            v.gameObject.SetActive(false);
            pool.Add(v);
        }
    }

    private void HideAll()
    {
        foreach (var v in pool) v.gameObject.SetActive(false);
    }

    private void EnsureRowComponents()
    {
        var topHLG = topRow.GetComponent<HorizontalLayoutGroup>() ?? topRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        var botHLG = bottomRow.GetComponent<HorizontalLayoutGroup>() ?? bottomRow.gameObject.AddComponent<HorizontalLayoutGroup>();

        ConfigureHLG(topHLG);
        ConfigureHLG(botHLG);

        topHLG.spacing = itemSpacingX;
        botHLG.spacing = itemSpacingX;

        SetRowRectDefaults(topRow);
        SetRowRectDefaults(bottomRow);
    }

    private static void ConfigureHLG(HorizontalLayoutGroup h)
    {
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 0, 0);
    }

    private static void SetRowRectDefaults(RectTransform rt)
    {
        // 가로 스트레치(부모 폭 비율), 중앙 Pivot
        rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
        rt.pivot = new Vector2(0.5f, rt.pivot.y);
        // 좌우 크기/위치는 앵커가 담당하므로 sizeDelta.x는 0 유지
        var sd = rt.sizeDelta;
        sd.x = 0f;
        rt.sizeDelta = sd;
    }

    private void ApplyTrapezoid(int topCount, int bottomCount)
    {
        if (enableTrapezoid && bottomCount > 0)
        {
            float inset = Mathf.Clamp01(trapezoidInsetPercent); // 0~0.45 권장
            // 윗줄만 좌우 인셋
            topRow.anchorMin = new Vector2(inset, topRow.anchorMin.y);
            topRow.anchorMax = new Vector2(1f - inset, topRow.anchorMax.y);
        }
        else
        {
            // 두 줄 모두 전체 폭 사용
            topRow.anchorMin = new Vector2(0f, topRow.anchorMin.y);
            topRow.anchorMax = new Vector2(1f, topRow.anchorMax.y);
        }

        // 아랫줄은 언제나 전체 폭 사용(사다리꼴 밑변)
        bottomRow.anchorMin = new Vector2(0f, bottomRow.anchorMin.y);
        bottomRow.anchorMax = new Vector2(1f, bottomRow.anchorMax.y);
    }

    private void OnRectTransformDimensionsChange()
    {
        // 현재 활성 상태 기준으로 다시 적용
        int topCount = 0, bottomCount = 0;
        foreach (Transform t in topRow) if (t.gameObject.activeSelf) topCount++;
        foreach (Transform t in bottomRow) if (t.gameObject.activeSelf) bottomCount++;

        ApplyTrapezoid(topCount, bottomCount);
        LayoutRebuilder.ForceRebuildLayoutImmediate(topRow);
        if (bottomCount > 0) LayoutRebuilder.ForceRebuildLayoutImmediate(bottomRow);
    }
}
