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
    [SerializeField, Min(0f)] private float rowSpacingY = 160f;   // �� �� �� Y ����
    [SerializeField, Min(0f)] private float itemSpacingX = 250f;  // ���� ����(�� �� ���� ����)
    [SerializeField, Min(0)] private int trapezoidInset = 125;   // ���� �¿� �е�(��ٸ��� ����)
    [SerializeField] private bool enableTrapezoid = true;

    private readonly List<FruitBadge> pool = new();

    public void Show(FruitReq[] reqs)
    {
        // �ʼ� ���۷��� üũ(�� �� �ϳ��� ������ ����)
        if (!badgePrefab || topRow == null || bottomRow == null) return;

        EnsureRowComponents();

        // null-safe ���� ��� �� Ǯ Ȯ��
        int n = reqs?.Length ?? 0;
        EnsurePool(n);

        HideAll();

        if (n <= 0) { ApplyTrapezoid(0, 0); return; }

        int topCount = Mathf.Min(n, 3);
        int bottomCount = n - topCount;

        int idx = 0;
        // ���� ä���
        for (int i = 0; i < topCount; i++, idx++)
        {
            var v = pool[idx];
            v.transform.SetParent(topRow, false);
            v.gameObject.SetActive(true);
            v.Set(reqs[idx].sprite, reqs[idx].count, reqs[idx].achieved);
        }

        // �Ʒ��� ä���
        for (int i = 0; i < bottomCount; i++, idx++)
        {
            var v = pool[idx];
            v.transform.SetParent(bottomRow, false);
            v.gameObject.SetActive(true);
            v.Set(reqs[idx].sprite, reqs[idx].count, reqs[idx].achieved);
        }

        // �Ʒ��� on/off
        bottomRow.gameObject.SetActive(bottomCount > 0);

        // �� �� Y ������(��Ŀ Center ����)
        var topPos = topRow.anchoredPosition; topPos.y = 0f; topRow.anchoredPosition = topPos;
        var botPos = bottomRow.anchoredPosition; botPos.y = -rowSpacingY; bottomRow.anchoredPosition = botPos;

        // ��ٸ��� �е� �� ���� ����
        ApplyTrapezoid(topCount, bottomCount);

        // ��� ������
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

        // ���� ����: ��� ���� + ���� ��/����
        ConfigureHLG(topHLG);
        ConfigureHLG(botHLG);

        // spacing ����
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
            // ������ ������: ��/�� �е� �����ϰ� �ο�
            topHLG.padding = new RectOffset(trapezoidInset, trapezoidInset, topHLG.padding.top, topHLG.padding.bottom);
            // �Ʒ����� �а�
            botHLG.padding = new RectOffset(0, 0, botHLG.padding.top, botHLG.padding.bottom);
        }
        else
        {
            topHLG.padding = new RectOffset(0, 0, topHLG.padding.top, topHLG.padding.bottom);
            botHLG.padding = new RectOffset(0, 0, botHLG.padding.top, botHLG.padding.bottom);
        }
    }
}
