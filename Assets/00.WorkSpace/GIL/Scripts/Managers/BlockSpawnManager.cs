using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public struct FitInfo
    {
        public Vector2Int offset;                 // 좌상단 오프셋 (col=x, row=y)
        public List<GridSquare> coveredSquares;   // 이 배치로 덮게 될 셀들
    }

    public class BlockSpawnManager : MonoBehaviour, IManager
    {
        public static BlockSpawnManager Instance { get; private set; }

        public int Order => 12;
        private EventQueue _bus;

        [Header("Resources")] 
        [SerializeField] private string resourcesPath = "Shapes";
        [SerializeField] private List<ShapeData> shapeData;
        
        [Header("Small block Penalty")] 
        [SerializeField] private bool smallBlockPenaltyMode = true; // 켜/끄기
        [SerializeField, Range(0f, 1f)] private float smallBlockFailRate = 0.5f; // 기본 50%
        [SerializeField] private int smallBlockTileThreshold = 3;
        
        private int[] _cumulativeWeights;
        private int[] _inverseCumulativeWeights;
        private int _totalWeight;
        private int _inverseTotalWeight;

        public void SetDependencies(EventQueue bus) { _bus = bus; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); return;
            }
            Instance = this;
        }

        public void PreInit()
        {
            LoadResources();
        }

        public void Init()
        {
            BuildWeightTable();
        }

        public void PostInit() { }

        private void LoadResources()
        {
            shapeData = new List<ShapeData>(Resources.LoadAll<ShapeData>(resourcesPath));
        }

        public void BuildWeightTable()
        {
            BuildCumulativeTable();
            BuildInverseCumulativeTable();
        }

        /// <summary>
        /// 가중치 기반으로 블럭 생성, 3개 이하 블럭에 대해서는 별도의 생성 확률 적용
        /// </summary>
        public List<ShapeData> GenerateBasicWave(int count)
        {
            // 초기화/리소스 가드
            if (shapeData == null || shapeData.Count == 0)
            {
                Debug.LogError("[Spawn] shapeData empty. Did LoadResources() run? (Resources/Shapes)");
                return new List<ShapeData>(count); // 빈 리스트
            }

            var result = new List<ShapeData>(count);
            var excluded = new HashSet<ShapeData>();

            for (int i = 0; i < count; i++)
            {
                ShapeData chosen = null;
                int guard = 0, maxGuard = shapeData.Count * 2;

                while (chosen == null && guard++ < maxGuard)
                {
                    var pick = GetRandomShapeByWeightExcluding(excluded);
                    if (pick == null) break;

                    bool isSmall = pick.activeBlockCount <= smallBlockTileThreshold;
                    if (smallBlockPenaltyMode && isSmall && Random.value < smallBlockFailRate)
                    {
                        excluded.Add(pick);
                        continue;
                    }
                    chosen = pick;
                }

                if (chosen == null) chosen = GetRandomShapeByWeight(); // 최종 fallback
                if (chosen == null)
                {
                    if (shapeData.Count > 0) chosen = shapeData[Random.Range(0, shapeData.Count)];
                }

                if (chosen == null)
                {
                    Debug.LogError($"[Spawn] Failed to choose shape at slot {i}. Inserting skip.");
                    continue;
                }

                result.Add(chosen);
            }

            return result;
        }

        private void BuildCumulativeTable()
        {
            if (shapeData == null || shapeData.Count == 0)
            {
                Debug.LogError("[Spawn] BuildCumulativeTable: shapeData empty");
                _cumulativeWeights = null; _totalWeight = 0; return;
            }

            _cumulativeWeights = new int[shapeData.Count];
            _totalWeight = 0;

            for (int i = 0; i < shapeData.Count; i++)
            {
                _totalWeight += shapeData[i].chanceForSpawn;
                _cumulativeWeights[i] = _totalWeight;
            }

            Debug.Log($"가중치 계산 완료: {_totalWeight}");
        }

        private void BuildInverseCumulativeTable()
        {
            _inverseCumulativeWeights = new int[shapeData.Count];
            _inverseTotalWeight = 0;

            for (int i = 0; i < shapeData.Count; i++)
            {
                _inverseTotalWeight += (_totalWeight - shapeData[i].chanceForSpawn);
                _inverseCumulativeWeights[i] = _inverseTotalWeight;
            }

            Debug.Log($"역가중치 계산 완료: {_inverseTotalWeight}");
        }    
        
        private ShapeData GetRandomShapeByWeight()
        {
            if (shapeData == null || shapeData.Count == 0) return null;
            if (_cumulativeWeights == null || _cumulativeWeights.Length != shapeData.Count) BuildCumulativeTable();

            int r = Random.Range(0, Mathf.Max(1, _totalWeight));
            for (int i = 0; i < _cumulativeWeights.Length; i++)
            {
                if (r <= _cumulativeWeights[i])
                {
                    return shapeData[i];
                }
            }
            
            return shapeData[shapeData.Count - 1];
        }
    
        private ShapeData GetRandomShapeByWeightExcluding(HashSet<ShapeData> excludedIds)
        {
            if (shapeData == null || shapeData.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if (excludedIds != null && excludedIds.Contains(s)) continue;
                total += s.chanceForSpawn;
            }
            if (total <= 0) return null;

            int r = Random.Range(0, total);
            int acc = 0;
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if (excludedIds != null && excludedIds.Contains(s)) continue;
                acc += s.chanceForSpawn;
                if (r < acc) return s;
            }
            return null;
        }
        
        public bool CanPlaceShapeData(ShapeData shape)
        {
            var gm = GridManager.Instance;
            var states = gm.gridStates;

            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shapeRows = maxY - minY + 1;
            int shapeCols = maxX - minX + 1;

            for (int yOff = 0; yOff <= gm.rows - shapeRows; yOff++)
            {
                for (int xOff = 0; xOff <= gm.cols - shapeCols; xOff++)
                {
                    if (CanPlace(shape, minX, minY, shapeRows, shapeCols, states, yOff, xOff))
                        return true;
                }
            }

            return false;
        }

        private bool CanPlace(ShapeData shape, int minX, int minY, int shapeRows, int shapeCols,
            bool[,] grid, int yOffset, int xOffset)
        {
            for (int y = 0; y < shapeRows; y++)
            for (int x = 0; x < shapeCols; x++)
            {
                if (!shape.rows[y + minY].columns[x + minX]) continue;
                if (grid[y + yOffset, x + xOffset]) return false;
            }

            return true;
        }

        private static (int minX, int maxX, int minY, int maxY) GetShapeBounds(ShapeData shape)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            for (int y = 0; y < shape.rows.Length; y++)
            {
                for (int x = 0; x < shape.rows[y].columns.Length; x++)
                {
                    if (!shape.rows[y].columns[x]) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
            {
                minX = minY = 0;
                maxX = maxY = 0;
            }

            return (minX, maxX, minY, maxY);
        }

        /// <summary>
        /// 현 보드(실제 상태 기준)에서 shape를 놓을 수 있는 모든 위치 중
        /// '겹치지 않는' 하나를 찾아 반환 (좌상단 우선). 못 찾으면 false.
        /// </summary>
        public bool TryFindOneFit(ShapeData shape, bool[,] virtualBoard, out FitInfo fit)
        {
            fit = default;
            var gm = GridManager.Instance;
            var squares = gm.gridSquares;

            // 경계 상자
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shRows = maxY - minY + 1, shCols = maxX - minX + 1;

            for (int oy = 0; oy <= gm.rows - shRows; oy++)
            {
                for (int ox = 0; ox <= gm.cols - shCols; ox++)
                {
                    bool ok = true;
                    var list = new List<GridSquare>(8);

                    for (int y = 0; y < shRows && ok; y++)
                    for (int x = 0; x < shCols; x++)
                    {
                        if (!shape.rows[y + minY].columns[x + minX]) continue;

                        // 실제/가상 보드 중 가상 보드 우선(중복 방지)
                        bool occupied = virtualBoard != null
                            ? virtualBoard[oy + y, ox + x]
                            : squares[oy + y, ox + x].IsOccupied;

                        if (occupied) { ok = false; break; }
                        list.Add(squares[oy + y, ox + x]);
                    }

                    if (ok)
                    {
                        fit = new FitInfo { offset = new Vector2Int(ox, oy), coveredSquares = list };
                        return true;
                    }
                }
            }
            return false;
        }

        

        /// <summary>
        /// 웨이브 전체에 대해 겹치지 않는 위치를 계산하고 Hover로 표시.
        /// 같은 셀 중복 하이라이트를 막기 위해 가상보드에 순차 점유 마킹.
        /// </summary>
        public void PreviewWaveNonOverlapping(List<ShapeData> wave, List<Sprite> spritesOrNull)
        {
            ClearPreview();

            var gm = GridManager.Instance;
            var rows = gm.rows; 
            var cols = gm.cols;
            var squares = gm.gridSquares;

            // 가상 보드: 현재 점유 상태를 복사하고, 미리보기로 선점한 칸은 true로 마킹
            var virtualBoard = new bool[rows, cols];
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    virtualBoard[row, col] = squares[row, col].IsOccupied;

            for (int i = 0; i < wave.Count; i++)
            {
                if (wave[i] == null) continue;
                if (TryFindOneFit(wave[i], virtualBoard, out var fit))
                {
                    // 스프라이트는 선택 사항: null이면 그리드의 기본 hover이미지로 표시됨
                    var sprite = (spritesOrNull != null && i < spritesOrNull.Count) ? spritesOrNull[i] : null;
                    ApplyPreview(fit, sprite);

                    // 가상보드 점유 마킹(다음 블록 프리뷰가 겹치지 않게)
                    foreach (var sq in fit.coveredSquares)
                        virtualBoard[sq.RowIndex, sq.ColIndex] = true;
                }
            }
        }

        /// <summary>
        /// 지정된 배치의 셀들을 Hover 상태로 표시(점유 갱신 X)
        /// </summary>
        private void ApplyPreview(FitInfo fit, Sprite spriteOrNull)
        {
            foreach (var sq in fit.coveredSquares)
            {
                if (spriteOrNull != null) 
                    sq.SetImage(spriteOrNull);
                if (!sq.IsOccupied) 
                    sq.SetState(GridState.Hover); // Hover 이미지를 켬
            }
        }

        /// <summary>
        /// 현재 보드에서 점유되지 않은 셀들의 Hover를 모두 해제
        /// </summary>
        public void ClearPreview()
        {
            var gm = GridManager.Instance;
            var squares = gm.gridSquares;
            for (int r = 0; r < gm.rows; r++)
            {
                for (int c = 0; c < gm.cols; c++)
                {
                    if (!squares[r, c].IsOccupied)
                        squares[r, c].SetState(GridState.Normal);
                }
            }
        }
    }
}


