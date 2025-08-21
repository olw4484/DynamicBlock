using System;
using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private GameObject gridPrefab;
    [SerializeField] private int column = 8;
    [SerializeField] private int row = 8;
    
    [Header("Grid Layout")]
    [SerializeField] private Vector2 startPosition = Vector2.zero;
    [SerializeField] private Vector2 spacing = new Vector2(5f, 5f);
    
    [HideInInspector] public List<GameObject> gridSquares = new();

    private void OnEnable()
    {
        GameEvents.CheckIfShapeCanBePlaced = CheckIfShapeCanBePlaced;
    }
    private void OnDisable()
    {
        GameEvents.CheckIfShapeCanBePlaced = null;
    }
    
    public void CreateGrid()
    {
        ClearGrid();

        if (gridPrefab == null) return;

        RectTransform squareRect = gridPrefab.GetComponent<RectTransform>();
        Vector2 squareSize = squareRect.sizeDelta;

        for (int r = 0; r < row; r++)
        {
            for (int c = 0; c < column; c++)
            {
                float posX = startPosition.x + c * (squareSize.x + spacing.x);
                float posY = startPosition.y - r * (squareSize.y + spacing.y);

                GameObject newSquare = Instantiate(gridPrefab, transform);
                newSquare.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, posY);

                gridSquares.Add(newSquare);
            }
        }
    }
    
    public void ClearGrid()
    {
        for (int i = gridSquares.Count - 1; i >= 0; i--)
        {
            if (gridSquares[i] != null)
                DestroyImmediate(gridSquares[i]);
        }
        gridSquares.Clear();
    }
    
    public bool CheckIfShapeCanBePlaced(List<Transform> shapeBlocks)
    {
        List<GridSquare> targetSquares = new();

        foreach (var block in shapeBlocks)
        {
            if (!block.gameObject.activeSelf) continue;

            Collider2D hit = Physics2D.OverlapPoint(block.position, LayerMask.GetMask("Grid"));
            
            if (hit == null) 
                return false;
            
            GridSquare square = hit.GetComponent<GridSquare>();
            if (square.SquareOccupied)
                return false;

            targetSquares.Add(square);
        }
        foreach (var square in targetSquares)
            square.ActivateSquare();

        return true;
    }
}

