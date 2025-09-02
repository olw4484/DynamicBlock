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
    [DefaultExecutionOrder(-10)]
    public class BlockStorage : MonoBehaviour, IRuntimeReset
    {
        #region Variables & Properties

        [Header("Block Prefab & Data")]
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private List<Sprite> shapeImageSprites;
        
        [Header("Spawn Positions")]

        [SerializeField] private List<Transform> blockSpawnPosList;
        [SerializeField] private Transform shapesPanel;

        [Header("Block Placement Helper")] 
        [SerializeField] private bool previewMode = true;
        
        private EventQueue _bus;

        private List<Block> _currentBlocks = new();
        
        // 게임 오버 1회만 발동 가드
        bool _gameOverFired;
        System.Action<ContinueGranted> _onContinue;

        bool _paused;
        private bool _initialized;

        #endregion

        #region Unity Callbacks

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
        
        #region Block Generation

        private void GenerateAllBlocks()
        {
            Debug.Log($"[Storage] >>> ENTER GenerateAllBlocks | paused={_paused} | " +
                      $"spawnPos={(blockSpawnPosList == null ? -1 : blockSpawnPosList.Count)} | " +
                      $"sprites={(shapeImageSprites == null ? -1 : shapeImageSprites.Count)} | " +
                      $"hasSpawner={(BlockSpawnManager.Instance != null)} | this={GetInstanceID()}");

            if (_paused)
            {
                Debug.LogWarning("[Storage] GenerateAllBlocks EARLY-RETURN: paused==true");
                return;
            }

            // 안전 정리
            for (int i = 0; i < _currentBlocks.Count; i++)
            {
                if (_currentBlocks[i]) 
                    Destroy(_currentBlocks[i].gameObject);
            }
            _currentBlocks.Clear();

            var spawner = BlockSpawnManager.Instance;
            if (spawner == null) { Debug.LogError("[Storage] Spawner null"); return; }

            var wave = spawner.GenerateBasicWave(blockSpawnPosList.Count);

            if (wave == null || wave.Count == 0)
            {
                Debug.LogError("[Storage] Wave is null/empty. Rebuilding weights and retry.");
                spawner.BuildWeightTable();
                wave = spawner.GenerateBasicWave(blockSpawnPosList.Count);
                if (wave == null || wave.Count == 0) return;
            }

            var previewSprites = new List<Sprite>(blockSpawnPosList.Count);

            for (int i = 0; i < blockSpawnPosList.Count; i++)
            {
                var shape = (i < wave.Count) ? wave[i] : null;
                if (shape == null)
                {
                    Debug.LogWarning($"[Storage] wave[{i}] is null → skip this slot.");
                    continue; 
                }

                var go = Instantiate(blockPrefab, blockSpawnPosList[i].position, Quaternion.identity, shapesPanel);
                var block = go.GetComponent<Block>();
                if (block == null) { Debug.LogError("[Storage] Block component missing"); Destroy(go); continue; }

                // 이미지 세팅
                var sprite = shapeImageSprites[GetRandomImageIndex()];
                block.shapePrefab.GetComponent<Image>().sprite = sprite;

                block.GenerateBlock(shape);
                _currentBlocks.Add(block);
            }

            if (previewMode)
                BlockSpawnManager.Instance.PreviewWaveNonOverlapping(wave, previewSprites);
        }

        private int GetRandomImageIndex()
        {
            return Random.Range(0, shapeImageSprites.Count);
        }
        
        #endregion
        

        #region Game Check

        private void CheckGameOver()
        {
            if (_currentBlocks == null || _currentBlocks.Count == 0)
                return;

            foreach (var block in _currentBlocks)
            {
                if (BlockSpawnManager.Instance.CanPlaceShapeData(block.GetShapeData()))
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

            int score = ScoreManager.Instance ? ScoreManager.Instance.Score
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
                // 리필 실행
                GenerateAllBlocks();

                // 리필 직후: 이번 세트 평가 → 클리어 없었으면 콤보 0
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.OnHandRefilled();
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

            BlockSpawnManager.Instance.BuildWeightTable();
            // 생성은 GridReady에서 재개
        }

        private void OnGridReady(GridReady e)
        {
            Debug.Log($"[Storage] OnGridReady | before: paused={_paused}, initialized={_initialized}, this={GetInstanceID()}");

            if (_paused == false && _initialized)
            {
                Debug.Log("[Storage] OnGridReady SKIP: already running");
                return;
            }

            _paused = false;
            if (!_initialized) _initialized = true;

            Debug.Log("[Storage] OnGridReady → call GenerateAllBlocks()");
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
