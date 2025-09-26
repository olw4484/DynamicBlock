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
    [SerializeField, Min(0f)] private float rowSpacingY = 160f;   // 두 줄 간 Y 간격
    [SerializeField, Min(0f)] private float itemSpacingX = 250f;  // 가로 간격(두 줄 동일 권장)
    [SerializeField, Min(0)] private int trapezoidInset = 125;   // 윗줄 좌우 패딩(사다리꼴 강도)
    [SerializeField] private bool enableTrapezoid = true;

    private readonly List<FruitBadge> pool = new();

    public void Show(FruitReq[] reqs)
    {
        // 필수 레퍼런스 체크(둘 중 하나라도 없으면 리턴)
        if (!badgePrefab || topRow == null || bottomRow == null) return;

        EnsureRowComponents();

        // null-safe 개수 계산 → 풀 확보
        int n = reqs?.Length ?? 0;
        EnsurePool(n);

        HideAll();

        if (n <= 0) { ApplyTrapezoid(0, 0); return; }

        int topCount = Mathf.Min(n, 3);
        int bottomCount = n - topCount;

        int idx = 0;
        // 윗줄 채우기
        for (int i = 0; i < topCount; i++, idx++)
        {
            var v = pool[idx];
            v.transform.SetParent(topRow, false);
            v.gameObject.SetActive(true);
            v.Set(reqs[idx].sprite, reqs[idx].count, reqs[idx].achieved);
        }

        // 아랫줄 채우기
        for (int i = 0; i < bottomCount; i++, idx++)
        {
            var v = pool[idx];
            v.transform.SetParent(bottomRow, false);
            v.gameObject.SetActive(true);
            v.Set(reqs[idx].sprite, reqs[idx].count, reqs[idx].achieved);
        }

        // 아랫줄 on/off
        bottomRow.gameObject.SetActive(bottomCount > 0);

        // 줄 간 Y 오프셋(앵커 Center 기준)
        var topPos = topRow.anchoredPosition; topPos.y = 0f; topRow.anchoredPosition = topPos;
        var botPos = bottomRow.anchoredPosition; botPos.y = -rowSpacingY; bottomRow.anchoredPosition = botPos;

        // 사다리꼴 패딩 및 간격 적용
        ApplyTrapezoid(topCount, bottomCount);

        // 즉시 리빌드
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

        // 공통 설정: 가운데 정렬 + 고정 폭/높이
        ConfigureHLG(topHLG);
        ConfigureHLG(botHLG);

        // spacing 적용
        topHLG.spacing = itemSpacingX;
        botHLG.spacing = itemSpacingX;
    }

    private static void ConfigureHLG(HorizontalLayoutGroup h)
    {
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
    }

    private void ApplyTrapezoid(int topCount, int bottomCount)
    {
        var topHLG = topRow.GetComponent<HorizontalLayoutGroup>();
        var botHLG = bottomRow.GetComponent<HorizontalLayoutGroup>();
        if (!topHLG || !botHLG) return;

        if (enableTrapezoid && bottomCount > 0)
        {
            // 윗변을 좁히기: 좌/우 패딩 동일하게 부여
            topHLG.padding = new RectOffset(trapezoidInset, trapezoidInset, topHLG.padding.top, topHLG.padding.bottom);
            // 아랫변은 넓게
            botHLG.padding = new RectOffset(0, 0, botHLG.padding.top, botHLG.padding.bottom);
        }
        else
        {
            topHLG.padding = new RectOffset(0, 0, topHLG.padding.top, topHLG.padding.bottom);
            botHLG.padding = new RectOffset(0, 0, botHLG.padding.top, botHLG.padding.bottom);
        }
    }
}
