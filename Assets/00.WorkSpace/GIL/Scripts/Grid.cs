using System.Collections.Generic;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts
{
    public class Grid : MonoBehaviour
    {
        [SerializeField] private int columns = 0;
        [SerializeField] private int rows = 0;

        [SerializeField] private float squaresGap = 0.1f;

        [SerializeField] private GameObject gridSquare;
        [SerializeField] private Vector2 startPosition = new Vector2(0f, 0f);
        [SerializeField] private float squareScale = 0.5f;
        [SerializeField] private float squareOffset = 0f;

        private Vector2 _offset = new Vector2(0f, 0f);

        private List<GameObject> _gridSquares = new();
        
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
    }
}
