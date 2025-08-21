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
    
    [HideInInspector]
    public List<GameObject> gridSquares = new();

    private void OnEnable()
    {
        GameEvents.CheckIfShapeCanBePlaced += CheckIfShapeCanBePlaced;
    }
    private void OnDisable()
    {
        GameEvents.CheckIfShapeCanBePlaced -= CheckIfShapeCanBePlaced;
    }
    
    private void CheckIfShapeCanBePlaced()
    {
        foreach (var square in gridSquares)
        {
            var gridSquare = square.GetComponent<GridSquare>();

            if (gridSquare.CanWeUseThisSquare() == true)
            {
                gridSquare.ActivateSquare();
            }
        }
    }
    public void CreateGrid()
    {
        // 기존 Grid 제거
        for (int i = gridSquares.Count - 1; i >= 0; i--)
        {
            if (gridSquares[i] != null)
                DestroyImmediate(gridSquares[i]);
        }
        gridSquares.Clear();

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
}

