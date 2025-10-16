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
        [SerializeField] private List<Transform> blockSpawnPosList;
        [SerializeField] private Transform shapesPanel;

        [Header("Block Placement Helper")]
        [SerializeField] private bool previewMode = false;
        [SerializeField] private int fruitFullFillCapPerWave = 0; // 0=무제한, 기본 1개만 FullFill

        [Header("AD")]
        [SerializeField] private float interstitialDelayAfterGameOver = 1f;
        private bool _adQueuedForThisGameOver;

        [Header("Revive")]
        [SerializeField] private int reviveWaveCount = 3;
        [SerializeField] private bool oneRevivePerRun = true;

        private int _persistedHigh = 0;
        [SerializeField] private bool _tieIsNew = false;

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

        // 다운 컨텍스트 / 게임오버 가드
        bool _gameOverFired;          // "다운 상태(리바이브 컨텍스트) 진입함" 플래그
        bool _downedPending;          // 동일 의미로 사용(중복 가드)
        bool _paused;
        private bool _initialized;
        bool _handRestoredTried = false;
        private bool _subscribed;
        bool _skipRestoreThisBoot;
        private bool _reviveAwaitFirstPlacement;  // 리바이브 후 첫 배치 전까지 GO 체크 금지
        private float _goCheckFreezeUntil = 0f;   // 리바이브 직후 프리즈(초 단위, realtime)

        // 사용하지 않던 필드 제거/미사용 표시
        // bool _downedSent;
        // bool _gameOverActivated;
        // bool _gameOverLocked;

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

            // 보드가 비어있다면 클래식 시작 보드부터 구성
            var gm = GridManager.Instance;
            if (MapManager.Instance != null && gm != null && !gm.HasAnyOccupied())
            {
                Debug.Log("[Storage] Board empty → build classic starting map first");
                MapManager.Instance.StartNewClassicMap();
            }

            // 기존 손패 정리
            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);

            _currentBlocks.Clear();
            _currentBlocksShapeData.Clear();
            _currentBlocksSpriteData.Clear();

            var spawner = blockManager;
            var sm = MapManager.Instance?.saveManager;

            // 저장에서 복원 시도
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
                    sprite = shapeImageSprites[0];
                else
                    sprite = shapeImageSprites[GetRandomImageIndex()];

                block.shapePrefab.GetComponent<Image>().sprite = sprite;
                previewSprites[i] = sprite;

                block.GenerateBlock(shape);
                _currentBlocks.Add(block);
                _currentBlocksShapeData.Add(shape);
                _currentBlocksSpriteData.Add(sprite);
                block.SetSpriteData(sprite);

                // 어드벤처 과일 모드일 때만 구 오버레이(개별 확률) 적용
                var mm = MapManager.Instance;
                if (mm.CurrentMode == GameMode.Adventure && mm?.CurrentMapData?.goalKind == MapGoalKind.Fruit)
                    TryApplyFruitOverlayToBlock(block);
            }

            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);
            Debug.Log("[Block Storage] 블록 생성 완성");
        }

        private int GetRandomImageIndex() => Random.Range(0, shapeImageSprites.Count);

        #endregion

        #region Fruit Overlay Algorithm
        // (생략 없음 — 네 최신본 그대로 유지)
        // ... 그대로 ...
        #endregion

        #region Game Check

        private bool IsGOCheckBlocked()
        {
            return
                AdStateProbe.IsRevivePending ||
                ReviveGate.IsArmed ||
                UIStateProbe.ReviveGraceActive ||
                UIStateProbe.ResultGuardActive ||
                UIStateProbe.IsReviveOpen ||
                (Time.realtimeSinceStartup < _goCheckFreezeUntil) ||
                _reviveAwaitFirstPlacement;
        }

        private void CheckGameOver()
        {
            int can = 0;
            foreach (var b in _currentBlocks) if (b && BlockSpawnManager.Instance.CanPlaceShapeData(b.GetShapeData())) can++;
            Debug.Log($"[GO-CHECK] placeableNow={can}/{_currentBlocks?.Count ?? 0} blocked={IsGOCheckBlocked()}");

            if (IsGOCheckBlocked())
            {
                Debug.Log("[GO-CHECK] skip: blocked");
                return;
            }
            StartCoroutine(CoCheckGameOverAfterEOF());
        }

        private IEnumerator CoCheckGameOverAfterEOF()
        {
            yield return new WaitForEndOfFrame();
            if (IsGOCheckBlocked()) yield break;

            if (_downedPending) { Debug.Log("[GO-CHECK] skip because downedPending"); yield break; }
            if (_currentBlocks == null || _currentBlocks.Count == 0) yield break;

            for (int i = 0; i < _currentBlocks.Count; i++)
            {
                var b = _currentBlocks[i];
                if (!b) continue;
                if (BlockSpawnManager.Instance.CanPlaceShapeData(b.GetShapeData()))
                    yield break;
            }

            Debug.Log("===== GAME OVER! 더 이상 배치할 수 있는 블록이 없습니다. =====");
            int score = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;

            _gameOverFired = true;
            _downedPending = true;

            bool canOffer = Game.Ads != null && Game.Ads.CanOfferReviveNow();

            if (canOffer)
            {
                AdStateProbe.IsRevivePending = true;
                ReviveGate.Arm(10f);
                Game.Bus?.PublishImmediate(new PlayerDowned(score, "no_place"));
            }
            else
            {
                AdStateProbe.IsRevivePending = false;
                ReviveGate.Disarm();
                ConfirmGameOverImmediate("no_place_no_revive");
            }
        }

        /// <summary>
        /// 광고 보상(onRewarded) 또는 UI 버튼에서 호출하면,
        /// GameOver 상태를 해제하고 Revive 웨이브를 손패에 적용하여 즉시 재개한다.
        /// </summary>
        public bool GenerateAdRewardWave()
        {
            // 0) 컨텍스트 가드
            if (oneRevivePerRun && _reviveUsed) { Debug.LogWarning("[Revive] 이미 사용"); return false; }
            if (!(_gameOverFired || _downedPending)) { Debug.LogWarning("[Revive] 다운 아님"); return false; }

            // 1) 웨이브 생성
            if (!BlockSpawnManager.Instance.TryGenerateReviveWave(reviveWaveCount, out var wave, out var fits)
                || wave == null || wave.Count == 0)
            {
                Debug.LogWarning("[Revive] 웨이브 생성 실패/빈 웨이브");
                return false;
            }

            // 2) 광고 큐 취소
            CancelQueuedInterstitialIfAny();

            // 3) 상태/가드 선세팅 (!!! 퍼블리시 서프레스 먼저)
            GameOverUtil.SuppressFor(3.0f, "revive-guard"); // ★ 추가
            _reviveUsed = true;
            _paused = false;
            Time.timeScale = 1f;

            _reviveAwaitFirstPlacement = true;
            UIStateProbe.ArmReviveGrace(2.5f);
            UIStateProbe.ArmResultGuard(2.5f);
            _goCheckFreezeUntil = Time.realtimeSinceStartup + 2.5f;

            // 4) 무장 해제 & 입력 언락
            ReviveGate.Disarm();
            AdStateProbe.IsRevivePending = false;
            Game.Bus?.PublishImmediate(new InputLock(false, "Revive"));

            // 5) 손패 적용
            var previews = ApplyReviveWave(wave);
            if (_currentBlocks == null || _currentBlocks.Count == 0)
            {
                Debug.LogError("[Revive] 적용 후 손패 비어있음 → Fallback Refill");
                GenerateAllBlocks();
            }

            // 6) 저장/훅
            ScoreManager.Instance?.OnHandRefilled();
            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);

            Debug.Log($"[Revive] 완료: hand={_currentBlocks.Count}, wave={wave.Count}, spawnSlots={blockSpawnPosList?.Count ?? -1}");

            // 7) 완료 이벤트
            Game.Bus?.PublishImmediate(new RevivePerformed());

            // 8) ResultGuard 자동 해제 예약은 유지
            MonoRunner.Run(Co_ReleaseResultGuard(2.5f));

            // 마지막에 다운 컨텍스트 플래그 리셋
            _gameOverFired = false;
            _downedPending = false;
            return true;
        }
        static IEnumerator Co_ReleaseResultGuard(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            UIStateProbe.DisarmResultGuard();
        }

        // === Revive 웨이브 적용 (손패 교체 + 스프라이트 수집) ===
        private List<Sprite> ApplyReviveWave(List<ShapeData> wave)
        {
            if (blockSpawnPosList == null || blockSpawnPosList.Count == 0 || shapesPanel == null)
            {
                Debug.LogError($"[Revive] SpawnPos/Panel 미지정: slots={blockSpawnPosList?.Count ?? -1}, panel={shapesPanel != null}");
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

        private void CancelQueuedInterstitialIfAny()
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
            yield return new WaitForSecondsRealtime(interstitialDelayAfterGameOver);

            if (Game.IsBound && Game.Ads != null && Game.Ads.IsInterstitialReady())
            {
                Game.Ads.ShowInterstitial(onClosed: () =>
                {
                    Game.Ads.Refresh();
                });
            }
            else
            {
                Game.Ads?.Refresh();
            }
        }

        public void OnBlockPlaced(Block placedBlock)
        {
            _currentBlocks.Remove(placedBlock);
            _currentBlocksShapeData.Remove(placedBlock.GetShapeData());
            _currentBlocksSpriteData.Remove(placedBlock.GetSpriteData());

            if (_reviveAwaitFirstPlacement)
            {
                _reviveAwaitFirstPlacement = false;
                Debug.Log("[GO-CHECK] revive guard lifted on first placement");
            }

            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);

            CheckGameOver();
            GridManager.Instance.ValidateGridConsistency();
            GameSnapShot.SaveGridSnapshot();

            if (_currentBlocks.Count == 0)
            {
                GenerateAllBlocks();
                ScoreManager.Instance?.OnHandRefilled();
            }
        }

        public void ConfirmGameOver()
        {
            if (!(_gameOverFired || _downedPending)) return;
            ConfirmGameOverImmediate(_lastReason);
        }

        #endregion

        #region Game Reset & Bus

        System.Action<ContinueGranted> _onContinue;
        int _lastScore;
        string _lastReason;

        public void SetDependencies(EventQueue bus)
        {
            _bus = bus;
            if (_subscribed) return;

            Debug.Log($"[Storage] Bind bus={_bus.GetHashCode()}");

            _bus.Subscribe<GameResetting>(OnGameResetting, replaySticky: false);
            _bus.Subscribe<GameResetRequest>(OnGameResetRequest, replaySticky: false);
            _bus.Subscribe<GridReady>(OnGridReady, replaySticky: true);
            _bus.Subscribe<GameEntered>(OnGameEntered, replaySticky: false);
            _bus.Subscribe<ReviveRequest>(OnReviveRequest, replaySticky: false);
            _bus.Subscribe<GiveUpRequest>(OnGiveUpRequest, replaySticky: false);
            _bus.Subscribe<GameDataChanged>(OnGameDataChanged, replaySticky: true);

            if (_onContinue == null)
            {
                _onContinue = OnContinueGranted;
                _bus.Subscribe(_onContinue, replaySticky: true);
            }

            _bus.Subscribe<RevivePerformed>(_ =>
            {
                GridManager.Instance?.ValidateGridConsistency();
                CheckGameOver();
            }, replaySticky: false);

            _subscribed = true;
        }

        public void ResetRuntime()
        {
            _gameOverFired = false;
            _downedPending = false;
            _reviveUsed = false;

            Time.timeScale = 1f;
            CancelQueuedInterstitialIfAny();

            _paused = true;

            _handSpawnedOnce = false;
            _handRestoredTried = false;
            _reviveAwaitFirstPlacement = false;

            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();
            _currentBlocksShapeData.Clear();
            _currentBlocksSpriteData.Clear();

            // 블럭 생성이 _paused로 인해 조기 종료되는 문제 방지
            _paused = false;
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
            Debug.Log($"[GO] ConfirmGameOverImmediate → publish GameOverConfirmed (final={final}, isNewBest={isNewBest})");

            _downedPending = false;
            _gameOverFired = false;

            ReviveGate.Disarm();

            GameOverUtil.PublishGameOverOnce(final, isNewBest, reason);
        }

        private void OnGameResetting(GameResetting _)
        {
            _gameOverFired = false;
            _downedPending = false;
            Time.timeScale = 1f;
            _reviveAwaitFirstPlacement = false;
            ReviveGate.Disarm();

            GameOverUtil.ResetAll("game-reset");
        }

        private void OnGameResetRequest(GameResetRequest e)
        {
            ResetRuntime();
            _reviveAwaitFirstPlacement = false;
            _skipRestoreThisBoot = true;
            StartCoroutine(CoRefillNextFrame());
        }

        private void OnReviveRequest(ReviveRequest _)
        {
            if (!(Game.Ads?.CanOfferReviveNow() ?? false))
            {
                Debug.Log("[Revive] not offerable (cooldown or not ready) → ConfirmGameOver()");
                ConfirmGameOver();
                return;
            }

            Debug.Log("[Revive] ReviveRequest received");
            if (!GenerateAdRewardWave())
            {
                Debug.LogWarning("[Revive] GenerateAdRewardWave failed → fallback ConfirmGameOver()");
                ConfirmGameOver();
            }
        }

        private void OnGiveUpRequest(GiveUpRequest _)
        {
            if (_gameOverFired || _downedPending)
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

        IEnumerator CoRefillNextFrame()
        {
            yield return null;
            RefillHand();
            yield return null;
            CheckGameOver();
        }

        void OnContinueGranted(ContinueGranted e)
        {
            GameOverUtil.CancelPending("continue-granted");
            GameOverUtil.SuppressFor(3f);

            if (_gameOverFired || _downedPending)
            {
                Debug.Log("[Revive] ContinueGranted received → Apply revive wave");
                if (!GenerateAdRewardWave())
                {
                    Debug.LogWarning("[Revive] GenerateAdRewardWave failed → fallback ConfirmGameOver()");
                    ConfirmGameOver();
                }
            }
            else
            {
                Debug.Log("[Revive] ContinueGranted ignored (not downed)");
            }
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
                blk.SpawnSlotIndex = slot;

                _currentBlocks.Add(blk);
                _currentBlocksShapeData.Add(shape);
                _currentBlocksSpriteData.Add(sprite);
            }

            return _currentBlocks.Count > 0;
        }

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

                if (!_skipRestoreThisBoot && sm != null && sm.TryRestoreBlocksToStorage(this))
                {
                    Debug.Log("[Hand] Restored from save → skip generation");
                    return;
                }
            }

            Debug.Log("[Hand] Refill → GenerateAllBlocks()");
            GenerateAllBlocks();
        }

        private void TryApplyFruitOverlayToBlock(Block block)
        {
            if (block == null) return;

            var mm = MapManager.Instance;
            if (mm == null || mm.CurrentMapData == null) return;
            // 과일 목표 모드일 때만
            if (mm.CurrentMapData.goalKind != MapGoalKind.Fruit) return;

            // 50% 확률(원하면 조절/삭제)
            if (UnityEngine.Random.value >= 0.5f)
            {
                ClearFruitOverlay(block);
                return;
            }

            // 활성 과일 후보 수집(0..4)
            var candidates = new List<int>(5);
            for (int i = 0; i < 5; i++)
            {
                if (!mm.IsFruitEnabled(i)) continue;
                int code = 201 + i;
                int remain = mm.GetFruitRemainingByCode(code);
                if (remain > 0) candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                ClearFruitOverlay(block);
                return;
            }

            // 후보 중 랜덤 선택
            int pickIdx = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var overlaySprite = mm.GetFruitSpriteByIndex(pickIdx);
            if (overlaySprite == null)
            {
                ClearFruitOverlay(block);
                return;
            }

            // 활성 ShapeSquare에만 과일 이미지 덮기
            var root = block.shapePrefab != null ? block.shapePrefab.transform : block.transform;
            var squares = root.GetComponentsInChildren<ShapeSquare>(true);
            foreach (var sq in squares)
            {
                if (sq == null) continue;
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

        #region Fruit Tracker Utils (Editor)
#if UNITY_EDITOR
        private void T(string msg) => UnityEngine.Debug.Log(msg);
        private void TDO(string title, System.Action body)
        {
            if (!string.IsNullOrEmpty(title)) UnityEngine.Debug.Log(title);
            body?.Invoke();
        }
#else
        [System.Diagnostics.Conditional("UNITY_EDITOR")] private void T(string msg) {}
        [System.Diagnostics.Conditional("UNITY_EDITOR")] private void TDO(string title, System.Action body) {}
#endif
        #endregion
    }
}
