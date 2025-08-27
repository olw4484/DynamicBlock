using _00.WorkSpace.GIL.Scripts.Grids;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class GridManager : MonoBehaviour, IRuntimeReset
    {
        public static GridManager Instance { get; private set; }

        public GridSquare[,] gridSquares; // 시각적 표현용
        private bool[,] gridStates;
        public int rows = 8;
        public int cols = 8;

        private int _lineCount;
        public int LineCount { get; private set; }

        private EventQueue _bus;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log("GridManager: Awake");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
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

            _bus?.PublishSticky(new GridReady(rows, cols), alsoEnqueue: false);
        }
        public void SetCellOccupied(int row, int col, bool occupied, Sprite occupiedImage = null)

        public void SetCellOccupied(int row, int col, bool occupied)
        {
            if (row < 0 || row >= rows || col < 0 || col >= cols) return;

            gridStates[row, col] = occupied;
            
            if(occupiedImage != null) gridSquares[row, col].SetImage(occupiedImage);
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
                sb.Append($"Line_{r + 1} :\t");
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
            
            Sprite targetImage = shapeBlocks[0].gameObject.GetComponent<Image>().sprite;
            
            foreach (var square in targetSquares)
                SetCellOccupied(square.RowIndex, square.ColIndex, true, targetImage);

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

            _lineCount = completedRows.Count + completedCols.Count;

            if (_bus != null && _lineCount > 0)
                _bus.Publish(new LinesCleared(completedRows.Count, completedCols.Count));

            if (_lineCount == 0)
            {
                ScoreManager.Instance.comboCount = 0;
                return;
            }

            ScoreManager.Instance.comboCount += _lineCount;
            ScoreManager.Instance.CalculateLineClearScore(_lineCount);

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

            _lineCount = 0;
        }

        private void ActiveClearEffectLine(int index, bool isRow)
        {
            // TODO: 나중에 이펙트 / 사운드 추가
        }

        public void SetDependencies(EventQueue bus)
        {
            _bus = bus;
            // 소프트 리셋 구독
            _bus.Subscribe<GameResetRequest>(_ => ResetRuntime(), replaySticky: false);
        }

        public void ResetRuntime()
        {
            if (gridStates == null || gridSquares == null) return;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    gridStates[r, c] = false;
                    if (gridSquares[r, c] != null)
                        gridSquares[r, c].SetOccupied(false);
                }
            // ScoreManager.Instance.comboCount = 0;
            _bus.PublishSticky(new LinesCleared(0, 0), alsoEnqueue: false);
        }
    }
}
