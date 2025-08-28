using System.Collections;
using System.Collections.Generic;
using System.Text;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;
using UnityEngine.UI;

namespace _00.WorkSpace.GIL.Scripts.Blocks
{
    public class BlockStorage : MonoBehaviour, IRuntimeReset
    {
        #region Variables & Properties

        [Header("Block Prefab & Data")]
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private List<ShapeData> shapeData;
        [SerializeField] private List<Sprite> shapeImageSprites;
        
        [Header("Spawn Positions")]

        [SerializeField] private List<Transform> blockSpawnPosList;
        [SerializeField] private Transform shapesPanel;
    
        private EventQueue _bus;

        private List<Block> _currentBlocks = new();
        private GridSquare[,] Grid => GridManager.Instance.gridSquares;
            
        private int[] _cumulativeWeights;
        private int[] _inverseCumulativeWeights;
        private int _totalWeight;
        private int _inverseTotalWeight;

        // 게임 오버 1회만 발동 가드
        bool _gameOverFired;
        System.Action<ContinueGranted> _onContinue;

        bool _paused;
        private bool _initialized;

        #endregion

        #region Unity Callbacks

        void Awake()
        {
            BuildCumulativeTable();
            BuildInverseCumulativeTable();
        }

        void Start() { TryBindBus(); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                DebugCurrentBlocks();
            }
        }

        void OnEnable()
        {
            // Game.Bind 이후에만 구독 시도
            if (Game.IsBound)
            {
                _onContinue = _ =>
                {
                    _gameOverFired = false;
                    Time.timeScale = 1f;
                    // 이어하기 정책에 맞게 블록 재생성/리셋
                    GenerateAllBlocks();
                };
                Game.Bus.Subscribe(_onContinue, replaySticky: false);
            }
        }

        void OnDisable()
        {
            if (Game.IsBound && _onContinue != null)
                Game.Bus.Unsubscribe(_onContinue);
        }

        #endregion

        #region Weight Tables

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

        #endregion
        
        #region Block Generation

        private IEnumerator GenerateBlocksNextFrame()
        {
            // 한 프레임 대기
            yield return null;

            // GridManager 준비 확인
            if (GridManager.Instance == null || GridManager.Instance.gridSquares == null)
                yield break;

            GenerateAllBlocks();
        }

        private void GenerateAllBlocks()
        {
            if (_paused) return;

            // 안전 정리
            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();

            bool guaranteedPlaced = false;

            for (int i = 0; i < blockSpawnPosList.Count; i++)
            {
                var go = Instantiate(blockPrefab,
                                     blockSpawnPosList[i].position,
                                     Quaternion.identity,
                                     shapesPanel);

                var block = go.GetComponent<Block>();

                // 오프바이원 픽스: 마지막 인덱스 포함
                block.shapePrefab.GetComponent<Image>().sprite =
                    shapeImageSprites[Random.Range(0, shapeImageSprites.Count)];

                var shape = !guaranteedPlaced ? GetGuaranteedPlaceableShape()
                                              : GetRandomShapeByWeight();
                guaranteedPlaced = true;

                block.GenerateBlock(shape);
                _currentBlocks.Add(block);
            }

            // CheckGameOver();
        }

        private int GetRandomImageIndex()
        {
            return Random.Range(0, shapeImageSprites.Count);
        }
        
        private ShapeData GetRandomShapeByWeight()
        {
            float randomValue = Random.Range(0, _totalWeight);

            for (int i = 0; i < _cumulativeWeights.Length; i++)
            {
                if (randomValue <= _cumulativeWeights[i])
                    return shapeData[i];
            }

            return shapeData[shapeData.Count - 1];
        }
        
        private ShapeData GetGuaranteedPlaceableShape()
        {
            foreach (var shape in shapeData)
            {
                if (CanPlaceShapeData(shape))
                    return shape;
            }

            return GetRandomShapeByWeight();
        }

        #endregion

        #region Placement Chect

        private bool CanPlaceShapeData(ShapeData shape)
        {
            if (Grid == null)
                return false;
            
            int gridRows = GridManager.Instance.rows;
            int gridCols = GridManager.Instance.cols;
            
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shapeRows = maxY - minY + 1;
            int shapeCols = maxX - minX + 1;

            for (int yOffset = 0; yOffset <= gridRows - shapeRows; yOffset++)
            {
                for (int xOffset = 0; xOffset <= gridCols - shapeCols; xOffset++)
                {
                    if (CanPlace(shape, minX, minY, shapeRows, shapeCols, Grid, yOffset, xOffset))
                        return true;
                }
            }
            return false;
        }
        
        private bool CanPlace(ShapeData shape, int minX, int minY, int shapeRows, int shapeCols, GridSquare[,] grid, int yOffset, int xOffset)
        {
            for (int y = 0; y < shapeRows; y++)
            {
                for (int x = 0; x < shapeCols; x++)
                {
                    if (shape.rows[y + minY].columns[x + minX])
                    {
                        if (grid[y + yOffset, x + xOffset].IsOccupied)
                            return false;
                    }
                }
            }
            return true;
            // TODO : 조건을 반대로 해보자
        }
        
