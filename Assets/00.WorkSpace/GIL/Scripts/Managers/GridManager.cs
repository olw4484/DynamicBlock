using System;
using System.Collections.Generic;
using System.Text;
using _00.WorkSpace.GIL.Scripts.Grids;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }
        
        public GridSquare[,] gridSquares; // 시각적 표현용
        private bool[,] gridStates;     
        public int rows = 8;
        public int cols = 8;
        
        [Header("Combo Debugger")]
        [SerializeField] private TMPro.TMP_Text comboText;

        private int _comboCount;
        
            
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnGUI()
        {
            comboText.text = $"Combo : {_comboCount.ToString()}";
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
        
        private void CheckForCompletedLines()
        {
            if (gridSquares == null || gridStates == null) return;
            
            List<int> completedCols = new();
            List<int> completedRows = new();
            
            for (int row = 0; row < rows; row++)
            {
                bool complete = true;
                for (int col = 0; col < cols; col++)
                {
                    if (!gridStates[row, col])
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                    completedRows.Add(row);
            }
            for (int col = 0; col < cols; col++)
            {
                bool complete = true;
                for (int row = 0; row < rows; row++)
                {
                    if (!gridStates[row, col])
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                    completedCols.Add(col);
            }
            
            int lineCount = completedRows.Count + completedCols.Count;

            if (lineCount == 0)
            {
                _comboCount = 0;
                return;
            }
            
            _comboCount+= lineCount;
            
            int gainedScore = CalculateLineClearScore(_comboCount - lineCount, lineCount);
            AddScore(gainedScore);
            
            foreach (int row in completedRows)
            {
                ActiveClearEffectLine(row, true);
                for (int col = 0; col < cols; col++)
                    SetCellOccupied(row, col, false);
            }

            foreach (int col in completedCols)
            {
                ActiveClearEffectLine(col, false);
                for (int row = 0; row < rows; row++)
                    SetCellOccupied(row, col, false);
            }
        }

        private int CalculateLineClearScore(int combo, int lineCount)
        {
            if (lineCount <= 0) return 0;

            int baseScore = (combo + 1) * 10;

            int factor = combo < 5 ? 2 :
                         combo < 10 ? 3 : 4;

            float multiplier;

            if (lineCount == 1)
            {
                multiplier = combo < 5f ? 1f :
                             combo < 10f ? 1.5f : 2;
            }
            else if (lineCount == 2)
            {
                multiplier = combo < 5 ? 2 :
                             combo < 10 ? 3 : 4;
            }
            else
            {
                multiplier = factor * ((lineCount - 2) * 3);
            }

            return (int)(baseScore * multiplier);
        }

        private void AddScore(int score)
        {
            ScoreManager.Instance.AddScore(score); // 점수 처리
        }

        private void ActiveClearEffectLine(int index, bool isRow)
        {
            // TODO: 나중에 이펙트 / 사운드 추가
        }
    }
}

