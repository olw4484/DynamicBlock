using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GridSquare : MonoBehaviour
{
    [SerializeField] private Image normalImage;
    [SerializeField] private List<Sprite> normalImages;

    public void SetImage(bool setFirstImage)
    {
        normalImage.GetComponent<Image>().sprite = setFirstImage ? normalImages[1] : normalImages[0] ;
    }
}
