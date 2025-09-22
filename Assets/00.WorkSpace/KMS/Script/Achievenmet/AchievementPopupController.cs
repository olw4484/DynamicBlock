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

        // 메달 아이콘
        int idx = def.ClampTierIndex(tier);
        if (def.tierSprites != null && def.tierSprites.Length > 0)
        {
            idx = Mathf.Clamp(idx, 0, def.tierSprites.Length - 1);
            medalIcon.sprite = def.tierSprites[idx];
        }

        // 날짜(로컬표시 권장)
        var local = utcNow.ToLocalTime();
        dateText.text = $"{local:yyyy.MM.dd}";

        // 설명 로컬라이즈 인자: 외부에서 전달 없으면 '획득한 티어의 임계값'으로 대체
        object[] fmtArgs =
            (args != null && args.Length > 0) ? args :
            (def.thresholds != null && def.thresholds.Length > 0
                ? new object[] { def.thresholds[Mathf.Clamp(tier - 1, 0, def.thresholds.Length - 1)] }
                : System.Array.Empty<object>());

        string desc = !string.IsNullOrEmpty(def.tableName)
            ? Loc.GetFrom(def.tableName, def.descKey, fmtArgs)
            : Loc.Get(def.descKey, fmtArgs);
        descText.text = desc;

        gameObject.SetActive(true);
    }
}
