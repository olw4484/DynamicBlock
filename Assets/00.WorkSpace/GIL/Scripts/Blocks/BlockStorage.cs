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
        [SerializeField] public List<Sprite> shapeImageSprites;
        [SerializeField] private string imageDictory = "BlockImages";

        [Header("Spawn Positions")] [SerializeField]
        private List<Transform> blockSpawnPosList;

        [SerializeField] private Transform shapesPanel;

        [Header("Block Placement Helper")] 
        [SerializeField] private bool previewMode = true;
        
        [Header("AD")]
        [SerializeField] private float interstitialDelayAfterGameOver = 1f;
        private bool _adQueuedForThisGameOver;
        
        [Header("Revive")]
        [SerializeField] private int  reviveWaveCount = 3;     // Revive 웨이브 크기
        [SerializeField] private bool oneRevivePerRun = true;  // 라운드당 1회 제한

        private bool _reviveUsed;
        private Coroutine _queuedInterstitialCo; 
        
        private EventQueue _bus;

        private List<Block> _currentBlocks = new();
        
        private bool _handSpawnedOnce;
        
        // 게임 오버 1회만 발동 가드
        bool _gameOverFired;
        System.Action<ContinueGranted> _onContinue;
        int _lastScore;
        string _lastReason;
        bool _paused;
        private bool _initialized;

        #endregion

        #region Block Image Load

        private void LoadImageData()
        {
            shapeImageSprites = new List<Sprite>(Resources.LoadAll<Sprite>(imageDictory));
        }
        #endregion
        
        #region Unity Callbacks

        void Awake()
        {
            LoadImageData();
        }
        
        void Start() { TryBindBus(); }

        void OnEnable()
        {
            Game.Bus?.Subscribe<GridReady>(_ => {
                Debug.Log("[Block Storage] : 그리드 셋팅 완료, 블럭 생성 준비");
                if (_paused || _handSpawnedOnce) return;
                GenerateAllBlocks();
                _handSpawnedOnce = true;
            }, replaySticky:true);

            Game.Bus?.Subscribe<GameResetting>(_ => _handSpawnedOnce = false, replaySticky:false);
            // 이전 코드인데 없이도 크게 문제 없는 것으로 보임, 혹시 모르니 유지할 예정
            // Game.Bind 이후에만 구독 시도
            // if (Game.IsBound)
            // {
            //     _onContinue = _ =>
            //     {
            //         _gameOverFired = false;
            //         Time.timeScale = 1f;
            //         // 이어하기 정책에 맞게 블록 재생성/리셋
            //         GenerateAllBlocks();
            //     };
            //     Game.Bus.Subscribe(_onContinue, replaySticky: false);
            // }
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
            Debug.Log("[Block Storage] : 블록 생성 시작");
            var blockManager = BlockSpawnManager.Instance;
            Debug.Log($"[Storage] >>> ENTER GenerateAllBlocks | paused={_paused} | " +
                      $"spawnPos={(blockSpawnPosList == null ? -1 : blockSpawnPosList.Count)} | " +
                      $"sprites={(shapeImageSprites == null ? -1 : shapeImageSprites.Count)} | " +
                      $"hasSpawner={(blockManager != null)} | this={GetInstanceID()}");

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

            var spawner = blockManager;
            if (spawner == null) { Debug.LogError("[Storage] Spawner null"); return; }

            var wave = spawner.GenerateBasicWave(blockSpawnPosList.Count);

            if (wave == null || wave.Count == 0)
            {
                Debug.LogError("[Storage] Wave is null/empty. Rebuilding weights and retry.");
                wave = spawner.GenerateBasicWave(blockSpawnPosList.Count);
                if (wave == null || wave.Count == 0) return;
            }

            var previewSprites = new List<Sprite>(blockSpawnPosList.Count);
            
            for (int k = 0; k < blockSpawnPosList.Count; k++) previewSprites.Add(null);
            
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

                Sprite sprite = null;
                
                if (MapManager.Instance.GameMode == GameMode.Tutorial)
                {
                    sprite = shapeImageSprites[0];
                }
                else
                {
                    // 이미지 세팅
                    sprite = shapeImageSprites[GetRandomImageIndex()];
                }
                block.shapePrefab.GetComponent<Image>().sprite = sprite;
                previewSprites[i] = sprite;
                block.GenerateBlock(shape);
                _currentBlocks.Add(block);
            }

            var fitsInfo = spawner.LastGeneratedFits;
            
            if (previewMode)
                blockManager.PreviewWaveNonOverlapping(wave, fitsInfo, previewSprites);
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

            ActivateGameOver();
        }

        private void ActivateGameOver()
        {
            FireGameOver("NoPlace");
        }

        // GameOver 트리거
        void FireGameOver(string reason = "NoPlace")
        {
            if (_gameOverFired) { Debug.Log("[Downed] blocked by guard"); return; }

            if (oneRevivePerRun && _reviveUsed)
            {
                ConfirmGameOverImmediate(reason);
                return;
            }

            _gameOverFired = true;

            _lastScore = ScoreManager.Instance ? ScoreManager.Instance.Score
                       : (Game.GM != null ? Game.GM.Score : 0);
            _lastReason = reason;

            Game.Bus.PublishImmediate(new PlayerDowned(_lastScore, _lastReason));
            StartCoroutine(Co_PauseAndOpenRevive());
        }
        void TryQueueInterstitialAfterGameOver()
        {
            if (_adQueuedForThisGameOver) return;
            _adQueuedForThisGameOver = true;
            //StartCoroutine(Co_ShowInterstitialAfterGameOver());
            
            // SJH : 게임 오버시 전면광고 실행은 ReviveScreen에서 실행
            //_queuedInterstitialCo = StartCoroutine(Co_ShowInterstitialAfterGameOver());
        }
        
        /// <summary>
        /// 광고 보상(onRewarded) 또는 UI 버튼에서 호출하면,
        /// GameOver 상태를 해제하고 Revive 웨이브를 손패에 적용하여 즉시 재개한다.
        /// </summary>
        /// <returns>true: 성공(재개) / false: 실패(그대로 GameOver 유지)</returns>
        public bool GenerateAdRewardWave()
        {
            // 0) 호출 가드
            if (oneRevivePerRun && _reviveUsed)
            {
                Debug.LogWarning("[Revive] 이미 Revive를 사용했습니다.");
                return false;
            }
            if (!_gameOverFired)
            {
                Debug.LogWarning("[Revive] GameOver 상태가 아니라 Revive를 실행하지 않습니다.");
                return false;
            }

            // 1) Revive 웨이브 생성 (라인 보정 → 가상 배치 + 라인 제거 반영)
            if (!BlockSpawnManager.Instance.TryGenerateReviveWave(reviveWaveCount, out var wave, out var fits))
            {
                Debug.LogWarning("[Revive] 라인 보정 불가 → Revive 웨이브 생성 실패. GameOver 유지");
                return false;
            }

            // 2) 대기 중인 인터스티셜 광고 예약이 있다면 취소 (재개 직후 광고가 뜨는 것 방지)
            CancelQueuedInterstitialIfAny();
            // 3) GameOver 해제 & UI 닫기
            _reviveUsed = true;
            _gameOverFired = false;
            Time.timeScale = 1f;

            Game.Bus?.PublishImmediate(new RevivePerformed());
            Game.Bus?.PublishImmediate(new ContinueGranted());

            // 4) 손패를 Revive 웨이브로 교체하고, 손패 갱신 훅 호출
            var previewSprites = ApplyReviveWave(wave);
            ScoreManager.Instance?.OnHandRefilled();
            BlockSpawnManager.Instance.PreviewWaveNonOverlapping(wave, fits, previewSprites);

            Debug.Log("[Revive] Revive 웨이브 적용 완료, 게임 재개");
            return true;
        }
        
        // === Revive 웨이브 적용 (손패 교체) ===
        // === Revive 웨이브 적용 (손패 교체 + 스프라이트 수집) ===
        private List<Sprite> ApplyReviveWave(List<ShapeData> wave)
        {
            // 1) 기존 손패 제거
            if (_currentBlocks != null)
            {
                for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                    if (_currentBlocks[i] != null)
                        Destroy(_currentBlocks[i].gameObject);
                _currentBlocks.Clear();
            }
            else _currentBlocks = new List<Block>(wave.Count);

            // 2) GenerateAllBlocks와 동일하게: 슬롯 개수만큼 previewSprites 준비
            var previewSprites = new List<Sprite>(blockSpawnPosList.Count);
            for (int k = 0; k < blockSpawnPosList.Count; k++) previewSprites.Add(null);

            // 3) 같은 방식으로 생성 + 스프라이트 세팅
            for (int i = 0; i < blockSpawnPosList.Count && i < wave.Count; i++)
            {
                var shape = wave[i];
                if (shape == null)
                {
                    Debug.LogWarning($"[Revive] wave[{i}] is null → skip this slot.");
                    continue;
                }

                // GenerateAllBlocks와 동일한 부모/좌표 체계
                var go = Instantiate(blockPrefab, blockSpawnPosList[i].position, Quaternion.identity, shapesPanel);
                var blk = go.GetComponent<Block>();
                if (!blk) { Debug.LogError("[Revive] Block component missing"); Destroy(go); continue; }

                // 스프라이트 선택 로직도 GenerateAllBlocks와 동일하게
                Sprite sprite = shapeImageSprites[GetRandomImageIndex()];
                // 프리팹 내 이미지 적용
                var img = blk.shapePrefab ? blk.shapePrefab.GetComponent<Image>() : null;
                if (img) img.sprite = sprite;

                previewSprites[i] = sprite;

                // 블록 초기화
                blk.GenerateBlock(shape);
                _currentBlocks.Add(blk);
            }

            return previewSprites;
        }
        private void CancelQueuedInterstitialIfAny() //
        {
            if (_queuedInterstitialCo != null)
            {
                StopCoroutine(_queuedInterstitialCo);
                _queuedInterstitialCo = null;
            }
            _adQueuedForThisGameOver = false;
        }
        IEnumerator Co_ShowInterstitialAfterGameOver()
        {
            // 2) 게임이 멈춰도 동작하는 Realtime 딜레이
            yield return new WaitForSecondsRealtime(interstitialDelayAfterGameOver);
        
            // 3) 전면 광고 시도 (Null/LIVE 무관하게 파사드만 호출)
            if (Game.IsBound && Game.Ads != null && Game.Ads.IsInterstitialReady())
            {
                Game.Ads.ShowInterstitial(onClosed: () =>
                {
                    // 닫힌 뒤 다음 로드를 준비
                    Game.Ads.Refresh();
                    // GameOver는 이미 pause 상태이므로 timeScale 재개 X
                });
            }
            else
            {
                // 준비 안 됐으면 로드만 재시도
                Game.Ads?.Refresh();
            }
        }
        IEnumerator Co_PauseAndOpenRevive()
        {
            yield return new WaitForEndOfFrame();
            Time.timeScale = 0f;
            TryQueueInterstitialAfterGameOver();
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

        public void ConfirmGameOver()
        {
            if (!_gameOverFired) return;
            ConfirmGameOverImmediate(_lastReason);
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

            _bus.Subscribe<ReviveRequest>(_ =>
            {
                if (!GenerateAdRewardWave())
                    ConfirmGameOver(); // 실패 시 곧바로 확정 오버
            }, replaySticky: false);

            _bus.Subscribe<GiveUpRequest>(_ => { ConfirmGameOver(); }, replaySticky: false);
        }

        public void ResetRuntime()
        {
            _gameOverFired = false;
            _reviveUsed = false;
            Time.timeScale = 1f;
            CancelQueuedInterstitialIfAny();

            Game.Bus?.ClearSticky<PlayerDowned>();
            Game.Bus?.ClearSticky<GameOverConfirmed>();

            // 생성/체크 잠깐 중지
            _paused = true;

            // 기존 블록 정리
            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();

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

        void ConfirmGameOverImmediate(string reason)
        {
            _lastScore = ScoreManager.Instance ? ScoreManager.Instance.Score
                       : (Game.GM != null ? Game.GM.Score : 0);
            _lastReason = reason;

            CancelQueuedInterstitialIfAny();
            Time.timeScale = 0f;

            Game.Bus.PublishImmediate(new GameOverConfirmed(_lastScore, _lastReason));
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
