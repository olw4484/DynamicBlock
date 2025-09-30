using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class MedalItemView : MonoBehaviour
{
    [SerializeField] private AchievementDefinition def;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    // ↓ 바인더 참조(인스펙터에서 넣거나 못 넣었으면 Find)
    [SerializeField] private AchievementUIBinder binder;
    [SerializeField] private Button button;

    public AchievementDefinition Definition => def;

    void Awake()
    {
        if (!binder) binder = FindObjectOfType<AchievementUIBinder>(true);
        if (button)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
        if (label) label.raycastTarget = false; // 클릭 방해 방지
    }
    void OnDestroy() { if (button) button.onClick.RemoveListener(OnClick); }

    public void OnClick()
    {
        if (binder && def) binder.ShowPopupFor(def);
    }

    public void Refresh(AchievementProgress p)
    {
        if (!def || !icon || !label) return;

        label.text = def.GetTitle();

        var sprites = def.tierSprites;
        if (p.tier <= 0)
        {
            if (sprites != null && sprites.Length > 0) icon.sprite = sprites[0];
            icon.color = def.dimWhenLocked ? new Color(1, 1, 1, 0.35f) : Color.white;
            return;
        }

        int idx = def.ClampTierIndex(p.tier);
        if (sprites != null && sprites.Length > 0)
        {
            idx = Mathf.Clamp(idx, 0, sprites.Length - 1);
            icon.sprite = sprites[idx];
        }
        icon.color = Color.white;
    }
}
