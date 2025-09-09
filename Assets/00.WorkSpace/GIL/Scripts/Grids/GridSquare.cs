using UnityEngine;
using UnityEngine.UI;

namespace _00.WorkSpace.GIL.Scripts.Grids
{
    public enum GridState {Normal, Hover, Active}

    public class GridSquare : MonoBehaviour
    {
        [HideInInspector] public int RowIndex;
        [HideInInspector] public int ColIndex;
        
        [Header("Image Objects")] 
        [SerializeField] private Image normalImage;
        [SerializeField] private Image hoverImage;
        [SerializeField] private Image activeImage;
        [SerializeField] private Image lineClearImage;
        [SerializeField] private Image fruitImage;
        
        [HideInInspector] public bool Selected { get; set; }
        [HideInInspector] public bool IsOccupied;
        [HideInInspector] public GridState state;
        
        public void SetState(GridState newState)
        {
            normalImage.gameObject.SetActive(newState == GridState.Normal);
            hoverImage.gameObject.SetActive(newState == GridState.Hover);
            activeImage.gameObject.SetActive(newState == GridState.Active);
            state = newState;
        }
        public void SetImage(Sprite sprite)
        {
            normalImage.sprite = sprite;
            hoverImage.sprite = sprite;
            activeImage.sprite = sprite;
        }

        public void SetLineClearImage(bool isActive, Sprite lineClearSprite = null)
        {
            lineClearImage.gameObject.SetActive(isActive);
            lineClearImage.sprite = lineClearSprite;
        }
        
        public void SetFruitImage(bool isActive, Sprite fruitSprite = null)
        {
            fruitImage.gameObject.SetActive(isActive);
            fruitImage.sprite = fruitSprite;
        }
        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            state = occupied ? GridState.Active : GridState.Normal;
            SetState(state);
        }
    }
}

