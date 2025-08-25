using System.Collections.Generic;
using System.Text;
using _00.WorkSpace.GIL.Scripts.Grids;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }
        
        private GridSquare[,] gridSquares; // 시각적 표현용
        private bool[,] gridStates;     
        public int rows = 8;
        public int cols = 8;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Q))
                PrintGridStates();
        }
        
        public void InitializeGridSquares(List<GridSquare> squareList, int rowCount, int colCount)
        {
            rows = rowCount;
            cols = colCount;
            gridSquares = new GridSquare[rows, cols];
            gridStates = new bool[rows, cols];

            foreach (var sq in squareList)
                gridSquares[sq.RowIndex, sq.ColIndex] = sq;
        }
        
        public void SetCellOccupied(int row, int col, bool occupied)
        {
            if (row < 0 || row >= rows || col < 0 || col >= cols) return;

            gridStates[row, col] = occupied;
            gridSquares[row, col].SetOccupied(occupied);
        }
        
        /// <summary>
        /// gridStates 출력 (X = 비어있음, 0 = 블럭 있음)
        /// </summary>
        public void PrintGridStates()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("==== gridStates ====\n");

            for (int r = 0; r < rows; r++)
            {
                sb.Append($"Line_{r} :\t");
                for (int c = 0; c < cols; c++)
                {
                    sb.Append(gridStates[r, c] ? "0 " : "X ");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }
        
        public bool CanPlaceShape(List<Transform> shapeBlocks)
        {
            var targetSquares = new List<GridSquare>();

            foreach (var block in shapeBlocks)
            {
                if (!block.gameObject.activeSelf) continue;

                Collider2D hit = Physics2D.OverlapPoint(block.position, LayerMask.GetMask("Grid"));
                if (hit == null) return false;

                GridSquare square = hit.GetComponent<GridSquare>();
                if (square.IsOccupied) return false;

                targetSquares.Add(square);
            }

            foreach (var square in targetSquares)
                SetCellOccupied(square.RowIndex, square.ColIndex, true);

            CheckForCompletedLines(); 
            return true;
        }
        
        public void CheckForCompletedLines()
        {
            if (gridSquares == null || gridStates == null) return;

            for (int r = 0; r < rows; r++)
            {
                bool complete = true;
                for (int c = 0; c < cols; c++)
                {
                    if (!gridStates[r, c])
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                {
                    AddScore();
                    ActiveClearEffectLine(r, true);

                    for (int c = 0; c < cols; c++)
                        SetCellOccupied(r, c, false);
                }
            }

            for (int c = 0; c < cols; c++)
            {
                bool complete = true;
                for (int r = 0; r < rows; r++)
                {
                    if (!gridStates[r, c])
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                {
                    AddScore();
                    

                    for (int r = 0; r < rows; r++)
                    {
                        SetCellOccupied(r, c, false);
                    }
                }
            }
        }

        private void AddScore()
        {
            ScoreManager.Instance.AddScore(100); // 점수 처리
        }

        private void ActiveClearEffectLine(int index, bool isRow)
        {
            // TODO: 나중에 이펙트 / 사운드 추가
        }
    }
}

