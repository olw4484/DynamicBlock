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

        private List<GridSquare> _hoverSquares = new();
        
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

        void OnEnable() { StartCoroutine(GameBindingUtil.WaitAndRun(() => TryBindBus())); }
        void Start() { TryBindBus(); } // 중복 호출 안전
        
        public bool TryGetPlacement(List<Transform> shapeBlocks, out List<GridSquare> targetSquares)
        {
            targetSquares = new List<GridSquare>();
            var seen = new HashSet<GridSquare>();
            int activeCount = 0;

            foreach (var block in shapeBlocks)
            {
                if (!block.gameObject.activeSelf) continue;
                activeCount++;

                var hit = Physics2D.OverlapPoint(block.position, LayerMask.GetMask("Grid"));
                if (hit == null) { targetSquares.Clear(); return false; }

                var square = hit.GetComponent<GridSquare>();
                if (square == null || square.IsOccupied) { targetSquares.Clear(); return false; }

                // 동일 칸에 두 블럭이 겹쳐 찍히는 비정상 상황 방지(좌표 오프셋 문제 등)
                if (!seen.Add(square)) { targetSquares.Clear(); return false; }
            }

            // 활성 블럭이 하나도 없으면 미리보기 의미가 없으니 false 처리(원하면 true로 바꿔도 됨)
            if (activeCount == 0) return false;

            targetSquares.AddRange(seen);
            return true;
        }
        
        public void ClearHoverPreview()
        {
            if (_hoverSquares.Count == 0) return;
            foreach (var sq in _hoverSquares)
            {
                // 이미 Active(배치 완료)인 칸은 건드리지 않음
                if (!sq.IsOccupied) sq.SetState(GridState.Normal);
            }
            _hoverSquares.Clear();
        }

        public void UpdateHoverPreview(List<Transform> shapeBlocks)
        {
            if (TryGetPlacement(shapeBlocks, out var squares))
            {
                // 드래그 중 보여줄 스프라이트(모양) 맞춰주기
                var sprite = shapeBlocks[0].GetComponent<UnityEngine.UI.Image>()?.sprite;
                ClearHoverPreview();

                foreach (var sq in squares)
                {
                    if (sprite != null) sq.SetImage(sprite);
                    if (!sq.IsOccupied) sq.SetState(GridState.Hover);
                }
                _hoverSquares.AddRange(squares);
            }
            else
            {
                ClearHoverPreview();
            }
        }
        
        public void InitializeGridSquares(List<GridSquare> squareList, int rowCount, int colCount)
        {
            rows = rowCount;
            cols = colCount;
            gridSquares = new GridSquare[rows, cols];
            gridStates = new bool[rows, cols];

            foreach (var sq in squareList)
                gridSquares[sq.RowIndex, sq.ColIndex] = sq;

            // 스티키로 상태 저장
            _bus?.PublishSticky(new GridReady(rows, cols), alsoEnqueue: false);

            // 즉시 한 번도 쏘기 — 단, BlockStorage에 디듀프 가드가 있어야 중복 생성되지 않음
            _bus?.PublishImmediate(new GridReady(rows, cols));
        }
        public void SetCellOccupied(int row, int col, bool occupied, Sprite occupiedImage = null)
        {
            if (row < 0 || row >= rows || col < 0 || col >= cols) return;

            gridStates[row, col] = occupied;

            if (occupiedImage != null) gridSquares[row, col].SetImage(occupiedImage);
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
            ClearHoverPreview();

            if (!TryGetPlacement(shapeBlocks, out var targetSquares))
                return false;

            var targetImage = shapeBlocks[0].GetComponent<UnityEngine.UI.Image>()?.sprite;
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
            Debug.Log($"[Grid] Bind bus={_bus.GetHashCode()}");
            _bus.Subscribe<GameResetRequest>(_ => {
                Debug.Log("[Grid] ResetRuntime received");
                ResetRuntime();
            }, replaySticky: false);
        }

        public void ResetRuntime()
        {
            if (gridStates == null || gridSquares == null) return;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    gridStates[r, c] = false;
                    gridSquares[r, c]?.SetOccupied(false);
                }

            _bus.PublishImmediate(new ComboChanged(0));
            _bus.PublishImmediate(new ScoreChanged(0));

            Debug.Log("[Grid] PublishImmediate(GridReady)");
            _bus.PublishImmediate(new GridReady(rows, cols));
        }
        private void TryBindBus()
        {
            if (_bus != null || !Game.IsBound) return;
            SetDependencies(Game.Bus); // 실제 DI
        }
    }
}
