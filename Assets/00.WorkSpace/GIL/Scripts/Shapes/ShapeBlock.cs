using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ShapeBlock : MonoBehaviour, IPointerClickHandler
{
    public int x, y;
    public Block parentBlock;

    public void OnPointerClick(PointerEventData eventData)
    {
        Image img = GetComponent<Image>();
        if (img != null && img.enabled)
        {
            Debug.Log($"활성 블록 클릭: ({x}, {y})");
            // TODO: 클릭 시 원하는 동작 구현
        }
    }
}