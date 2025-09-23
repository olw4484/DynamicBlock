using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AchievementPopupController : MonoBehaviour
{
    [SerializeField] private Image medalIcon;
    [SerializeField] private TMP_Text dateText, descText;

    public void Show(AchievementDefinition def, int tier, System.DateTime utcNow, params object[] args)
    {
        if (!def || !medalIcon || !dateText || !descText) return;

        // 아이콘
        int idx = def.ClampTierIndex(tier);
        if (def.tierSprites != null && def.tierSprites.Length > 0)
            medalIcon.sprite = def.tierSprites[Mathf.Clamp(idx, 0, def.tierSprites.Length - 1)];

        // 날짜
        dateText.text = utcNow.ToLocalTime().ToString("yyyy.MM.dd");

        // 포맷 인자(없으면 해당 티어 임계값)
        object[] fmtArgs =
            (args != null && args.Length > 0) ? args :
            (def.thresholds != null && def.thresholds.Length > 0
                ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] }
                : System.Array.Empty<object>());

        int tierToUse = Mathf.Max(1, tier);
        string key = def.GetDescKeyForTier(tierToUse);
        string desc = Loc.Get(key, fmtArgs);
        descText.text = desc;

        gameObject.SetActive(true);
    }

}
