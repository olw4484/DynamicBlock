using UnityEngine;
using UnityEngine.UI;

namespace _00.WorkSpace.GIL.Scripts.Grids
{
    public enum GridState {Normal, Hover, Active}

    public class GridSquare : MonoBehaviour
    {
        [Header("Image Objects")] 
        [SerializeField] private Image normalImage;
        [SerializeField] private Image hoverImage;
        [SerializeField] private Image activeImage;

        public bool Selected { get; set; }
        public bool SquareOccupied { get; private set; }

        private void Start()
        {
            Selected = false;
            SquareOccupied = false;
            SetState(GridState.Normal);
        }

        public void ActivateSquare()
        {
            SetState(GridState.Active);
            Selected = true;
            SquareOccupied = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Block") && !SquareOccupied)
            {
                SetState(GridState.Hover);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("Block") && !SquareOccupied)
            {
                SetState(GridState.Hover);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Block") && !SquareOccupied)
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
            SquareOccupied = occupied;
            SetState(occupied ? GridState.Active : GridState.Normal);
        }
    }
}

