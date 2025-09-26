using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AchievementPopupController : MonoBehaviour
{
    [SerializeField] private Image medalIcon;
    [SerializeField] private TMP_Text dateText, descText;

    public void Show(AchievementDefinition def, int tier, DateTime utcNow, params object[] args)
    {
        if (!def || !medalIcon || !dateText || !descText) return;

        // 1) 아이콘
        int idx = def.ClampTierIndex(tier);
        if (def.tierSprites != null && def.tierSprites.Length > 0)
            medalIcon.sprite = def.tierSprites[Mathf.Clamp(idx, 0, def.tierSprites.Length - 1)];

        // 2) 날짜 (세이브에 기록된 달성일자 우선)
        var data = Game.Save?.Data;

        DateTime toShowUtc = utcNow;

        if (data != null && data.TryGetAchievementUnlockUtc((int)def.id, tier, out var storedUtc))
            toShowUtc = storedUtc;

        dateText.text = toShowUtc.ToLocalTime().ToString("yyyy.MM.dd");

        // 3) 설명 포맷
        object[] fmtArgs =
            (args != null && args.Length > 0) ? args :
            (def.thresholds != null && def.thresholds.Length > 0
                ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] }
                : Array.Empty<object>());

        int tierToUse = Mathf.Max(1, tier);
        string key = def.GetDescKeyForTier(tierToUse);
        descText.text = Loc.Get(key, fmtArgs);

        gameObject.SetActive(true);
    }
}
