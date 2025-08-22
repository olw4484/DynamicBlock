using System;
using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private GameObject gridSquare;
    [SerializeField] private int columns = 8;
    [SerializeField] private int rows = 8;
    
    [Header("Grid Layout")]
    [SerializeField] private Vector2 startPosition = Vector2.zero;
    [SerializeField] private Vector2 spacing = new Vector2(5f, 5f);
    
    public List<GridSquare> _gridSquares = new();

    private void OnEnable()
    {
        GameEvents.CheckIfShapeCanBePlaced = CheckIfShapeCanBePlaced;
    }
    private void OnDisable()
    {
        GameEvents.CheckIfShapeCanBePlaced = null;
    }
    
    private void Awake()
    {
        if(gridSquare == null)
            CreateGrid();
    }
    
    public void CreateGrid()
    {
        ClearGrid();
        if (gridSquare == null) return;
        
        RectTransform squareRect = gridSquare.GetComponent<RectTransform>();
        Vector2 squareSize = squareRect.sizeDelta;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                float posX = startPosition.x + column * (squareSize.x + spacing.x);
                float posY = startPosition.y - row * (squareSize.y + spacing.y);

                GameObject newSquare = Instantiate(gridSquare, transform);
                newSquare.name = $"Square_{row}_{column}";
                newSquare.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, posY);
                
                _gridSquares.Add(newSquare.GetComponent<GridSquare>());
            }
        }
    }
    
    public void ClearGrid()
    {
        for (int i = _gridSquares.Count - 1; i >= 0; i--)
        {
            if (_gridSquares[i] != null)
                DestroyImmediate(_gridSquares[i].gameObject);
        }
        _gridSquares.Clear();
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
        
        CheckForCompletedLines();
        return true;
    }
    
    public void CheckForCompletedLines()
    {
        if (_gridSquares == null || _gridSquares.Count == 0) return;
        
        // 가로줄 검사
        for (int row = 0; row < rows; row++)
        {
            bool rowComplete = true;
            for (int column = 0; column < columns; column++)
            {
                GridSquare square = _gridSquares[row * columns + column];
                if (!square.SquareOccupied)
                {
                    rowComplete = false;
                    break;
                }
            }

            if (rowComplete)
            {
                ActiveClearEffectLine(row, true); // 가로줄 이펙트 실행

                for (int column = 0; column < columns; column++)
                {
                    GridSquare square = _gridSquares[row * columns + column];
                    square.SetOccupied(false);
                    square.SetState(GridState.Normal);
                }
            }
        }

        // 세로줄 검사
        for (int column = 0; column < columns; column++)
        {
            bool colComplete = true;
            for (int row = 0; row < rows; row++)
            {
                GridSquare square = _gridSquares[row * columns + column];
                if (!square.SquareOccupied)
                {
                    colComplete = false;
                    break;
                }
            }

            if (colComplete)
            {
                ActiveClearEffectLine(column, false); // 세로줄 이펙트 실행

                for (int row = 0; row < rows; row++)
                {
                    GridSquare square = _gridSquares[row * columns + column];
                    square.SetOccupied(false);
                    square.SetState(GridState.Normal);
                }
            }
        }
    }

    private void ActiveClearEffectLine(int index, bool isRow)
    {
        // TODO: 나중에 이펙트 / 사운드 추가
    }
}

