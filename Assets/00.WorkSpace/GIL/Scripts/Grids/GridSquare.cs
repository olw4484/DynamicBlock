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
        
        public bool Selected { get; set; }
        public bool IsOccupied { get; private set; }

        private void Start()
        {
            Selected = false;
            IsOccupied = false;
            SetState(GridState.Normal);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("Block") && !IsOccupied)
            {
                var myBounds = GetComponent<BoxCollider2D>().bounds;
                var otherBounds = other.bounds;

                if (myBounds.Contains(otherBounds.min) && myBounds.Contains(otherBounds.max))
                {
                    SetState(GridState.Hover);
                }
                else
                {
                    SetState(GridState.Normal);
                }
            }
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Block") && !IsOccupied)
            {
                SetState(GridState.Normal);
            }     
        }

        public void SetState(GridState newState)
        {
            normalImage.gameObject.SetActive(newState == GridState.Normal);
            hoverImage.gameObject.SetActive(newState == GridState.Hover);
            activeImage.gameObject.SetActive(newState == GridState.Active);
        }
    
        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            SetState(occupied ? GridState.Active : GridState.Normal);
        }
    }
}

