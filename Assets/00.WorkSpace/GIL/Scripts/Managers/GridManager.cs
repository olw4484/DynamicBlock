using _00.WorkSpace.GIL.Scripts.Grids;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using LJJ;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class GridManager : MonoBehaviour, IRuntimeReset
    {
        public static GridManager Instance { get; private set; }

        public GridSquare[,] gridSquares; // 시각적 표현용
        public bool[,] gridStates;
        public int rows = 8;
        public int cols = 8;

        private int _lineCount;

        private int _gridMask;
        
        private List<GridSquare> _hoverSquares = new();
        private List<GridSquare> _hoverLineSquares = new();
        
        private EventQueue _bus;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _gridMask = LayerMask.GetMask("Grid");
            Debug.Log("GridManager: Awake");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
                PrintGridInfo();
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

                Collider2D hit = Physics2D.OverlapPoint(block.position, _gridMask);
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
            if (_hoverSquares.Count > 0)
            {
                foreach (var sq in _hoverSquares)
                    if (!sq.IsOccupied) sq.SetState(GridState.Normal);
                _hoverSquares.Clear();
            }

            if (_hoverLineSquares.Count > 0)
            {
                foreach (var sq in _hoverLineSquares)
                    sq.SetLineClearImage(false, null);   // 오버레이 OFF
                _hoverLineSquares.Clear();
            }
        }

        public void UpdateHoverPreview(List<Transform> shapeBlocks)
        {
            // 이전 프리뷰 싹 정리
            ClearHoverPreview();
            Game.Fx.StopAllLoop();

            if (!TryGetPlacement(shapeBlocks, out var squares))
                return;

            // 드래그 중 블록 스프라이트
            var sprite = shapeBlocks[0].GetComponent<UnityEngine.UI.Image>()?.sprite;

            // 놓일 칸 프리뷰(기존 기능 유지)
            foreach (var sq in squares)
            {
                if (sprite != null) sq.SetImage(sprite);   // 빈칸에만 적용(TryGetPlacement에서 Occupied 제외됨)
                if (!sq.IsOccupied) sq.SetState(GridState.Hover);
            }
            _hoverSquares.AddRange(squares);

            // 가상 배치 시 완성될 라인들을 오버레이로 표시
            if (PredictCompletedLines(squares, out var rowsCompleted, out var colsCompleted))
                ShowLineFollowOverlay(rowsCompleted, colsCompleted, sprite);
        }

        public void InitializeGridSquares(List<GridSquare> squareList, int rowCount, int colCount)
        {
            rows = rowCount; cols = colCount;
            gridSquares = new GridSquare[rows, cols];
            gridStates = new bool[rows, cols];

            foreach (var sq in squareList)
                gridSquares[sq.RowIndex, sq.ColIndex] = sq;

            _bus?.PublishSticky(new GridReady(rows, cols), alsoEnqueue: false);
            _bus?.PublishImmediate(new GridReady(rows, cols));
        }
        public void SetCellOccupied(int row, int col, bool occupied, Sprite occupiedImage = null)
        {
            if (row < 0 || row >= rows || col < 0 || col >= cols) return;

            gridStates[row, col] = occupied;

            if (occupiedImage != null) gridSquares[row, col].SetImage(occupiedImage);
            gridSquares[row, col].SetOccupied(occupied);
        }

        private void PrintGridInfo()
        {
            PrintGridSquares();
            PrintGridStates();
            PrintGridOccupiedInfo();
        }
        
        /// <summary>
        /// gridStates 출력 (X = 비어있음, 0 = 블럭 있음)
        /// </summary>
        public void PrintGridStates()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("==== Grid States ====\n");

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
        
        public void PrintGridSquares()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("==== Grid Squares ====\n");

            for (int r = 0; r < rows; r++)
            {
                sb.Append($"Line_{r + 1} :\t");
                for (int c = 0; c < cols; c++)
                {
                    sb.Append($"{gridSquares[r, c].state}\t");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }

        public void PrintGridOccupiedInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("==== Grid Occupied Info ====\n");

            for (int r = 0; r < rows; r++)
            {
                sb.Append($"Line_{r + 1} :\t");
                for (int c = 0; c < cols; c++)
                {
                    sb.Append($"{gridSquares[r, c].IsOccupied}\t");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }
        
        public bool CanPlaceShape(List<Transform> shapeBlocks)
        {
            ClearHoverPreview();
            // 블록 이미지/점유 처리
            Sprite targetImage = shapeBlocks[0].gameObject.GetComponent<Image>().sprite;

            if (!TryGetPlacement(shapeBlocks, out var targetSquares))
                return false;

            foreach (var square in targetSquares)
                SetCellOccupied(square.RowIndex, square.ColIndex, true, targetImage);

            // 이번에 배치한 블록 칸 수(블록 자체 점수로 사용)
            int blockUnits = targetSquares.Count;

            CheckForCompletedLines(blockUnits);
            return true;
        }

        private void CheckForCompletedLines(int blockUnits)
        {
            if (gridSquares == null || gridStates == null) return;

            List<int> completedCols = new();
            List<int> completedRows = new();

            // 가로 체크
            for (int row = 0; row < rows; row++)
            {
                bool complete = true;
                for (int col = 0; col < cols; col++)
                    if (!gridStates[row, col]) { complete = false; break; }
                if (complete) completedRows.Add(row);
            }

            // 세로 체크
            for (int col = 0; col < cols; col++)
            {
                bool complete = true;
                for (int row = 0; row < rows; row++)
                    if (!gridStates[row, col]) { complete = false; break; }
                if (complete) completedCols.Add(col);
            }

            _lineCount = completedRows.Count + completedCols.Count;

            if (_bus != null && _lineCount > 0)
                _bus.Publish(new LinesCleared(completedRows.Count, completedCols.Count));

            // 여기서 콤보 조작/점수 가산을 직접 하지 말고 ScoreManager에 위임
            ScoreManager.Instance.ApplyMoveScore(blockUnits, _lineCount);

            // 라인 클리어 반영(이펙트/비우기)
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

            if (IsBoardEmpty())
            {
                _bus?.Publish(new AllClear());
            }

            _lineCount = 0;
        }

        private void ActiveClearEffectLine(int index, bool isRow)
        {
            if (Game.Fx == null) return;              
            var color = Color.white;              

            if (isRow) Game.Fx.PlayRow(index, color);
            else Game.Fx.PlayCol(index, color);
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

        private bool IsBoardEmpty()
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (gridStates[r, c]) return false;  // 하나라도 true(점유)면 비지 않음
            return true;
        }
        
        private bool PredictCompletedLines(
            List<GridSquare> addedSquares, 
            out List<int> rowsCompleted, 
            out List<int> colsCompleted)
        {
            rowsCompleted = new();
            colsCompleted = new();
            if (gridSquares == null || gridStates == null) return false;

            // 빠른 포함 체크용
            var added = new HashSet<(int r, int c)>();
            foreach (var sq in addedSquares) added.Add((sq.RowIndex, sq.ColIndex));

            // 가로 라인
            for (int r = 0; r < rows; r++)
            {
                bool complete = true;
                for (int c = 0; c < cols; c++)
                {
                    if (!gridStates[r, c] && !added.Contains((r, c)))
                    {
                        complete = false;
                        break;
                    }
                }
                if (complete) rowsCompleted.Add(r);
            }

            // 세로 라인
            for (int c = 0; c < cols; c++)
            {
                bool complete = true;
                for (int r = 0; r < rows; r++)
                {
                    if (!gridStates[r, c] && !added.Contains((r, c)))
                    {
                        complete = false;
                        break;
                    }
                }
                if (complete) colsCompleted.Add(c);
            }

            return (rowsCompleted.Count + colsCompleted.Count) > 0;
        }
        
        private void ShowLineFollowOverlay(List<int> rowsCompleted, List<int> colsCompleted, Sprite sprite)
        {
            if (sprite == null) return;

            var seen = new HashSet<GridSquare>();

            // 가로 라인
            foreach (int r in rowsCompleted)
            {
                for (int c = 0; c < cols; c++)
                {
                    var sq = gridSquares[r, c];
                    if (sq == null) continue;
                    if (seen.Add(sq))
                    {
                        sq.SetLineClearImage(true, sprite);
                        _hoverLineSquares.Add(sq);
                    }
                }
            }

            // 세로 라인
            foreach (int c in colsCompleted)
            {
                for (int r = 0; r < rows; r++)
                {
                    var sq = gridSquares[r, c];
                    if (sq == null) continue;
                    if (seen.Add(sq))
                    {
                        sq.SetLineClearImage(true, sprite);
                        _hoverLineSquares.Add(sq);
                    }
                }
            }
        }
    }
}