        private (int minX, int maxX, int minY, int maxY) GetShapeBounds(ShapeData shape)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            for (int y = 0; y < shape.rows.Length; y++)
            {
                for (int x = 0; x < shape.rows[y].columns.Length; x++)
                {
                    if (shape.rows[y].columns[x])
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            return (minX, maxX, minY, maxY);
        }

        #endregion

        #region Game Check

        private void CheckGameOver()
        {
            if (_currentBlocks == null || _currentBlocks.Count == 0)
                return;

            foreach (var block in _currentBlocks)
            {
                if (CanPlaceShapeData(block.GetShapeData()))
                    return;
            }

            Debug.Log("===== GAME OVER! 더 이상 배치할 수 있는 블록이 없습니다. =====");

            // TODO : 여기 밑에다가 게임 오버 붙이기!
            ActivateGameOver();
        }

        private void ActivateGameOver()
        {
            
            FireGameOver("NoPlace");
        }

        // GameOver 트리거
        void FireGameOver(string reason = "NoPlace")
        {
            if (_gameOverFired) { Debug.Log("[GameOver] blocked by guard"); return; }
            _gameOverFired = true;

            int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score
                       : (Game.GM != null ? Game.GM.Score : 0);

            Game.Bus.PublishSticky(new GameOver(score, reason));
            Time.timeScale = 0f;
        }

        public void OnBlockPlaced(Block placedBlock)
        {
            _currentBlocks.Remove(placedBlock);
            
            CheckGameOver();
            
            if (_currentBlocks.Count == 0)
            {
                GenerateAllBlocks();
            }
        }

        #endregion
        #region Game Reset
        public void SetDependencies(EventQueue bus)
        {
            _bus = bus;
            Debug.Log($"[Storage] Bind bus={_bus.GetHashCode()}");

            // 1) 리셋 시작: 가드/타임스케일 초기화 (이중 안전망)
            _bus.Subscribe<GameResetting>(_ =>
            {
                _gameOverFired = false;
                Time.timeScale = 1f;
            }, replaySticky: false);

            // 2) 리셋 처리: 내부 상태 정리
            _bus.Subscribe<GameResetRequest>(_ =>
            {
                Debug.Log("[Storage] ResetRuntime received");
                ResetRuntime();
            }, replaySticky: false);

            // 3) 그리드 준비됨: 초기 블록 생성
            _bus.Subscribe<GridReady>(OnGridReady, replaySticky: true);

            // 4) 폴백: 이미 그리드가 있으면 바로 한 번 호출
            if (GridManager.Instance != null && GridManager.Instance.gridSquares != null)
            {
                Debug.Log("[Storage] Fallback OnGridReady (grid already built)");
                OnGridReady(new GridReady(GridManager.Instance.rows, GridManager.Instance.cols));
            }
        }

        public void ResetRuntime()
        {
            _gameOverFired = false;
            Time.timeScale = 1f;

            // 생성/체크 잠깐 중지
            _paused = true;

            // 기존 블록 정리
            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();

            BuildCumulativeTable();
            BuildInverseCumulativeTable();
            // 생성은 GridReady에서 재개
        }

        private void OnGridReady(GridReady e)
        {
            // 디듀프 가드: 같은 프레임/두 번 이상 호출 방지
            if (_paused == false && _initialized)
            {
                // 이미 정상 진행 중이면, 리셋·중복이 아닌 이상 굳이 재생성 안 함
                return;
            }

            _paused = false;

            // 초기 한 번은 생성하도록 플래그만 세팅
            if (!_initialized) _initialized = true;

            Debug.Log("[Storage] OnGridReady → GenerateAllBlocks()");
            GenerateAllBlocks();
        }


        private void TryBindBus()
        {
            if (_bus != null || !Game.IsBound) return;
            SetDependencies(Game.Bus);
        }

        #endregion
        #region Debug

        private void DebugCurrentBlocks()
        {
            if (_currentBlocks == null || _currentBlocks.Count == 0)
            {
                Debug.Log("현재 보관 중인 블록이 없습니다.");
                return;
            }

            int index = 0;
            foreach (var block in _currentBlocks)
            {
                index++;
                ShapeData data = block.GetShapeData();
                StringBuilder sb = new StringBuilder();
                sb.Append($"Block No.{index}\n");
                sb.Append(ShapeDataToString(data));
                Debug.Log(sb.ToString());
            }
        }
        
        private string ShapeDataToString(ShapeData shapeData)
        {
            if (shapeData == null || shapeData.rows == null)
                return "Null ShapeData";

            StringBuilder sb = new StringBuilder();
            
            for (int y = 0; y < shapeData.rows.Length; y++)
            {
                for (int x = 0; x < shapeData.rows[y].columns.Length; x++)
                {
                    sb.Append(shapeData.rows[y].columns[x] ? "O " : "X ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion
    }
}
