using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class BlockSpawnManager : MonoBehaviour
    {
        public static BlockSpawnManager Instance { get; private set; }

        [Header("Resources")] 
        [SerializeField] private string resourcesPath = "Shapes";
        [SerializeField] private List<ShapeData> shapeData;
        
        [Header("Small block Penalty")] 
        [SerializeField] private bool smallBlockPenaltyMode = true; // 켜/끄기
        [SerializeField, Range(0f, 1f)] private float smallBlockFailRate = 0.5f; // 기본 50%
        [SerializeField]private int smallBlockTileThreshold = 3;
        
        private int[] _cumulativeWeights;
        private int[] _inverseCumulativeWeights;
        private int _totalWeight;
        private int _inverseTotalWeight;

        void Awake()
        {
            Init();

            LoadResources();
            
            BuildWeightTable();
        }
        
        private void Init()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }
        
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
            var result = new List<ShapeData>(count);

            // 이번 웨이브에서 "소환 실패한 블록"은 이후 검색에서 제외
            var excludedForThisWave = new HashSet<string>();

            for (int i = 0; i < count; i++)
            {
                ShapeData chosen = null;
                int guard = 0; // 무한 루프 방지

                while (chosen == null && guard++ < shapeData.Count)
                {
                    // 제외 목록 반영해서 가중치 추첨
                    var pick = GetRandomShapeByWeightExcluding(excludedForThisWave);
                    if (pick == null) break; // 전부 제외된 경우

                    // 소형 여부를 activeBlockCount로 판정
                    bool isSmall = pick.activeBlockCount <= smallBlockTileThreshold;

                    if (smallBlockPenaltyMode && isSmall && Random.value < smallBlockFailRate)
                    {
                        // 소환 실패 → 이번 웨이브에서 제외하고 재검색
                        excludedForThisWave.Add(pick.Id);
                        continue;
                    }

                    // 성공
                    chosen = pick;
                }

                // 그래도 못 뽑았으면 가중치 기반 생성 ( 발동할 일 매우 적음 )
                if (chosen == null)
                {
                    Debug.LogWarning("모든 후보가 소형 페널티로 제외되어 가중치 강제 소환을 수행합니다.");                    
                    chosen = GetRandomShapeByWeight();
                }

                result.Add(chosen);
            }

            return result;
        }

        private void BuildCumulativeTable()
        {
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

        private ShapeData GetGuaranteedPlaceableShape()
        {
            foreach (var s in shapeData)
                if (CanPlaceShapeData(s))
                    return s;

            // 보장 실패 시 기존과 동일하게 가중치 폴백
            return GetRandomShapeByWeight();
        }
    
        private ShapeData GetRandomShapeByWeightExcluding(HashSet<string> excludedIds)
        {
            if (shapeData == null || shapeData.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if (excludedIds != null && excludedIds.Contains(s.Id)) continue;
                total += s.chanceForSpawn;
            }
            if (total <= 0) return null;

            int r = Random.Range(0, total);
            int acc = 0;
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if (excludedIds != null && excludedIds.Contains(s.Id)) continue;
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
            } // 빈 데이터 방어

            return (minX, maxX, minY, maxY);
        }
    }
}


