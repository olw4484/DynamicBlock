using System.Collections;
using System.Collections.Generic;
using System.Text;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Blocks
{
    public class BlockStorage : MonoBehaviour, IRuntimeReset
    {
        #region Variables & Properties

        [Header("Block Prefab & Data")]
        public GameObject blockPrefab;
        public List<ShapeData> shapeData;
    
        [Header("Spawn Positions")]
        public List<Transform> blockSpawnPosList;

        public Transform shapesPanel;
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

        #endregion

        #region Unity Callbacks

        void Awake()
        {
            BuildCumulativeTable();
            BuildInverseCumulativeTable();
        }
        
        void Start()
        {
            StartCoroutine(GenerateBlocksNextFrame());
        }

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
            foreach (var blk in _currentBlocks)
            {
                if (blk != null)
                    Destroy(blk.gameObject);
            }
            _currentBlocks.Clear();
            
            bool guaranteedPlaced = false;
            
            for (int i = 0; i < blockSpawnPosList.Count; i++)
            {
                GameObject newBlockObj = Instantiate(blockPrefab,
                    blockSpawnPosList[i].position,
                    Quaternion.identity,
                    shapesPanel);

                Block newBlock = newBlockObj.GetComponent<Block>();

                ShapeData selectedShape;

                if (!guaranteedPlaced)
                {
                    selectedShape = GetGuaranteedPlaceableShape();
                    guaranteedPlaced = true;
                }
                else
                {
                    selectedShape = GetRandomShapeByWeight();
                }

                newBlock.GenerateBlock(selectedShape);
                _currentBlocks.Add(newBlock);
            }
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
            FireGameOver("NoPlace");
        }

        // GameOver 트리거
        void FireGameOver(string reason = "NoPlace")
        {
            if (_gameOverFired) return;
            _gameOverFired = true;

            int score = Game.GM != null ? Game.GM.Score : 0;
            Game.Bus.PublishSticky(new GameOver(score, reason)); // UI가 모달 오픈
            Time.timeScale = 0f; // 일시정지
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
            _bus.Subscribe<GameResetRequest>(_ => ResetRuntime(), replaySticky: false);
        }

        public void ResetRuntime()
        {
            // 현재 블록들 제거
            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();

            BuildCumulativeTable();
            BuildInverseCumulativeTable();

            // 바로 다시 생성
            GenerateAllBlocks();
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
