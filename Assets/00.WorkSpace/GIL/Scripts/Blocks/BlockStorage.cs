using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Shapes;
using _00.WorkSpace.GIL.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace _00.WorkSpace.GIL.Scripts.Blocks
{
    [DefaultExecutionOrder(-10)]
    public class BlockStorage : MonoBehaviour, IRuntimeReset
    {
        #region Variables & Properties

        [Header("Block Prefab & Data")]
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] public List<Sprite> shapeImageSprites;

        [Header("Spawn Positions")]
        [SerializeField]
        private List<Transform> blockSpawnPosList;

        [SerializeField] private Transform shapesPanel;

        [Header("Block Placement Helper")]
        [SerializeField] private bool previewMode = false;

        [Header("AD")]
        [SerializeField] private float interstitialDelayAfterGameOver = 1f;
        private bool _adQueuedForThisGameOver;

        [Header("Revive")]
        [SerializeField] private int reviveWaveCount = 3;     // Revive 웨이브 크기
        [SerializeField] private bool oneRevivePerRun = true;  // 라운드당 1회 제한

        private int _persistedHigh = 0;                 // 저장된 하이스코어 캐시
        [SerializeField] private bool _tieIsNew = false; // 동점도 신기록 취급할지

        private bool _reviveUsed;
        private Coroutine _queuedInterstitialCo;

        private EventQueue _bus;

        private List<Block> _currentBlocks = new();
        private List<ShapeData> _currentBlocksShapeData = new();
        private List<Sprite> _currentBlocksSpriteData = new();
        public List<Block> CurrentBlocks => _currentBlocks;
        public List<ShapeData> CurrentBlocksShapedata => _currentBlocksShapeData;
        public List<Sprite> CurrentBlocksSpriteData => _currentBlocksSpriteData;
        private bool _handSpawnedOnce;
        int _lastResetFrame = -1;
        // 게임 오버 1회만 발동 가드
        bool _gameOverFired;
        System.Action<ContinueGranted> _onContinue;
        int _lastScore;
        string _lastReason;
        bool _paused;
        private bool _initialized;
        bool _handRestoredTried = false;
        private bool _subscribed;
        #endregion

        #region Block Image Load

        private void LoadImageData()
        {
            shapeImageSprites = new List<Sprite>(GDS.I.BlockSprites);
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
            StartCoroutine(CoBindWhenBusReady());
        }

        void OnDisable()
        {
            if (Game.IsBound && _onContinue != null)
                Game.Bus.Unsubscribe(_onContinue);

            if (_subscribed)
            {
                _bus.Unsubscribe<GridReady>(OnGridReady);
                _bus.Unsubscribe<GameEntered>(OnGameEntered);
                _bus.Unsubscribe<GameResetting>(OnGameResetting);
                _bus.Unsubscribe<GameResetRequest>(OnGameResetRequest);
                _bus.Unsubscribe<ReviveRequest>(OnReviveRequest);
                _bus.Unsubscribe<GiveUpRequest>(OnGiveUpRequest);
                _bus.Unsubscribe<GameDataChanged>(OnGameDataChanged);
                _subscribed = false;
            }
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
                      $"hasSpawner={blockManager != null} | this={GetInstanceID()}");

            if (_paused)
            {
                Debug.LogWarning("[Storage] GenerateAllBlocks EARLY-RETURN: paused==true");
                return;
            }

            // 안전 정리, 미리 생성하기
            var gm = GridManager.Instance;
            if (MapManager.Instance != null && gm != null && !gm.HasAnyOccupied())
            {
                Debug.Log("[Storage] Board empty → build classic starting map first");
                MapManager.Instance.StartNewClassicMap(); // 동기 실행, 즉시 셀 찍힘
            }

            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);

            _currentBlocks.Clear();
            _currentBlocksShapeData.Clear();
            _currentBlocksSpriteData.Clear();
            var spawner = blockManager;

            var sm = MapManager.Instance?.saveManager;
            if (sm != null && sm.TryRestoreBlocksToStorage(this))
            {
                Debug.Log("[Storage] Restored blocks from save. Skip random generation.");
                return;
            }

            if (spawner == null) { Debug.LogError("[Storage] Spawner null"); return; }

            var wave = spawner.GenerateWaveV170(blockSpawnPosList.Count);

            if (wave == null || wave.Count == 0)
            {
                Debug.LogError("[Storage] Wave is null/empty. Rebuilding weights and retry.");
                wave = spawner.GenerateWaveV170(blockSpawnPosList.Count);
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

                block.SpawnSlotIndex = i;

                Sprite sprite = null;

                if (MapManager.Instance.CurrentMode == GameMode.Tutorial)
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
                _currentBlocksShapeData.Add(shape);
                _currentBlocksSpriteData.Add(sprite);
                block.SetSpriteData(sprite);

                if (MapManager.Instance != null && MapManager.Instance.CurrentMapData != null && MapManager.Instance.CurrentMapData.goalKind == MapGoalKind.Fruit)
                {
                    TryApplyFruitOverlayToBlock(block);
                }
            }

            var fitsInfo = spawner.LastGeneratedFits;

            Debug.Log("[Block Storage] 블록 생성 완성");

            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);
            // TODO : 프리뷰 모드를 다시 사용하고 싶을 경우 주석 해체
            //if (previewMode) blockManager.PreviewWaveNonOverlapping(wave, fitsInfo, previewSprites);
        }

        private void TryApplyFruitOverlayToBlock(Block block)
        {
            if (block == null) return;

            var mm = MapManager.Instance;
            if (mm == null || mm.CurrentMapData == null) return;
            // 과일 목표 모드만
            if (mm.CurrentMapData.goalKind != MapGoalKind.Fruit) return;

            // 50% 확률 ( 임시 )
            if (UnityEngine.Random.value >= 0.5f)
            {
                // 과일 적용 안 하는 케이스: 안전하게 오버레이 초기화 시도
                ClearFruitOverlay(block);
                return;
            }

            // 활성 과일 후보 수집(0..4)
            var candidates = new List<int>(5);
            for (int i = 0; i < 5; i++)
                if (mm.IsFruitEnabled(i))
                    candidates.Add(i);

            if (candidates.Count == 0)
            {
                ClearFruitOverlay(block);
                return;
            }

            // 후보 중 랜덤 선택
            int pickIdx = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var overlaySprite = mm.GetFruitSpriteByIndex(pickIdx); // MapManager._fruitSpriteList 기반 getter
            if (overlaySprite == null)
            {
                ClearFruitOverlay(block);
                return;
            }

            // shapePrefab 아래의 활성 ShapeSquare에만 과일 이미지 덮기
            var root = block.shapePrefab != null ? block.shapePrefab.transform : block.transform;
            var squares = root.GetComponentsInChildren<ShapeSquare>(true);
            foreach (var sq in squares)
            {
                if (sq == null) continue;
                // 블록 활성 칸(자기꺼 활성)만 과일 오버레이
                bool active = sq.gameObject.activeSelf;
                sq.SetFruitImage(active ? overlaySprite : null);
            }
        }

        private void ClearFruitOverlay(Block block)
        {
            if (block == null) return;
            var root = block.shapePrefab != null ? block.shapePrefab.transform : block.transform;
            var squares = root.GetComponentsInChildren<ShapeSquare>(true);
            foreach (var sq in squares)
            {
                if (sq == null) continue;
                sq.SetFruitImage(null);
            }
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

            // 1회 부활 제한 체크
            if (oneRevivePerRun && _reviveUsed)
            {
                ConfirmGameOverImmediate(reason);
                return;
            }

            _gameOverFired = true;

            _lastScore = ScoreManager.Instance ? ScoreManager.Instance.Score
                       : (Game.GM != null ? Game.GM.Score : 0);
            _lastReason = reason;

            // Revive 열지 말고 이벤트만 발행 => UIManager가 1초 후 오픈
            Game.Bus.PublishImmediate(new PlayerDowned(_lastScore, _lastReason));

            // 전면광고 큐잉은 유지하되, 실제 표시는 ReviveScreen에서
            TryQueueInterstitialAfterGameOver();
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
            // 0) 가드
            if (oneRevivePerRun && _reviveUsed)
            {
                Debug.LogWarning("[Revive] 이미 Revive를 사용했습니다.");
                return false;
            }
            if (!_gameOverFired)
            {
                Debug.LogWarning("[Revive] GameOver 상태가 아니라 Revive 실행 안 함.");
                return false;
            }

            // 1) 웨이브 생성
            if (!BlockSpawnManager.Instance.TryGenerateReviveWave(reviveWaveCount, out var wave, out var fits)
                || wave == null || wave.Count == 0)
            {
                Debug.LogWarning($"[Revive] 웨이브 생성 실패/빈 웨이브. cnt={(wave?.Count ?? -1)} → GameOver 유지");
                return false;
            }

            // 2) 광고 큐 취소
            CancelQueuedInterstitialIfAny();

            // 3) 상태 복구(가드/일시정지) → 손패 적용 전에!
            _reviveUsed = true;
            _gameOverFired = false;
            _paused = false;
            Time.timeScale = 1f;

            Game.Bus?.PublishImmediate(new InputLock(false, "Revive"));

            // 4) 손패 적용
            var previews = ApplyReviveWave(wave);

            // 5) 안전망: 손패가 비어 있으면 즉시 리필
            if (_currentBlocks == null || _currentBlocks.Count == 0)
            {
                Debug.LogError("[Revive] 웨이브 적용 후에도 손패가 비어 있음 → Fallback GenerateAllBlocks()");
                GenerateAllBlocks();
            }

            // 6) 훅/저장
            ScoreManager.Instance?.OnHandRefilled();
            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);

            Debug.Log($"[Revive] 완료: hand={_currentBlocks.Count}, wave={wave.Count}, spawnSlots={blockSpawnPosList?.Count ?? -1}");

            // 7) 마지막에 UI/상태 이벤트 발행(패널 닫기 등)
            Game.Bus?.PublishImmediate(new RevivePerformed());
            Game.Bus?.PublishImmediate(new ContinueGranted());

            return true;
        }

        // === Revive 웨이브 적용 (손패 교체) ===
        // === Revive 웨이브 적용 (손패 교체 + 스프라이트 수집) ===
        private List<Sprite> ApplyReviveWave(List<ShapeData> wave)
        {
            if (blockSpawnPosList == null || blockSpawnPosList.Count == 0 || shapesPanel == null)
            {
                Debug.LogError($"[Revive] SpawnPos/Panel 미지정: slots={(blockSpawnPosList?.Count ?? -1)}, panel={(shapesPanel != null)}");
            }

            // 기존 손패 제거
            if (_currentBlocks != null)
            {
                for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                    if (_currentBlocks[i] != null) Destroy(_currentBlocks[i].gameObject);
                _currentBlocks.Clear();
                _currentBlocksShapeData.Clear();
                _currentBlocksSpriteData.Clear();
            }
            else _currentBlocks = new List<Block>(wave.Count);

            // 프리뷰 배열 준비
            var previewSprites = new List<Sprite>(blockSpawnPosList.Count);
            for (int k = 0; k < blockSpawnPosList.Count; k++) previewSprites.Add(null);

            // 생성
            int spawnCount = Mathf.Min(blockSpawnPosList.Count, wave.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                var shape = wave[i];
                if (shape == null) { Debug.LogWarning($"[Revive] wave[{i}] null → skip"); continue; }

                var parent = shapesPanel;
                var pos = blockSpawnPosList[i].position;
                var go = Instantiate(blockPrefab, pos, Quaternion.identity, parent);
                var blk = go.GetComponent<Block>();
                if (!blk) { Debug.LogError("[Revive] Block component missing"); Destroy(go); continue; }
                blk.SpawnSlotIndex = i;

                // 스프라이트
                Sprite sprite = (shapeImageSprites != null && shapeImageSprites.Count > 0)
                                ? shapeImageSprites[GetRandomImageIndex()]
                                : null;
                var img = blk.shapePrefab ? blk.shapePrefab.GetComponent<Image>() : null;
                if (img) img.sprite = sprite;
                previewSprites[i] = sprite;

                // 블록 초기화
                blk.GenerateBlock(shape);
                _currentBlocks.Add(blk);
                _currentBlocksShapeData.Add(blk.GetShapeData());
                _currentBlocksSpriteData.Add(sprite);
            }

            Debug.Log($"[Revive] ApplyWave: spawned={_currentBlocks.Count}/{spawnCount}, panelActive={shapesPanel.gameObject.activeInHierarchy}");
            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);
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
            _currentBlocksShapeData.Remove(placedBlock.GetShapeData());
            _currentBlocksSpriteData.Remove(placedBlock.GetSpriteData());

            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);

            CheckGameOver();

            GridManager.Instance.ValidateGridConsistency();
            GameSnapShot.SaveGridSnapshot();

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
            if (_subscribed) return; // 중복 가드

            Debug.Log($"[Storage] Bind bus={_bus.GetHashCode()}");

            _bus.Subscribe<GameResetting>(OnGameResetting, replaySticky: false);
            _bus.Subscribe<GameResetRequest>(OnGameResetRequest, replaySticky: false);
            _bus.Subscribe<GridReady>(OnGridReady, replaySticky: true);
            _bus.Subscribe<GameEntered>(OnGameEntered, replaySticky: false);
            _bus.Subscribe<ReviveRequest>(OnReviveRequest, replaySticky: false);
            _bus.Subscribe<GiveUpRequest>(OnGiveUpRequest, replaySticky: false);
            _bus.Subscribe<GameDataChanged>(OnGameDataChanged, replaySticky: true);

            _subscribed = true;
        }

        public void ResetRuntime()
        {
            _gameOverFired = false;
            _reviveUsed = false;
            Time.timeScale = 1f;
            CancelQueuedInterstitialIfAny();

            _paused = true;

            _handSpawnedOnce = false;
            _handRestoredTried = false;

            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();
            _currentBlocksShapeData.Clear();
            _currentBlocksSpriteData.Clear();
        }

        private void OnGridReady(GridReady e)
        {
            Debug.Log("[BlockStorage] OnGridReady 수신됨");
            _paused = false;

            if (!HasAnyHand())
                RefillHand();
        }

        private void TryBindBus()
        {
            if (!Game.IsBound) return;

            if (_bus == null) _bus = Game.Bus;
            if (!_subscribed) SetDependencies(_bus);
        }

        void ConfirmGameOverImmediate(string reason)
        {
            int final = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
            bool isNewBest = _tieIsNew ? (final >= _persistedHigh) : (final > _persistedHigh);

            Debug.Log($"[GO] final={final}, cachedHigh={_persistedHigh}, isNewBest={isNewBest}");

            Game.Bus.PublishImmediate(new GameOverConfirmed(final, isNewBest, reason));
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

        // 리스트를 받아 블록 재생성하기
        public bool RebuildBlocksFromLists(List<ShapeData> shapes, List<Sprite> sprites)
        {
            if (shapes == null || sprites == null) return false;
            int n = Mathf.Min(shapes.Count, sprites.Count);
            if (n <= 0) return false;

            // 기존 손패 제거
            if (_currentBlocks != null)
            {
                for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                    if (_currentBlocks[i] != null)
                        Destroy(_currentBlocks[i].gameObject);
                _currentBlocks.Clear();
                _currentBlocksShapeData.Clear();
                _currentBlocksSpriteData.Clear();
            }
            else
            {
                _currentBlocks = new List<Block>(n);
            }

            // 저장된 개수만큼만 복원 (spawn 슬롯보다 적어도 추가 생성하지 않음)
            int count = Mathf.Min(n, blockSpawnPosList.Count);
            for (int i = 0; i < count; i++)
            {
                var shape = shapes[i];
                var sprite = sprites[i];
                if (!shape) continue;

                var go = Instantiate(blockPrefab, blockSpawnPosList[i].position, Quaternion.identity, shapesPanel);
                var blk = go.GetComponent<Block>();
                if (!blk) { Destroy(go); continue; }

                blk.GenerateBlock(shape);

                var img = blk.shapePrefab ? blk.shapePrefab.GetComponent<Image>() : null;
                if (img && sprite) img.sprite = sprite;
                blk.SetSpriteData(sprite);

                _currentBlocks.Add(blk);
                _currentBlocksShapeData.Add(shape);
                _currentBlocksSpriteData.Add(sprite);
            }
            return _currentBlocks.Count > 0;
        }

        // 원래 손패 슬롯 유지
        public bool RebuildBlocksFromLists(List<ShapeData> shapes, List<Sprite> sprites, List<int> slots)
        {
            if (shapes == null || sprites == null || slots == null) return false;
            int n = Mathf.Min(shapes.Count, Math.Min(sprites.Count, slots.Count));
            if (n <= 0) return false;

            // 기존 손패 제거
            if (_currentBlocks != null)
            {
                for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                    if (_currentBlocks[i] != null) Destroy(_currentBlocks[i].gameObject);
                _currentBlocks.Clear();
                _currentBlocksShapeData.Clear();
                _currentBlocksSpriteData.Clear();
            }
            else
            {
                _currentBlocks = new List<Block>(n);
            }

            // 저장된 슬롯에만 재생성 (부족한 슬롯은 비워둠)
            for (int k = 0; k < n; k++)
            {
                var shape = shapes[k];
                var sprite = sprites[k];
                int slot = slots[k];

                if (!shape) continue;
                if (slot < 0 || slot >= blockSpawnPosList.Count) continue;

                var pos = blockSpawnPosList[slot];
                var go = Instantiate(blockPrefab, pos.position, Quaternion.identity, shapesPanel);
                var blk = go.GetComponent<Block>();
                if (!blk) { Destroy(go); continue; }

                blk.GenerateBlock(shape);

                var img = blk.shapePrefab ? blk.shapePrefab.GetComponent<Image>() : null;
                if (img && sprite) img.sprite = sprite;

                blk.SetSpriteData(sprite);
                blk.SpawnSlotIndex = slot;                   // 슬롯 보존

                _currentBlocks.Add(blk);
                _currentBlocksShapeData.Add(shape);
                _currentBlocksSpriteData.Add(sprite);
            }

            return _currentBlocks.Count > 0;
        }

        // === 손패 오브젝트/리스트 즉시 정리 (리셋 시 사용) ===
        public void ClearHand()
        {
            if (_currentBlocks != null)
            {
                for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                    if (_currentBlocks[i] != null)
                        Destroy(_currentBlocks[i].gameObject);
                _currentBlocks.Clear();
            }
            _currentBlocksShapeData?.Clear();
            _currentBlocksSpriteData?.Clear();

            Debug.Log("[Storage] Hand cleared by reset.");
        }
        void OnGameEntered(GameEntered e)
        {
            if (e.mode != GameMode.Classic) return;

            _paused = false;
            if (!_initialized) _initialized = true;

            if (HasAnyHand()) { Debug.Log("[Hand] Already has hand, skip refill"); return; }

            var gm = GridManager.Instance;
            var gd = MapManager.Instance?.saveManager?.gameData;
            bool hasSavable = gd != null && gd.isClassicModePlaying &&
                              gd.currentMapLayout != null && gd.currentMapLayout.Any(v => v > 0);
            Debug.Log($"[HAND] OnGameEntered → RefillHand: gm.HasAnyOccupied()={gm.HasAnyOccupied()}, hasSavable={hasSavable}");
            RefillHand();

        }
        bool HasAnyHand()
        {
            if (_currentBlocks == null || _currentBlocks.Count == 0) return false;
            for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                if (_currentBlocks[i] == null) _currentBlocks.RemoveAt(i);
            return _currentBlocks.Count > 0;
        }

        public void RefillHand()
        {
            if (!_handRestoredTried)
            {
                _handRestoredTried = true;

                var sm = MapManager.Instance?.saveManager;
                int nameCnt = sm?.Data?.currentShapeNames?.Count ?? 0;
                Debug.Log($"[Hand] Try restore before generate. names={nameCnt}");

                if (sm != null && sm.TryRestoreBlocksToStorage(this))
                {
                    Debug.Log("[Hand] Restored from save → skip generation");
                    return;
                }
            }

            Debug.Log("[Hand] Refill → GenerateAllBlocks()");
            GenerateAllBlocks();
        }
    private void OnGameResetting(GameResetting _)
        {
            _gameOverFired = false;
            Time.timeScale = 1f;
        }

        private void OnGameResetRequest(GameResetRequest e)
        {
            Debug.Log("[Storage] ResetRuntime (scene-only) — no SaveManager clear");
            ResetRuntime();
        }

        private void OnReviveRequest(ReviveRequest _)
        {
            if (!GenerateAdRewardWave()) ConfirmGameOver();
        }

        private void OnGiveUpRequest(GiveUpRequest _)
        {
            ConfirmGameOver();
        }

        private void OnGameDataChanged(GameDataChanged e)
        {
            _persistedHigh = e.data.highScore;
            Debug.Log($"[BlockStorage] HighScore cached = {_persistedHigh}");
        }
        IEnumerator CoBindWhenBusReady()
        {
            while (!Game.IsBound) yield return null;

            if (!_subscribed) SetDependencies(Game.Bus);

            yield return null;

            if (!HasAnyHand())
            {
                Debug.Log("[Storage] Boot fallback: no hand after bind → GenerateAllBlocks");
                GenerateAllBlocks();
            }
        }
    }
}