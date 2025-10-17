using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class FruitBadge : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image checkMark;

    public void Set(Sprite sprite, int count, bool achieved)
    {
        if (icon) icon.sprite = sprite;

        // �޼� ���¸� ���� ����
        if (countText)
        {
            countText.text = achieved ? string.Empty : count.ToString();
            countText.enabled = !achieved;
            // �Ǵ� countText.gameObject.SetActive(!achieved);
        }

        if (checkMark) checkMark.gameObject.SetActive(achieved);
    }
}
