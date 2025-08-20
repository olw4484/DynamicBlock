using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    [SerializeField] ShapeStorage shapeStorage;
    [SerializeField] private int columns = 8;
    [SerializeField] private int rows = 8;

    [SerializeField] private float squaresGap = 2f;

    [SerializeField] private GameObject gridSquare;
    [SerializeField] private Vector2 startPosition = new Vector2(0f, 0f);
    [SerializeField] private float squareScale = 1f;
    [SerializeField] private float squareOffset = 0.75f;
    
    private Vector2 _offset = new Vector2(0f, 0f);
    private List<GameObject> _gridSquares = new();

    void OnEnable()
    {
        GameEvent.CheckIfShapeCanBePlaced += CheckIfShapeCanBePlaced;
    }
    
    void OnDisable()
    {
        GameEvent.CheckIfShapeCanBePlaced -= CheckIfShapeCanBePlaced;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        CreateGrid();
    }

    private void CreateGrid()
    {
        SpawnGridSquares();
        SetGridSquares();
    }
    
    private void SpawnGridSquares()
    {
        // 0, 1, 2, 3, 4
        // 5, 6, 7, 8 ,9

        int squareIndex = 0;

        for (var row = 0; row < rows; ++row)
        {
            for (var col = 0; col < columns; ++col)
            {
                _gridSquares.Add(Instantiate(gridSquare)); 
                
                _gridSquares[_gridSquares.Count - 1].GetComponent<GridSquare>().SquareIndex = squareIndex;
                _gridSquares[_gridSquares.Count - 1].transform.SetParent(this.transform);
                _gridSquares[_gridSquares.Count - 1].transform.localScale = new Vector3(squareScale, squareScale, squareScale);
                _gridSquares[_gridSquares.Count - 1].GetComponent<GridSquare>().SetImage(squareIndex % 2 == 0);
                squareIndex++;
            }
        }
    }
    
    private void SetGridSquares()
    {
        int columnNumber = 0;
        int rowNumber = 0;
        Vector2 squareGapNumber = new Vector2(0f, 0f);
        bool rowMoved = false;
        
        var squareRect = _gridSquares[0].GetComponent<RectTransform>();
        
        _offset.x = squareRect.rect.width * squareRect.transform.localScale.x + squareOffset;
        _offset.y = squareRect.rect.height * squareRect.transform.localScale.y + squareOffset;

        foreach (GameObject square in _gridSquares)
        {
            if (columnNumber + 1 > columns)
            {
                squareGapNumber.x = 0;
                columnNumber = 0;
                rowNumber++;
                rowMoved = false;
            }

            var posXOffset = _offset.x * columnNumber + (squareGapNumber.x * squaresGap);
            var posYOffset = _offset.y * rowNumber +  (squareGapNumber.y * squaresGap);

            if (columnNumber > 0 && columnNumber % 3 == 0)
            {
                squareGapNumber.x++;
                posXOffset += squaresGap;
            }

            if (rowNumber > 0 && rowNumber % 3 == 0 && rowMoved == false)
            {
                rowMoved = true;
                squareGapNumber.y++;
                posYOffset += squaresGap;
            }

            square.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                startPosition.x + posXOffset, 
                startPosition.y - posYOffset);

            square.GetComponent<RectTransform>().localPosition = new Vector3(
                startPosition.x + posXOffset,
                startPosition.y - posYOffset,
                0f);

            columnNumber++;
        }
    }
    
    private void CheckIfShapeCanBePlaced()
    {
        var squareIndexes = new List<int>();        
        
        foreach (var square in _gridSquares)
        {
            var gridSquare = square.GetComponent<GridSquare>();

            if (gridSquare.Selected && !gridSquare.SquareOccupied)
            {
                squareIndexes.Add(gridSquare.SquareIndex);
                gridSquare.Selected = false;
            }
        }

        var currentSelectedShape = shapeStorage.GetCurrentSelectedShape();
        if (currentSelectedShape == null) return;

        if (currentSelectedShape.TotalSquareNumber == squareIndexes.Count)
        {
            foreach (var squareIndex in squareIndexes)
            {
                _gridSquares[squareIndex].GetComponent<GridSquare>().PlaceShapeOnBoard();
            }
            currentSelectedShape.DeactivateShape();
        }
        else
        {
            GameEvent.MoveShapeToStartPosition();
        }
    }
}

