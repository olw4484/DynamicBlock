using UnityEngine;
using UnityEngine.UI;


public enum GridState {Normal, Hover, Active}

public class GridSquare : MonoBehaviour
{
    [Header("Image Objects")]
    [SerializeField] private Image normalImage;
    [SerializeField] private Image hoverImage;
    [SerializeField] private Image activeImage;
    
    private void Awake()
    {
        SetState(GridState.Normal); // 초기 상태
    }

    public void SetState(GridState newState)
    {
        normalImage.gameObject.SetActive(newState == GridState.Normal);
        hoverImage.gameObject.SetActive(newState == GridState.Hover);
        activeImage.gameObject.SetActive(newState == GridState.Active);
    }
}
