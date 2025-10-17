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
            Debug.Log($"[Storage] GenerateAllBlocks: paused={_paused}, mode={MapManager.Instance?.CurrentMode}");

            if (MapManager.Instance?.CurrentMode == GameMode.Tutorial)
            {
                if (!HasAnyHand())
                {
                    ForceTutorialFirstHand();
                    TutorialFlags.MarkHandInjected();
                    MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);
                    Debug.Log("[Storage][TUT] Injected first 2x2 (by hand check) and locked. RETURN.");
                }
                else
                {
                    Debug.Log("[Storage][TUT] Hand already exists. Skip generation. RETURN.");
                }
                return;
            }
            Debug.Log("[Block Storage] : 블록 생성 시작");
            var blockManager = BlockSpawnManager.Instance;
            if (_paused) { Debug.LogWarning("[Storage] GenerateAllBlocks EARLY-RETURN: paused==true"); return; }

            // 클래식 시작 보드 생성은 클래식에서만
            var gm = GridManager.Instance;
            if (MapManager.Instance.CurrentMode == GameMode.Classic && gm != null && !gm.HasAnyOccupied())
            {
                Debug.Log("[Storage] Board empty (Classic) → build classic starting map");
                MapManager.Instance.StartNewClassicMap();
            }

            if (_paused) { Debug.LogWarning("[Storage] GenerateAllBlocks EARLY-RETURN: paused==true"); return; }

            // 손패 스냅샷은 현재 정책상 "클래식 제외"
            bool allowHandSnapshot = (MapManager.Instance?.CurrentMode == GameMode.Adventure);

            // 기존 손패 정리
            for (int i = 0; i < _currentBlocks.Count; i++)
                if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
            _currentBlocks.Clear();
            _currentBlocksShapeData.Clear();
            _currentBlocksSpriteData.Clear();

            // 저장에서 복원 시도 (튜토리얼 제외는 위에서 return 처리로 이미 빠져나감)
            var sm = MapManager.Instance?.saveManager;
            if (allowHandSnapshot && sm != null && sm.TryRestoreBlocksToStorage(this))
            {
                Debug.Log("[Storage] Restored blocks from save. Skip random generation.");
                return;
            }

            if (blockManager == null) { Debug.LogError("[Storage] Spawner null"); return; }

            var wave = blockManager.GenerateWaveV170(blockSpawnPosList.Count);
            if (wave == null || wave.Count == 0)
            {
                Debug.LogError("[Storage] Wave is null/empty. Rebuilding weights and retry.");
                wave = blockManager.GenerateWaveV170(blockSpawnPosList.Count);
                if (wave == null || wave.Count == 0) return;
            }

            var previewSprites = new List<Sprite>(blockSpawnPosList.Count);
            for (int k = 0; k < blockSpawnPosList.Count; k++) previewSprites.Add(null);

            for (int i = 0; i < blockSpawnPosList.Count; i++)
            {
                var shape = (i < wave.Count) ? wave[i] : null;
                if (shape == null) { Debug.LogWarning($"[Storage] wave[{i}] is null → skip this slot."); continue; }

                var go = Instantiate(blockPrefab, blockSpawnPosList[i].position, Quaternion.identity, shapesPanel);
                var block = go.GetComponent<Block>();
                if (block == null) { Debug.LogError("[Storage] Block component missing"); Destroy(go); continue; }

                block.SpawnSlotIndex = i;

                // 튜토리얼 아닐 때만 랜덤 스프라이트
                Sprite sprite = shapeImageSprites[GetRandomImageIndex()];

                block.SetSpriteData(sprite);
                previewSprites[i] = sprite;
                block.GenerateBlock(shape);
                _currentBlocks.Add(block);
                _currentBlocksShapeData.Add(shape);
                _currentBlocksSpriteData.Add(sprite);

                var mm = MapManager.Instance;
                if (mm.CurrentMode == GameMode.Adventure && mm?.CurrentMapData?.goalKind == MapGoalKind.Fruit)
                    TryApplyFruitOverlayToBlock(block);
            }

            if (allowHandSnapshot)
                MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);

            Debug.Log("[Block Storage] 블록 생성 완성 (hand snapshot " + (allowHandSnapshot ? "enabled" : "DISABLED for Classic") + ")");
        }

        private int GetRandomImageIndex() => Random.Range(0, shapeImageSprites.Count);

        #endregion

        #region Fruit Overlay Algorithm

        // 신규 과일 오버레이 알고리즘 (웨이브 단위)
        private void ApplyFruitWaveAlgorithm(List<Block> wave)
        {
            var mm = MapManager.Instance;
            if (mm == null) return;

            // --- (2) 기본/현재 목표, 활성 과일, 보드 내 과일 개수 집계 ---
            var activeCodes = mm.ActiveFruitCodes;          // e.g., [201,203,205]
            if (activeCodes == null || activeCodes.Count == 0) return;

            int TotalInitial() { int s = 0; foreach (var code in activeCodes) s += mm.GetInitialFruitGoalByCode(code); return s; }
            int TotalCurrent() { int s = 0; foreach (var code in activeCodes) s += mm.GetFruitGoalByCode(code); return s; }

            var boardCounts = CountFruitsOnBoard(activeCodes); // code→현재 보드 내 과일 칸 수

            int totalInit = Mathf.Max(1, TotalInitial());
            int totalCur = Mathf.Clamp(TotalCurrent(), 0, totalInit);
            // --- (5) 특수 타일 소환 조건 확률 ---
            // P = 0.60 - (totalCur/totalInit)*0.20
            float pCond = Mathf.Clamp01(0.60f - (totalCur / (float)totalInit * 0.20f));

            // (예외) 현재 목표 중 0이 된 타일 존재?
            bool anyZero = false;
            foreach (var code in activeCodes)
            {
                // 초기 목표가 1 이상이었던 과일이 현재 0이 된 경우만 '도달'로 취급
                int init = mm.GetInitialFruitGoalByCode(code);
                int cur = mm.GetFruitGoalByCode(code);
                if (init > 0 && cur <= 0) { anyZero = true; break; }
            }
            // 로그 뭉치
            TDO($"[FruitWave] a) 웨이브 과일 준비", () =>
            {
                T($" - activeCodes: {string.Join(",", activeCodes)}");
                T($" - goals init={totalInit}, cur={totalCur}, progress={1f - (totalCur / (float)totalInit):P0}");
                T($" - pCond={pCond:F2}, anyZero={anyZero}");
                var bc = string.Join(", ", activeCodes.Select(c => $"{c}:{boardCounts[c]}"));
                T($" - boardCounts: {bc}");
            });

            if (anyZero)
            {
                T("[FruitWave] b-i) 목표 0 도달 예외 → FullFill 반복");
                // ---- 5-1. "목표 0 도달" 예외처리: 대상 블록 선정 → 전체 타일 덮기 → 반복
                TrySpawnOnBlocks_FullFill(wave, activeCodes, mm, boardCounts, perBlockOnce: true, repeatWhileAvailable: false);
                return;
            }

            float roll = UnityEngine.Random.value;
            if (roll <= pCond)
            {
                T($"[FruitWave] b-ii) 확률 성공 roll={roll:F2} ≤ pCond={pCond:F2} → FullFill 1개");
                // ---- 5-2. "확률 성공": 대상 블록 1개에 전체 타일 덮기
                TrySpawnOnBlocks_FullFill(wave, activeCodes, mm, boardCounts,
                    perBlockOnce: true, repeatWhileAvailable: false);
            }
            else
            {
                T($"[FruitWave] b-iii) 확률 실패 roll={roll:F2} > pCond={pCond:F2} → RandomInside");
                // ---- 5-3. "확률 실패": 대상 블록 1개에 랜덤 위치 소환 → 필요시 추가 스폰/추가 블록
                TrySpawnOnBlocks_RandomInside(wave, activeCodes, mm, boardCounts, totalCur, totalInit);
            }
        }

        // === 보조: 보드 내 과일 카운트 ===
        private Dictionary<int, int> CountFruitsOnBoard(IReadOnlyList<int> activeCodes)
        {
            var dict = new Dictionary<int, int>();
            foreach (var code in activeCodes) dict[code] = 0;
            var gm = FindObjectOfType<GridManager>();
            if (gm == null) return dict;

            int rows = gm.rows, cols = gm.cols;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var cell = gm.gridSquares[r, c];
                    if (cell == null) continue;
                    int code = cell.BlockSpriteIndex; // 201..205일 때 과일
                    if (dict.ContainsKey(code)) dict[code]++;
                }
            return dict;
        }

        // === 가중치: 타일 선택 점수(10/8/6/4/2) = (현재 목표 많을수록 ↑) + (보드 내 개수 적을수록 ↑) ===
        private int ScoreFruitForSelection(int code, MapManager mm, Dictionary<int, int> boardCounts, IReadOnlyList<int> activeCodes)
        {
            // 현재 목표 순위(내림차순) → 10/8/6/4/2
            var sortedByGoal = new List<int>(activeCodes);
            sortedByGoal.Sort((a, b) => mm.GetFruitGoalByCode(b).CompareTo(mm.GetFruitGoalByCode(a)));
            int goalRank = Mathf.Clamp(sortedByGoal.IndexOf(code), 0, 4);
            int goalScore = 10 - (goalRank * 2);

            // 보드 내 개수 순위(오름차순: 적을수록 높음) → 10/8/6/4/2
            var sortedByBoard = new List<int>(activeCodes);
            sortedByBoard.Sort((a, b) => boardCounts[a].CompareTo(boardCounts[b]));
            int boardRank = Mathf.Clamp(sortedByBoard.IndexOf(code), 0, 4);
            int boardScore = 10 - (boardRank * 2);

            return goalScore + boardScore; // 20..4
        }

        // === 블록 후보: "이번 웨이브 생성 순서" 우선, 이미 과일 소환된 블록 제외 ===
        private Block PickTargetBlock(List<Block> wave)
        {
            foreach (var b in wave)
                if (!BlockHasAnyFruit(b)) return b;
            return null; // 전부 과일 있으면 없음
        }

        private bool BlockHasAnyFruit(Block b)
        {
            var root = b.shapePrefab ? b.shapePrefab.transform : b.transform;
            var squares = root.GetComponentsInChildren<ShapeSquare>(true);
            foreach (var s in squares)
            {
                if (s != null && s.gameObject.activeSelf)
                {
                    // 셀 자체가 활성 & 오버레이가 실제로 존재할 때만 true
                }
            }
            return false;
        }

        // === 전 블록 타일 덮기(5-1 / 5-2 공통) ===
        private void TrySpawnOnBlocks_FullFill(
            List<Block> wave, IReadOnlyList<int> activeCodes, MapManager mm,
            Dictionary<int, int> boardCounts, bool perBlockOnce, bool repeatWhileAvailable)
        {
            var usable = new List<Block>(wave);
            int spawnedBlocks = 0;

            while (usable.Count > 0)
            {
                // 웨이브별 FullFill 상한
                if (fruitFullFillCapPerWave > 0 && spawnedBlocks >= fruitFullFillCapPerWave)
                {
                    T($"[FruitWave] z) FullFill 캡 도달 → 종료 (spawnedBlocks={spawnedBlocks})");
                    break;
                }

                // FullFill 대상 블록 선정(이미 과일 있는 블록은 제외)
                var b = PickTargetBlock(usable);
                if (b == null)
                {
                    // 모든 블록에 과일 존재
                    T("[FruitWave] c) 대상 없음(모두 이미 과일 존재) → 종료");
                    break;
                }

                // 블록 내 활성 셀 수 확인(0이면 스킵)
                int tiles = CountActiveCells(b);
                if (tiles <= 0)
                {
                    T($"[FruitWave] c) 대상 블록 slot={b.SpawnSlotIndex}, tiles={tiles} (활성 셀 없음) → 스킵");
                    usable.Remove(b);
                    continue;
                }

                // 과일 코드 가중치 선택
                int pickCode = WeightedPickFruit(activeCodes, code => ScoreFruitForSelection(code, mm, boardCounts, activeCodes));
                int pickIdx = pickCode - 201;

                TDO($"[FruitWave] c) 대상 블록 slot={b.SpawnSlotIndex}, tiles={tiles}", () =>
                {
                    T($" - pickCode={pickCode} (idx={pickIdx}) | score={ScoreFruitForSelection(pickCode, mm, boardCounts, activeCodes)}");
                });

                // 블록의 모든 활성 셀에 과일 오버레이 적용
                ApplyFruitToBlock_AllCells(b, pickIdx);
                spawnedBlocks++;

                // 보드 카운트 동기화(이번 블록에 칠한 셀 수만큼 증가)
                if (tiles > 0)
                    boardCounts[pickCode] += tiles;

                // 한 블록만 하고 종료(5-2) vs 가능한 동안 반복(5-1)
                if (!repeatWhileAvailable)
                {
                    // FullFill 1개 완료 후 종료
                    T($"[FruitWave] d) FullFill 완료(1개) → 종료");
                    break;
                }

                // 다음 대상 탐색(현재 블록은 제외)
                usable.Remove(b);
            }

            T($"[FruitWave] z) FullFill 총 {spawnedBlocks}개 블록");
        }

        // === 확률 실패 분기(5-3): 블록 1개 랜덤 위치 소환 + 추가 스폰/추가 블록 확률 ===
        private void TrySpawnOnBlocks_RandomInside(
            List<Block> wave, IReadOnlyList<int> activeCodes, MapManager mm,
            Dictionary<int, int> boardCounts, int totalCur, int totalInit)
        {
            var b = PickTargetBlock(wave);
            if (b == null)
            {
                T("[FruitWave] c) 대상 없음 → 종료");
                return;
            }

            int code = WeightedPickFruit(activeCodes, c => ScoreFruitForSelection(c, mm, boardCounts, activeCodes));
            int idx = code - 201;

            TDO($"[FruitWave] c) 대상 블록 slot={b.SpawnSlotIndex}", () =>
                T($" - pickCode={code} (idx={idx}), score={ScoreFruitForSelection(code, mm, boardCounts, activeCodes)}")
            );

            // 최초 1개 랜덤 배치
            int placed = ApplyFruitToBlock_RandomCells(b, idx, minCount: 1, maxCount: 1);
            T($"[FruitWave] d) 첫 배치 placed={placed}");

            // d) 2개 미만이면(e) 추가 소환 확률
            if (placed < 2)
            {
                float pExtra = Mathf.Clamp01(1.00f - (totalCur / (float)totalInit * 0.30f));
                float roll = UnityEngine.Random.value;
                T($"[FruitWave] e) 추가 소환 roll={roll:F2} ≤ pExtra={pExtra:F2}? {(roll <= pExtra ? "Y" : "N")}");
                if (roll <= pExtra)
                {
                    int add = ApplyFruitToBlock_RandomCells(b, idx, 1, 1);
                    T($"[FruitWave] e) 추가 배치 add={add}");
                    placed += add;
                }
            }

            // f) 블록 추가 선정 확률
            int blocksWithFruit = wave.Count(BlockHasAnyFruit);
            float baseP = (blocksWithFruit <= 1) ? 1.0f : 0.8f;
            float pNext = Mathf.Clamp01(baseP - (totalCur / (float)totalInit * 0.20f));
            float r2 = UnityEngine.Random.value;
            T($"[FruitWave] f) 추가 블록 선정 roll={r2:F2} ≤ pNext={pNext:F2}? {(r2 <= pNext ? "Y" : "N")} (blocksWithFruit={blocksWithFruit})");
            if (r2 <= pNext)
            {
                // 다음 블록 1개에 동일 절차 반복(한 번만)
                var others = wave.Where(x => x != b).ToList();
                var b2 = PickTargetBlock(others);
                if (b2 != null)
                {
                    int code2 = WeightedPickFruit(activeCodes, c => ScoreFruitForSelection(c, mm, boardCounts, activeCodes));
                    int idx2 = code2 - 201;

                    T($"[FruitWave] f) 추가 블록 slot={b2.SpawnSlotIndex}, code={code2} (idx={idx2})");

                    int p2 = ApplyFruitToBlock_RandomCells(b2, idx2, 1, 1);
                    T($"[FruitWave] f) 추가 블록 첫 배치={p2}");

                    if (p2 < 2)
                    {
                        float pExtra2 = Mathf.Clamp01(1.00f - (totalCur / (float)totalInit * 0.30f));
                        float r3 = UnityEngine.Random.value;
                        T($"[FruitWave] f) 추가 블록의 추가 소환 roll={r3:F2} ≤ pExtra2={pExtra2:F2}? {(r3 <= pExtra2 ? "Y" : "N")}");
                        if (r3 <= pExtra2)
                        {
                            int add2 = ApplyFruitToBlock_RandomCells(b2, idx2, 1, 1);
                            T($"[FruitWave] f) 추가 블록의 추가 배치 add={add2}");
                        }
                    }
                }
                else T("[FruitWave] f) 추가 블록 없음");
            }
        }

        // === 유틸: 가중치 픽 ===
        private int WeightedPickFruit(IReadOnlyList<int> codes, System.Func<int, int> score)
        {
            int sum = 0;
            foreach (var c in codes) sum += Mathf.Max(1, score(c));
            int r = Random.Range(0, sum);
            foreach (var c in codes)
            {
                int w = Mathf.Max(1, score(c));
                if (r < w) return c;
                r -= w;
            }
            return codes[Random.Range(0, codes.Count)];
        }

        // === 유틸: 블록의 활성 칸 수 ===
        private int CountActiveCells(Block b)
        {
            var root = b.shapePrefab ? b.shapePrefab.transform : b.transform;
            int n = 0;
            foreach (var s in root.GetComponentsInChildren<ShapeSquare>(true))
                if (s != null && s.gameObject.activeSelf) n++;
            return n;
        }

        // === 실제 적용: 전체 칸 덮기 ===
        private void ApplyFruitToBlock_AllCells(Block b, int fruitIdx)
        {
            var mm = MapManager.Instance;
            var sprite = mm?.GetFruitSpriteByIndex(fruitIdx);
            var root = b.shapePrefab ? b.shapePrefab.transform : b.transform;
            int cnt = 0;
            foreach (var s in root.GetComponentsInChildren<ShapeSquare>(true))
                if (s != null && s.gameObject.activeSelf)
                { s.SetFruitImage(sprite); cnt++; }
            T($"[FruitWave] → ApplyAll(slot={b.SpawnSlotIndex}, idx={fruitIdx}, cells={cnt})");
        }

        // === 실제 적용: 랜덤 칸 n개만 배치 ===
        private int ApplyFruitToBlock_RandomCells(Block b, int fruitIdx, int minCount, int maxCount)
        {
            var mm = MapManager.Instance;
            var sprite = mm?.GetFruitSpriteByIndex(fruitIdx);
            if (sprite == null) return 0;

            var root = b.shapePrefab ? b.shapePrefab.transform : b.transform;
            var cells = root.GetComponentsInChildren<ShapeSquare>(true)
                            .Where(s => s != null && s.gameObject.activeSelf && s.FruitSprite == null)
                            .ToList();
            if (cells.Count == 0) { T($"[FruitWave] → ApplyRandom(slot={b.SpawnSlotIndex}) 대상 0"); return 0; }

            int k = Mathf.Clamp(UnityEngine.Random.Range(minCount, maxCount + 1), 1, cells.Count);
            for (int i = 0; i < k; i++)
            {
                int pick = UnityEngine.Random.Range(0, cells.Count);
                cells[pick].SetFruitImage(sprite);
                cells.RemoveAt(pick);
            }
            T($"[FruitWave] → ApplyRandom(slot={b.SpawnSlotIndex}, idx={fruitIdx}, k={k})");
            return k;
        }
        // 구 과일 오버레이 알고리즘 (블록 개별 단위 확률)
        private void TryApplyFruitOverlayToBlock(Block block)
        {
            if (block == null) return;

            var mm = MapManager.Instance;
            if (mm == null || mm.CurrentMapData == null) return;
            // 과일 목표 모드만
            if (mm.CurrentMapData.goalKind != MapGoalKind.Fruit) return;

            // 50% 확률 ( 임시 ), 100% 생성 원할 시 아래 주석 처리
            if (UnityEngine.Random.value >= 0.5f)
            {
                // 과일 적용 안 하는 케이스: 안전하게 오버레이 초기화 시도
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
        #endregion


        #region Game Check

        bool IsGOCheckBlocked()
        {
            bool blocked =
                AdStateProbe.IsRevivePending ||
                ReviveGate.IsArmed ||
                UIStateProbe.ReviveGraceActive ||
                UIStateProbe.ResultGuardActive ||
                UIStateProbe.IsReviveOpen ||
                (Time.realtimeSinceStartup < _goCheckFreezeUntil) ||
                _reviveAwaitFirstPlacement;

#if QA_BUILD || DEVELOPMENT_BUILD
    if (blocked)
    {
        Debug.Log($"[GO-CHECK] blocked by:" +
            $" revivePending={AdStateProbe.IsRevivePending}" +
            $", armed={ReviveGate.IsArmed}" +
            $", reviveOpen={UIStateProbe.IsReviveOpen}" +
            $", resultOpen={UIStateProbe.IsResultOpen}" +
            $", grace={UIStateProbe.ReviveGraceActive}" +
            $", resultGuard={UIStateProbe.ResultGuardActive}" +
            $", localFreeze={(Time.realtimeSinceStartup < _goCheckFreezeUntil)}" +
            $", awaitFirst={_reviveAwaitFirstPlacement}");
    }
#endif
            return blocked;
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

            bool adsReadyToOffer =
                Game.Ads != null &&
                Game.Ads.CanOfferReviveNow() &&
                (!oneRevivePerRun || !_reviveUsed);

            if (adsReadyToOffer)
            {
                _gameOverFired = true;
                _downedPending = true;

                AdStateProbe.IsRevivePending = true;
                ReviveGate.Arm(10f);

                Game.Bus?.PublishImmediate(new PlayerDowned(score, "no_place"));
            }
            else
            {
                _gameOverFired = false;
                _downedPending = false;

                AdStateProbe.IsRevivePending = false;
                ReviveGate.Disarm();

                ConfirmGameOverImmediate("no_place_no_revive");
            }

            if (Game.Ads != null && !Game.Ads.CanOfferReviveNow())
            {
                Debug.Log("[GO] revive not available → auto confirm");
                ConfirmGameOverImmediate("no_place_no_revive");
                yield break;
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

            // 3) 상태/가드 선세팅
            GameOverUtil.SuppressFor(3.0f, "revive-guard");
            _reviveUsed = true;
            _paused = false;
            Time.timeScale = 1f;

            _reviveAwaitFirstPlacement = true;
            UIStateProbe.ArmReviveGrace(2.5f);
            UIStateProbe.ArmResultGuard(2.5f);
            _goCheckFreezeUntil = Time.realtimeSinceStartup + 2.5f;

            MonoRunner.Run(Co_AutoUnblockGoCheck(3.0f));

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

        public void OnBlockPlaced(Block placedBlock)
        {
            if (_reviveAwaitFirstPlacement)
            {
                _reviveAwaitFirstPlacement = false;
                Debug.Log("[Revive] First placement → unblock GO checks");

                UIStateProbe.DisarmReviveGrace();
            }

            _currentBlocks.Remove(placedBlock);
            _currentBlocksShapeData.Remove(placedBlock.GetShapeData());
            _currentBlocksSpriteData.Remove(placedBlock.GetSpriteData());

            bool switchedToClassicJustNow = false;

            // 튜토리얼 첫 배치 → ‘보드/점수/맵 유지’ 상태로 Classic 전환
            if (MapManager.Instance.CurrentMode == GameMode.Tutorial
                && TutorialFlags.WasTutorialHandInjected()
                && !TutorialFlags.WasFirstPlacement())
            {
                TutorialFlags.MarkFirstPlacement();
                switchedToClassicJustNow = true;

                var map = MapManager.Instance;

                // 모드만 Classic으로 전환(리셋/맵 재생성 X)
                map.SetGameMode(GameMode.Classic);
                map.SetGoalKind(MapGoalKind.None);
                StageManager.Instance?.SetObjectsByGameModeNGoalKind(GameMode.Classic, MapGoalKind.None);

                // HUD는 이미 ON 상태지만 한 번 더 안전하게
                Game.Bus?.PublishSticky(new PanelToggle("HUDScore", true), alsoEnqueue: false);
                Game.Bus?.PublishSticky(new PanelToggle("Score", true), alsoEnqueue: false);
                Game.Bus?.PublishImmediate(new PanelToggle("HUDScore", true));
                Game.Bus?.PublishImmediate(new PanelToggle("Score", true));

                // 현재 보드/점수 상태를 Classic 세이브로 표시
                var sm = map.saveManager;
                if (sm?.gameData != null)
                {
                    sm.gameData.isClassicModePlaying = true;
                    sm.gameData.currentMapLayout = GridManager.Instance.ExportLayoutCodes();
                    sm.gameData.currentScore = ScoreManager.Instance?.Score ?? 0;
                    sm.gameData.currentCombo = ScoreManager.Instance?.Combo ?? 0;
                    sm.SaveGame();
                }

                Game.Bus?.PublishImmediate(new GameEntered(GameMode.Classic));
            }

            MapManager.Instance?.saveManager?.SaveCurrentBlocksFromStorage(this);

            CheckGameOver();
            GridManager.Instance.ValidateGridConsistency();
            GameSnapShot.SaveGridSnapshot();

            if (_currentBlocks.Count == 0 && !switchedToClassicJustNow)
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
        // --- Tutorial helpers (BlockStorage 안에 추가) ---
        private ShapeData MakeSquare2x2()
        {
            var sd = ScriptableObject.CreateInstance<ShapeData>();

            if (sd.rows == null || sd.rows.Length != 5) sd.rows = new ShapeRow[5];
            for (int r = 0; r < 5; r++)
            {
                if (sd.rows[r] == null) sd.rows[r] = new ShapeRow();
                for (int c = 0; c < 5; c++) sd.rows[r].columns[c] = false;
            }

            sd.rows[2].columns[2] = true; sd.rows[2].columns[3] = true;
            sd.rows[3].columns[2] = true; sd.rows[3].columns[3] = true;

            sd.activeBlockCount = 4;
            sd.chanceForSpawn = 4;
            sd.difficulty = 0;
            sd.Id = "TUTORIAL_2x2";
            return sd;
        }

        private void ForceTutorialFirstHand()
        {
            if (_currentBlocks != null)
            {
                for (int i = _currentBlocks.Count - 1; i >= 0; i--)
                    if (_currentBlocks[i]) Destroy(_currentBlocks[i].gameObject);
                _currentBlocks.Clear();
                _currentBlocksShapeData.Clear();
                _currentBlocksSpriteData.Clear();
            }

            if (blockSpawnPosList == null || blockSpawnPosList.Count == 0 || blockPrefab == null || shapesPanel == null)
            {
                Debug.LogError("[Storage][TUT] ForceTutorialFirstHand prerequisites missing.");
                return;
            }

            int mid = Mathf.Clamp(blockSpawnPosList.Count / 2, 0, blockSpawnPosList.Count - 1);
            var shape = MakeSquare2x2();

            var go = Instantiate(blockPrefab, blockSpawnPosList[mid].position, Quaternion.identity, shapesPanel);
            var block = go.GetComponent<Block>();
            if (!block) { Destroy(go); Debug.LogError("[Storage][TUT] Block component missing."); return; }

            block.SpawnSlotIndex = mid;

            Sprite sprite = (shapeImageSprites != null && shapeImageSprites.Count > 0) ? shapeImageSprites[0] : null;

            block.SetSpriteData(sprite);
            block.GenerateBlock(shape);

            _currentBlocks.Add(block);
            _currentBlocksShapeData.Add(shape);
            _currentBlocksSpriteData.Add(sprite);

            Debug.Log($"[Storage][TUT] ForceTutorialFirstHand() at slot={mid}");
        }
        IEnumerator Co_AutoUnblockGoCheck(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (_reviveAwaitFirstPlacement)
            {
                _reviveAwaitFirstPlacement = false;
                Debug.Log("[Revive] Auto-unblock GO checks after delay");
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
