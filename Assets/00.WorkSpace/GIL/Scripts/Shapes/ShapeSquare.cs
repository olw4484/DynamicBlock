using UnityEngine;
using UnityEngine.UI;


public class ShapeSquare : MonoBehaviour
{
    [SerializeField] private Image occupiedImage;

    void Start()
    {
        occupiedImage.gameObject.SetActive(false);
    }
}

