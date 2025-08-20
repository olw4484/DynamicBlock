using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GridSquare : MonoBehaviour
{
    [SerializeField] private Image normalImage;
    [SerializeField] private Image hoverImage;
    [SerializeField] private Image activeImage;
    [SerializeField] private List<Sprite> normalImages;
    
    public bool Selected { get; set; }
    public int SquareIndex { get; set; }
    public bool SquareOccupied { get; set; }

    private void Start()
    {
        Selected = false;
        SquareOccupied = false;
    }

    public void PlaceShapeOnBoard()
    {
        ActivateSquare();
    }
    
    public bool CanWeUseThisSquare()
    {
        return hoverImage.gameObject.activeSelf;
    }

    public void ActivateSquare()
    {
        hoverImage.gameObject.SetActive(false);
        activeImage.gameObject.SetActive(true);
        Selected = true;
        SquareOccupied = true;
    }
    
    public void SetImage(bool setFirstImage)
    {
        normalImage.GetComponent<Image>().sprite = setFirstImage ? normalImages[1] : normalImages[0] ;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (SquareOccupied == false)
        {
            Selected = true;
            hoverImage.gameObject.SetActive(true);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (SquareOccupied == false)
        {
            hoverImage.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (SquareOccupied == false)
        {
            Selected = false;
            hoverImage.gameObject.SetActive(false);
        }
    }
}
