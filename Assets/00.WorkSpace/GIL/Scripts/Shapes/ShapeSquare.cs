using UnityEngine;
using UnityEngine.UI;

namespace _00.WorkSpace.GIL.Scripts.Shapes
{
    public class ShapeSquare : MonoBehaviour
    {
        public Image occupiedImage;

        void Start()
        {
            occupiedImage.gameObject.SetActive(false);
        }

        public void DeactivateSquare()
        {
            gameObject.GetComponent<BoxCollider2D>().enabled = false;
            gameObject.SetActive(false);
        }

        public void ActivateSquare()
        {
            gameObject.GetComponent<BoxCollider2D>().enabled = true;
            gameObject.SetActive(true);
        }
    }
}
