using _00.WorkSpace.GIL.Scripts.Blocks;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Messages;
using _00.WorkSpace.GIL.Scripts.Shapes;
using _00.WorkSpace.GIL.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class MapManager : MonoBehaviour, IManager
    {
        public static MapManager Instance;

        [Header("Save Tutorial")]
        public SaveManager saveManager;
        public GameMode CurrentMode = GameMode.Tutorial;
        public MapGoalKind CurrentGoalKind = MapGoalKind.Score;

        [Header("Map Runtime")]
        [SerializeField] private int defaultMapIndex = 0;
        [SerializeField] private GameObject grid;
        private MapData[] _mapList;

        [SerializeField] private MapData _currentMapData; // 현재 로딩된 맵 (디버그 확인용)
        public MapData CurrentMapData => _currentMapData; // 읽기 전용 접근자
        [SerializeField] private bool[] _fruitEnabledRuntime = new bool[5];
        [SerializeField] private int[] _fruitGoalsRuntime = new int[5];
        [SerializeField] private int[] _fruitGoalsInitial = new int[5]; // 0..4, 스테이지 입장 시 스냅샷
        [SerializeField] private List<int> _activeFruitCodes = new();           // 201..205
        [SerializeField] private Dictionary<int, int> _fruitGoalsByCode = new(); // key:201..205
        [SerializeField] private bool _fruitAllClearedAnnounced = false;

        public IReadOnlyList<int> ActiveFruitCodes => _activeFruitCodes;
        public bool IsFruitEnabled(int idx) => (uint)idx < _fruitEnabledRuntime.Length && _fruitEnabledRuntime[idx];
        public int GetFruitGoal(int idx) => (uint)idx < _fruitGoalsRuntime.Length ? _fruitGoalsRuntime[idx] : 0;
        public bool IsFruitCodeActive(int code) => _activeFruitCodes.Contains(code);
        public int GetFruitGoalByCode(int code) => _fruitGoalsByCode.TryGetValue(code, out var v) ? v : 0;

        private const int FruitBaseCode = 201;
        private const int FruitCount = 5;
        private bool _shownOnce;

        [SerializeField] private int[] fruitCurrentsRuntime = new int[FruitCount];


        // UI 캐시
        private AdventureFruitProgress _fruitUI;

        private readonly Dictionary<int, Sprite> _codeToSprite = new();
        private static readonly Regex s_CodeRegex = new(@"^\s*(\d+)(?=_)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private Sprite[] _blockSpriteList; // 단순 블록 스프라이트 리스트
        private Sprite[] _fruitSpriteList; // 블록 + 과일 스프라이트 리스트
        private Sprite[] _fruitBackgroundSprite; // 블록 + 과일 스프라이트의 블록 배경화면 이미지 리스트, 현재는 단 하나

        private bool _codeMapsBuilt = false;
        bool _isApplyingMap;
        int _tutorialApplyTicket = 0;
        private bool _pendingClassicEnter;
        private ClassicEnterPolicy _pendingClassicPolicy;
        [SerializeField] private bool _scoreGoalClearedAnnounced = false;

        bool _pendingTutorialApply;
        int _pendingIndex;
        private Action<int> _scoreProgressHandler;

        private Action<GridReady> _classicEnterHandler;
        private Action<GridReady> _onGridReadyTutorial;

        private EventQueue _bus;

        private void Awake()
        {
            Debug.Log("[MapManager] : 튜토리얼 정보 초기화는 F1");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); return;
            }
            Instance = this;

            if (_mapList == null) LoadMapData();
            if (saveManager) saveManager.LoadGame();
            else Debug.LogWarning("[MapManager] saveManager missing — LoadGame skipped");
            Debug.Log("[MapManager] Loaded saveData");

            _bus = Game.Bus;

        }

        private void Start()
        {
            // 세이브가 로드된 직후 모드를 반영
            if (saveManager != null)
            {
                // 즉시 한 번
                ApplySavedGameMode(saveManager.gameData);
                // 이후에도 로드가 다시 일어날 수 있으니 구독
                saveManager.AfterLoad += ApplySavedGameMode;
            }
        }

        private void OnDestroy()
        {
            if (saveManager != null)
                saveManager.AfterLoad -= ApplySavedGameMode;
        }

        public int Order => 13;
        public void PreInit() { }

        public void Init()
        {
            LoadMapData();
            BuildCodeMaps();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Debug.Log("[MapManager] : 튜토리얼 정보 초기화");
                SetGameMode(GameMode.Tutorial);
            }
        }
        void OnEnable()
        {
            if (PendingEnterIntent.TryConsume(out var intent))
            {
                _shownOnce = false;
                SetGameMode(intent.mode);

                if (intent.mode == GameMode.Tutorial)
                {
                    RequestTutorialApply();
                    Debug.Log("[Map] Tutorial apply requested (via PendingEnterIntent)");
                }
                else
                {
                    // Classic: 저장 강제 로드
                    if (intent.forceLoadSave)
                    {
                        RequestClassicEnter(MapManager.ClassicEnterPolicy.ForceLoadSave);
                    }
                    else
                    {
                        // 필요 시 다른 정책 분기 (없으면 위 한 줄로 충분)
                        RequestClassicEnter(MapManager.ClassicEnterPolicy.ForceLoadSave);
                    }
                    Debug.Log("[Map] Classic enter requested (ForceLoadSave via PendingEnterIntent)");
                }
            }
            Game.Bus?.Subscribe<GameOverConfirmed>(OnGameOverConfirmed, replaySticky: false);

            Game.Bus?.Subscribe<GameResetRequest>(_ =>
            {
                _shownOnce = false;
            }, replaySticky: false);
        }

        void OnDisable()
        {
            Game.Bus?.Unsubscribe<GameOverConfirmed>(OnGameOverConfirmed);
            Game.Bus?.Unsubscribe<GameResetRequest>(null);
        }

        private void ApplySavedGameMode(GameData data)
        {
            var loaded = (saveManager != null) ? saveManager.GetGameMode() : GameMode.Tutorial;
            CurrentMode = loaded;
            Debug.Log($"[MapManager] Loaded GameMode: {CurrentMode}");
        }

        /// <summary>
        /// 게임 모드 변경, 바꿀 때 이걸 쓰기(추적 용이함)
        /// </summary>
        public void SetGameMode(GameMode mode)
        {
            var prev = CurrentMode;
            CurrentMode = mode;

            if (saveManager != null)
                saveManager.SetGameMode(mode, save: true);

            Debug.Log($"[MapManager] 게임 모드 변경 : {prev} -> {CurrentMode}");
        }

        public void SetGoalKind(MapGoalKind kind)
        {
            var prev = CurrentGoalKind;
            CurrentGoalKind = kind;

            Debug.Log($"[MapManager] 어드벤쳐 입장 모드 변경 : {prev} -> {CurrentGoalKind}");
        }


        // 튜토리얼 종료시 호출 지점에서:
        public void OnTutorialCompleted()
        {
            SetGameMode(GameMode.Classic);
            // TODO : 튜토리얼을 진행하고 나서 원하는 진입 로직 호출
        }

        public void PostInit() { }

        private void LoadMapData()
        {
            var g = GDS.I;
            _mapList = g.Maps.OrderBy(m => m.mapIndex).ToArray(); // mapIndex 순 정렬
            _blockSpriteList = g.BlockSprites;
            _fruitSpriteList = g.BlockWithFruitSprites;
            _fruitBackgroundSprite = g.FruitBackgroundSprites;
        }

        private void BuildCodeMaps()
        {
            _codeToSprite.Clear();

            void AddAll(Sprite[] arr)
            {
                if (arr == null) return;
                foreach (var s in arr)
                {
                    if (!s) continue;
                    var m = s_CodeRegex.Match(s.name);
                    if (!m.Success) { Debug.LogWarning($"[MapManager] 선행 숫자 없음: {s.name}"); continue; }
                    int code = int.Parse(m.Groups[1].Value);
                    if (_codeToSprite.ContainsKey(code))
                    {
                        Debug.LogWarning($"[MapManager] 코드 중복 {code}: {_codeToSprite[code]?.name} vs {s.name}");
                        continue; // 중복은 최초 항목 유지
                    }
                    _codeToSprite.Add(code, s);
                }
            }

            // 리소스들에서 전부 스캔
            AddAll(_blockSpriteList);
            AddAll(_fruitSpriteList);
            // 필요시 AddAll(_fruitBackgroundSprite); // 보드 타일이면 제외
        }

        public Sprite GetSpriteForCode(int code)
        {
            return code > 0 && _codeToSprite.TryGetValue(code, out var s) ? s : null;
        }

        public int GetCodeForSprite(Sprite s)
        {
            if (!s) return 0;
            var m = s_CodeRegex.Match(s.name);
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        public static bool IsFruitCode(int code) => code >= 200 && code < 300;

        // index = 0..4  (code로 받으려면 아래 GetFruitSpriteByCode 사용)
        public Sprite GetFruitSpriteByIndex(int idx)
        {
            if (_fruitSpriteList == null || idx < 0 || idx >= _fruitSpriteList.Length) return null;
            return _fruitSpriteList[idx];
        }

        public Sprite GetFruitSpriteByCode(int code)
        {
            // 201번부터 시작이라 제거하고 시작함
            int idx = code - 201;
            return GetFruitSpriteByIndex(idx);
        }

        /// <summary>
        /// 맵 데이터를 토대로 그리드를 칠하기, 게임 시작 -> 블럭 생성 이전에 써야 할듯
        /// 게임 시작 위치를 정확히 모르겠어서 어디서든 코드를 사용하여 바로 붙일 수 있게 해야함.
        /// _mapList의 [i]번째 데이터를 불러와서 안의 layout 상태에 따라 그리드를 칠하기
        /// </summary>
        /// <param name="index">생성할 맵 Index, 0일 경우 튜토리얼, 1 이상일 경우 스테이지 번호</param>
        // 버튼/Start에서 한 줄 사용
        public void SetMapDataToGrid(int index = 0, bool publishGridReady = true)
        {
            if (_isApplyingMap) { Debug.LogWarning("[MapManager] Re-entrant SetMapDataToGrid blocked"); return; }
            _isApplyingMap = true;
            try
            {
                if (_mapList == null || _mapList.Length == 0) LoadMapData();
                if (_mapList == null || _mapList.Length == 0) { Debug.LogError("[MapManager] Maps not found."); return; }

                int idx = Mathf.Clamp(index, 0, _mapList.Length - 1);

                // Adventure 모드에서는 0을 기본맵으로 바꾸지 않는다
                if (idx == 0 && CurrentMode != GameMode.Adventure)
                    idx = defaultMapIndex;

                var map = _mapList[idx];
                if (!map) { Debug.LogError($"[MapManager] MapData[{idx}] is null."); return; }

                ApplyMapToCurrentGrid(map, publishGridReady);
                Debug.Log($"[MapManager] SetMapDataToGrid 완료: index={index}, resolvedIdx={idx}, mode={CurrentMode}");
                if (CurrentMode == GameMode.Classic)
                    StartCoroutine(RestoreScoreNextFrame());
            }
            finally { _isApplyingMap = false; }
        }

        /// <summary>
        /// 현재 클래식 모드 맵 상태를 불러오기, SaveManager에 있는 맵 정보를 그대로 집어넣기
        /// </summary>
        public void LoadCurrentClassicMap()
        {
            var gm = GridManager.Instance;
            if (!gm)
            {
                Debug.LogError("[MapManager] LoadCurrentClassicMap: GridManager missing");
                return;
            }

            var layout = saveManager?.gameData?.currentMapLayout;
            if (layout == null || layout.Count == 0)
            {
                Debug.LogWarning("[MapManager] LoadCurrentClassicMap: no saved layout -> fallback StartNewClassicMap()");
                StartNewClassicMap();
                return;
            }

            // 길이 보정 (패딩/트림)
            int expected = gm.rows * gm.cols;
            if (layout.Count != expected)
            {
                if (layout.Count < expected)
                    layout = layout.Concat(Enumerable.Repeat(0, expected - layout.Count)).ToList();
                else
                    layout = layout.Take(expected).ToList();
            }

            Debug.Log($"[MM] LoadCurrentClassicMap: layoutCount={saveManager?.gameData?.currentMapLayout?.Count}");

            // ImportLayoutCodes로 바로 반영 (내부에서 SetCellOccupied 호출)
            if (!gm.ImportLayoutCodes(layout, repaint: true, doValidate: true))
            {
                Debug.Log($"[MM] After Import: gm.HasAnyOccupied()={GridManager.Instance.HasAnyOccupied()}");
                StartNewClassicMap();
                return;
            }

            StartCoroutine(RestoreScoreNextFrame());
            StartCoroutine(Co_PostEnterSignals(GameMode.Classic));
        }

        private void ApplyMapToCurrentGrid(MapData map, bool publishGridReady = true)
        {
            Debug.Log("[MapManager] ApplyMapToCurrentGrid 호출됨");
            var gm = GridManager.Instance;
            if (!gm || gm.gridSquares == null) return;

            gm.ResetBoardToEmpty();

            if (!_codeMapsBuilt) { BuildCodeMaps(); _codeMapsBuilt = true; }

            int rows = Mathf.Min(map.rows, gm.rows);
            int cols = Mathf.Min(map.cols, gm.cols);

            StringBuilder sb = new();

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int code = map.layout[(r * map.cols) + c];
                    if (code <= 0)
                    {
                        gm.SetCellOccupied(r, c, false);
                        continue;
                    }

                    var sprite = GDS.I?.GetBlockSpriteByLayoutCode(code);
                    if (!sprite && _codeToSprite != null)
                        _codeToSprite.TryGetValue(code, out sprite);

                    // 200이 넘을 경우 ( 과일 블록일 경우 ) 강제 매핑.
                    if (!sprite && code >= 200 && _fruitSpriteList != null && _fruitSpriteList.Length > 0)
                    {
                        int i = Mathf.Clamp(code - 201, 0, _fruitSpriteList.Length - 1);
                        sprite = _fruitSpriteList[i];
                    }
                    // 과일 블록일 경우의 5번째 파라미터 isFruit : true를 추가
                    sb.Append($"[MapManager] Applying cell ({r},{c}): code={code}, sprite={(sprite != null ? sprite.name : "null")}");
                    if (code >= 200)
                        gm.SetCellOccupied(r, c, true, sprite, isFruit: true);
                    else
                        gm.SetCellOccupied(r, c, /*occupied:*/ true, sprite);
                }

            Debug.Log(sb.ToString());

            gm.ValidateGridConsistency();

            if (!publishGridReady) return;

            Game.Bus?.PublishSticky(new GridReady(gm.rows, gm.cols), alsoEnqueue: false);
            // Game.Bus?.PublishImmediate(new GridReady(gm.rows, gm.cols));
            GridManager.Instance.PublishGridReady();
        }

        // 블록 규칙성
        // 0     : 빈칸(비활성)
        // 101..105  : 일반 블록 
        // 201..205 : 일반 + 과일 블록
        private (string desc, bool isActive) DecodeLayoutCode(int code)
        {
            if (code == 0) return ("빈칸(구멍)", false);
            var sprite = GetSpriteForCode(code);
            if (sprite != null) return ($"코드 {code} ({sprite.name})", true);
            return ($"알 수 없는 코드 {code}", false);
        }
        // 보드 크기 고정(문서 기준 8x8)
        private const int BOARD_SIZE = 8;

        /// <summary>
        /// 클래식 모드: 시작 보드를 일부 채워둔다.
        /// 규칙: 보드를 4분면(4x4)으로 나누고 코너에서 가까운 순으로 스캔,
        /// 난이도 3~4 블록만 후보, 배치 시 '예약'하여 겹침 방지,
        /// 타일 합이 minTotalTiles 이상 될 때까지 반복.
        /// </summary>
        public void GenerateClassicStartingMap(int minTotalTiles = 30, int maxPlacements = 8, bool avoidInstantLineClear = true, int perQuadrantTileCap = 8)
        {
            if (CurrentMode != GameMode.Classic) return;

            var gm = GridManager.Instance;
            var grid = gm?.gridSquares;
            if (grid == null) { Debug.LogError("[ClassicStart] gridSquares is null"); return; }

            // 1) 소환 풀: 난이도 3~4 (없으면 전체)
            var spawner = BlockSpawnManager.Instance;
            if (spawner == null || spawner.shapeData == null || spawner.shapeData.Count == 0)
            {
                Debug.LogError("[ClassicStart] BlockSpawnManager/shapeData missing");
                return;
            }
            var pool = spawner.shapeData.Where(s => s != null && s.difficulty >= 3 && s.difficulty <= 4).ToList();
            if (pool.Count == 0) pool = spawner.shapeData.ToList();

            // 2) 예약 보드(점유도)
            var occ = SnapshotOccupied();

            // 3) 4분면 정의
            var quads = new[]
            {
                new Quad(0,3,0,3,  new Vector2Int(0,0)),   // Q1
                new Quad(4,7,0,3,  new Vector2Int(7,0)),   // Q2
                new Quad(0,3,4,7,  new Vector2Int(0,7)),   // Q3
                new Quad(4,7,4,7,  new Vector2Int(7,7)),   // Q4
            };

            var placed = new List<(ShapeData s, int ox, int oy)>();
            int sumTiles = 0;
            int[] quadTileSums = new int[4];

            for (int qi = 0; qi < quads.Length; qi++)
            {
                var q = quads[qi];

                foreach (var c in EnumerateCellsInQuad(q))
                {
                    if (placed.Count >= maxPlacements && sumTiles >= minTotalTiles) break;
                    if (occ[c.y, c.x]) continue;

                    var candidates = new List<(ShapeData s, int ox, int oy)>();
                    foreach (var s in pool)
                    {
                        if (!TryGetShapeBounds(s, out var w, out var h)) continue;
                        if (c.x + w > BOARD_SIZE || c.y + h > BOARD_SIZE) continue;
                        if (!CanPlaceAt(occ, s, c.x, c.y)) continue;
                        if (avoidInstantLineClear && MakesFullLineIfStamped(occ, s, c.x, c.y)) continue;

                        candidates.Add((s, c.x, c.y));
                    }
                    if (candidates.Count == 0) continue;

                    bool mustReach = (placed.Count + 1 == maxPlacements) && (sumTiles < minTotalTiles);
                    var pick = mustReach
                        ? candidates.OrderByDescending(t => GetTileCount(t.s)).First()
                        : candidates[UnityEngine.Random.Range(0, candidates.Count)];

                    int tcount = GetTileCount(pick.s);

                    // 분면 한도 검사: 초과면 이 블록은 패스하고 다음 분면으로
                    if (quadTileSums[qi] + tcount > perQuadrantTileCap)
                        break;

                    // 정상 배치(예약판에만 찍기)
                    Stamp(occ, pick.s, pick.ox, pick.oy, true);
                    placed.Add(pick);
                    sumTiles += tcount;
                    quadTileSums[qi] += tcount;
                }
            }

            // 4) 실제 보드에 반영(시각/상태) — 반드시 GridManager 경유
            ApplyPlacementsToGridViaGridManager(placed);

            Debug.Log($"[ClassicStart] placed={placed.Count}, sumTiles={sumTiles}");
        }

        private void ApplyPlacementsToGridViaGridManager(List<(ShapeData s, int ox, int oy)> placed)
        {
            var gm = GridManager.Instance;
            if (gm == null) return;
            var sprite = _blockSpriteList != null && _blockSpriteList.Length > 0 ? _blockSpriteList[Random.Range(0, _blockSpriteList.Length)]
                : null;
            foreach (var (shape, ox, oy) in placed)
            {
                foreach (var p in EnumerateShapeCells(shape)) // shape 로컬 타일 좌표들(=true 칸)
                {
                    int r = oy + p.y;
                    int c = ox + p.x;

                    gm.SetCellOccupied(r, c, true, sprite); // 반드시 이 API 사용
                }
            }
        }

        private struct Quad
        {
            public int xMin, xMax, yMin, yMax; public Vector2Int corner;
            public Quad(int xMin, int xMax, int yMin, int yMax, Vector2Int corner)
            { this.xMin = xMin; this.xMax = xMax; this.yMin = yMin; this.yMax = yMax; this.corner = corner; }
        }

        // 활성 칸들(bounding box로 trim된 로컬 좌표) 열거
        private IEnumerable<Vector2Int> EnumerateShapeCells(ShapeData s)
        {
            if (!TryGetShapeBounds(s, out int w, out int h, out int minX, out int minY))
                yield break;

            for (int y = 0; y < h; y++)
            {
                var row = s.rows[minY + y];
                if (row?.columns == null) continue;

                for (int x = 0; x < w; x++)
                {
                    if (row.columns[minX + x])
                        yield return new Vector2Int(x, y); // trim된 로컬 좌표(0..w-1, 0..h-1)
                }
            }
        }

        // shape의 활성 영역 bounding box
        private bool TryGetShapeBounds(ShapeData s, out int w, out int h)
            => TryGetShapeBounds(s, out w, out h, out _, out _);

        private bool TryGetShapeBounds(ShapeData s, out int w, out int h, out int minX, out int minY)
        {
            w = h = 0; minX = 5; minY = 5; int maxX = -1, maxY = -1;
            if (s == null || s.rows == null || s.rows.Length != 5) return false;

            for (int y = 0; y < 5; y++)
            {
                var row = s.rows[y];
                if (row?.columns == null || row.columns.Length != 5) continue;

                for (int x = 0; x < 5; x++)
                {
                    if (!row.columns[x]) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0 || maxY < 0) return false; // 활성 칸 없음
            w = maxX - minX + 1;
            h = maxY - minY + 1;
            return true;
        }

        // 현재 보드 점유 스냅샷
        private bool[,] SnapshotOccupied()
             => GridManager.Instance.SnapshotOccupied();

        // (ox,oy)에 배치 가능?
        private bool CanPlaceAt(bool[,] occ, ShapeData s, int ox, int oy)
        {
            if (!TryGetShapeBounds(s, out int w, out int h)) return false;

            int rows = occ.GetLength(0);
            int cols = occ.GetLength(1);

            // 경계 체크
            if (ox < 0 || oy < 0 || ox + w > cols || oy + h > rows) return false;

            // 점유 겹침 체크
            foreach (var p in EnumerateShapeCells(s))
            {
                int r = oy + p.y;
                int c = ox + p.x;
                if (occ[r, c]) return false;
            }
            return true;
        }

        // 임시로 찍었을 때 즉시 라인 클리어가 생기는지 검사
        private bool MakesFullLineIfStamped(bool[,] occ, ShapeData s, int ox, int oy)
        {
            int rows = occ.GetLength(0);
            int cols = occ.GetLength(1);

            // 영향 받는 행/열만 검사(최소화)
            var affectedRows = new HashSet<int>();
            var affectedCols = new HashSet<int>();
            foreach (var p in EnumerateShapeCells(s))
            {
                affectedRows.Add(oy + p.y);
                affectedCols.Add(ox + p.x);
            }

            // 행 검사
            foreach (var rr in affectedRows)
            {
                bool full = true;
                for (int c = 0; c < cols; c++)
                {
                    bool filled = occ[rr, c];
                    // stamping 후 상태 반영
                    foreach (var p in EnumerateShapeCells(s))
                        if (rr == oy + p.y && c == ox + p.x) { filled = true; break; }

                    if (!filled) { full = false; break; }
                }
                if (full) return true;
            }

            // 열 검사
            foreach (var cc in affectedCols)
            {
                bool full = true;
                for (int r = 0; r < rows; r++)
                {
                    bool filled = occ[r, cc];
                    foreach (var p in EnumerateShapeCells(s))
                        if (r == oy + p.y && cc == ox + p.x) { filled = true; break; }

                    if (!filled) { full = false; break; }
                }
                if (full) return true;
            }

            return false;
        }

        // 예약판(occ)에 실제로 찍거나 지움
        private void Stamp(bool[,] occ, ShapeData s, int ox, int oy, bool value)
        {
            foreach (var p in EnumerateShapeCells(s))
                occ[oy + p.y, ox + p.x] = value;
        }

        private IEnumerable<Vector2Int> EnumerateCellsInQuad(Quad q)
        {
            var cells = new List<Vector2Int>();
            for (int y = q.yMin; y <= q.yMax; y++)
                for (int x = q.xMin; x <= q.xMax; x++)
                    cells.Add(new Vector2Int(x, y));

            // 코너에서 가까운 순
            cells.Sort((a, b) =>
            {
                int da = Mathf.Abs(a.x - q.corner.x) + Mathf.Abs(a.y - q.corner.y);
                int db = Mathf.Abs(b.x - q.corner.x) + Mathf.Abs(b.y - q.corner.y);
                if (da != db) return da.CompareTo(db);
                int cx = a.x.CompareTo(b.x);
                return cx != 0 ? cx : a.y.CompareTo(b.y);
            });
            return cells;
        }

        private void ClearBoard(GridSquare[,] grid)
        {
            var gm = GridManager.Instance;
            if (!gm) return;

            for (int r = 0; r < gm.rows; r++)
                for (int c = 0; c < gm.cols; c++)
                    gm.SetCellOccupied(r, c, false); // index/sprite/state 모두 초기화
        }

        private static int GetTileCount(ShapeData s)
        {
            // s.tileCount가 있다면 그 값 사용, 없으면 rows로 계산
            if (s.GetType().GetField("tileCount") != null)
                return (int)s.GetType().GetField("tileCount").GetValue(s);

            int cnt = 0;
            // ShapeData.rows[y].cells[x] 형태
            if (s.rows != null)
                for (int y = 0; y < s.rows.Length; y++)
                    for (int x = 0; x < s.rows[y].columns.Length; x++)
                        if (s.rows[y].columns[x]) cnt++;
            return cnt;
        }

        bool IsGridFullyReady(GridManager gm)
        {
            if (!gm || gm.gridSquares == null) return false;
            int R = gm.gridSquares.GetLength(0), C = gm.gridSquares.GetLength(1);
            if (R != gm.rows || C != gm.cols) return false;
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                    if (gm.gridSquares[r, c] == null) return false;
            return true;
        }



        public IEnumerator EnterTutorial()
        {
            yield return null;

            RequestTutorialApply();
        }

        private void ApplyTutorialNow(int index)
        {
            _pendingTutorialApply = false;
            try { if (_onGridReadyTutorial != null) Game.Bus?.Unsubscribe(_onGridReadyTutorial); } catch { }
            _onGridReadyTutorial = null;

            SetMapDataToGrid(index, publishGridReady: false);
            StartCoroutine(Co_PostEnterSignals(GameMode.Tutorial));
        }

        public void RequestTutorialApply(int index = 0)
        {
            _tutorialApplyTicket++;
            int myTicket = _tutorialApplyTicket;

            if (_onGridReadyTutorial != null) { try { Game.Bus?.Unsubscribe(_onGridReadyTutorial); } catch { } _onGridReadyTutorial = null; }

            _pendingTutorialApply = true;
            _pendingIndex = index;

            var gm = GridManager.Instance;
            if (IsGridFullyReady(gm)) { ApplyTutorialNow(index); return; }

            _onGridReadyTutorial = _ =>
            {
                if (myTicket != _tutorialApplyTicket) return;
                if (!IsGridFullyReady(GridManager.Instance)) return;
                ApplyTutorialNow(_pendingIndex);
            };
            Game.Bus?.Subscribe(_onGridReadyTutorial, replaySticky: true);
        }

        IEnumerator Co_PostGridReadyOnce(int rows, int cols)
        {
            // 동기 재귀 끊기 위해 다음 프레임에 한 번만
            yield return null;
            Game.Bus?.PublishSticky(new GridReady(rows, cols), alsoEnqueue: false);
            // Immediate는 되도록 생략 (필요 시 한 줄만)
            // Game.Bus?.PublishImmediate(new GridReady(rows, cols));
        }

        public void EnterClassic(ClassicEnterPolicy policy = ClassicEnterPolicy.ResumeIfAliveElseLoadSaveElseNew)
        {
            ClearAdventureListeners();
            DisableAdventureObjects();
            SetGoalKind(MapGoalKind.None);

            var gm = GridManager.Instance;
            if (!gm) { Debug.LogError("[ClassicEnter] GridManager missing"); return; }

            bool hasLiveBoard = gm.HasAnyOccupied();

            var gd = saveManager?.gameData;
            int savedCount = gd?.currentMapLayout?.Count ?? 0;
            int expected = gm.rows * gm.cols;

            bool hasSavable =
                gd != null &&
                gd.isClassicModePlaying &&
                gd.currentMapLayout != null &&
                gd.currentMapLayout.Count == gm.rows * gm.cols &&
                gd.currentMapLayout.Any(v => v > 0);

            Debug.Log($"[ClassicEnter] policy={policy}, hasLiveBoard={hasLiveBoard}, " +
                      $"isClassicModePlaying={gd?.isClassicModePlaying}, savedCount={savedCount}, expected={expected}, " +
                      $"hasSavable={hasSavable}");

            switch (policy)
            {
                case ClassicEnterPolicy.ForceNew:
                    gm.ResetBoardToEmpty();
                    StartNewClassicMap();
                    GameSnapShot.SaveGridSnapshot();
                    break;

                case ClassicEnterPolicy.ForceLoadSave:
                    gm.ResetBoardToEmpty();
                    if (hasSavable)
                    {
                        Debug.Log("[ClassicEnter] -> LoadCurrentClassicMap()");
                        LoadCurrentClassicMap();
                    }
                    else
                    {
                        Debug.Log("[ClassicEnter] -> StartNewClassicMap() (no savable)");
                        StartNewClassicMap();
                    }
                    break;

                default: // ResumeIfAliveElseLoadSaveElseNew
                    if (hasLiveBoard)
                    {
                        Debug.Log("[ClassicEnter] Resume live board");
                        gm.HealBoardFromStates();
                        gm.ValidateGridConsistency();
                    }
                    else if (hasSavable)
                    {
                        Debug.Log("[ClassicEnter] Resume -> Load save");
                        gm.ResetBoardToEmpty();
                        LoadCurrentClassicMap();
                        if (!gm.HasAnyOccupied() && !hasSavable)
                        {
                            Debug.Log("[Hand] No live board & no save → StartNewClassicMap()");
                            MapManager.Instance.StartNewClassicMap();
                        }
                    }
                    else
                    {
                        Debug.Log("[ClassicEnter] No live/save -> Start new");
                        gm.ResetBoardToEmpty();
                        StartNewClassicMap();
                        GameSnapShot.SaveGridSnapshot();
                    }
                    break;
            }
        }

        public void RequestClassicEnter(ClassicEnterPolicy policy = ClassicEnterPolicy.ResumeIfAliveElseLoadSaveElseNew)
        {
            Debug.Log("[MapManager] RequestClassicEnter 호출됨");
            _pendingClassicEnter = true;
            _pendingClassicPolicy = policy;

            var gm = GridManager.Instance;
            if (IsGridFullyReady(gm))
            {
                // 이미 준비되어 있으면 바로 진입하되, 재진입 방지 먼저!
                _pendingClassicEnter = false;
                EnterClassic(policy);
                return;
            }

            // 1회성 핸들러 준비
            _classicEnterHandler = _ =>
            {
                // 이미 처리됐다면 무시
                if (!_pendingClassicEnter) return;
                if (!IsGridFullyReady(GridManager.Instance)) return;

                // 재진입 방지 플래그를 먼저 내리고
                _pendingClassicEnter = false;

                // 바로 구독 해제(한 번만 동작)
                try { Game.Bus?.Unsubscribe(_classicEnterHandler); } catch { }

                Debug.Log("[MapManager] Requesting Classic Enter (by GridReady)");
                EnterClassic(_pendingClassicPolicy);
            };

            // Sticky 재생 시 즉시 불릴 수 있으니 위 가드 순서가 중요!
            Game.Bus?.Subscribe(_classicEnterHandler, replaySticky: true);

            // 타임아웃 강행도 동일하게 가드
            StartCoroutine(Co_ForceEnterClassicAfterTimeout(2f));
            Debug.Log($"[MM] RequestClassicEnter: policy={policy}, gridReady={IsGridFullyReady(GridManager.Instance)}");
        }

        private IEnumerator Co_ForceEnterClassicAfterTimeout(float sec)
        {
            float t = 0f;
            while (t < sec && !IsGridFullyReady(GridManager.Instance))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_pendingClassicEnter)
            {
                Debug.LogWarning("[MapManager] GridReady timeout -> forcing EnterClassic");
                EnterClassic(_pendingClassicPolicy);
                _pendingClassicEnter = false;
            }
        }

        public void StartNewClassicMap()
        {
            Debug.Log("[MM] StartNewClassicMap()");

            var gm = GridManager.Instance;
            if (!gm || gm.gridSquares == null)
            {
                Debug.LogWarning("[MapManager] StartNewClassicMap: GridManager not ready.");
                return;
            }

            gm.ResetBoardToEmpty();

            if (!_codeMapsBuilt) { BuildCodeMaps(); _codeMapsBuilt = true; }

            GenerateClassicStartingMap();
            gm.ValidateGridConsistency();

            // 세이브
            if (saveManager?.gameData != null)
            {
                saveManager.gameData.isClassicModePlaying = true;
                saveManager.gameData.currentMapLayout = gm.ExportLayoutCodes();
                saveManager.gameData.currentScore = 0;
                saveManager.gameData.currentCombo = 0;
                saveManager.SaveGame();
            }

            GameSnapShot.SaveGridSnapshot();

            StartCoroutine(Co_PostEnterSignals(GameMode.Classic));
        }

        private IEnumerator RestoreScoreNextFrame()
        {
            yield return null;
            RestoreScoreFromSave();
        }

        private void RestoreScoreFromSave()
        {
            var save = saveManager;
            var score = ScoreManager.Instance;
            var gm = GridManager.Instance;
            if (save == null || score == null || gm == null) return;

            // 이어하기 조건: 점수가 있거나(플레이 이력) / 보드에 타일이 남아있을 때
            bool shouldRestore = save.gameData.currentScore > 0 || gm.HasAnyOccupied();
            if (shouldRestore)
            {
                Debug.Log($"[MapManager] Restoring score, Current Score : {save.gameData.currentScore}, Current Combo : {save.gameData.currentCombo}");
                score.SetFromSave(save.gameData.currentScore, save.gameData.currentCombo);
            }
            else
                score.ResetAll(); // 완전 새 게임이면 0으로
        }

        public enum ClassicEnterPolicy
        {
            ResumeIfAliveElseLoadSaveElseNew,  // 기본: 라이브 보드 그대로, 없으면 저장 복원, 그것도 없으면 신규
            ForceLoadSave,                     // 항상 저장 복원
            ForceNew                           // Retry/패배: 완전 초기화 -> 신규
        }
        IEnumerator Co_PostEnterSignals(GameMode mode)
        {
            // 동기 재귀 차단
            yield return null;

            var gm = GridManager.Instance;
            if (!gm) yield break;

            // 보드는 Sticky 1회
            Game.Bus?.PublishSticky(new BoardReady(gm.rows, gm.cols), alsoEnqueue: false);

            // 입장은 Immediate 1회 (손패/스폰은 이걸 듣는다)
            Game.Bus?.PublishImmediate(new GameEntered(mode));
        }

        public void RestartClassicAfterReset(bool clearRun)
        {
            StartCoroutine(CoRestartClassicAfterReset(clearRun));
        }

        private IEnumerator CoRestartClassicAfterReset(bool clearRun)
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (clearRun)
            {
                saveManager?.ClearRunState(true);
                ScoreManager.Instance?.ResetAll();
                yield break;
            }

            EnterClassic(ClassicEnterPolicy.ForceNew);
        }

        public void EnterStage(int stageNumber)
        {
            if (stageNumber <= 0)
            {
                Debug.LogError($"[MapManager] EnterStage expects 1-based, got {stageNumber}");
                return;
            }

            int idx0 = Mathf.Clamp(stageNumber - 1, 0, _mapList.Length - 1);
            int mapIdx = Mathf.Clamp(idx0 + 1, 1, _mapList.Length - 1);

            _currentMapData = null;
            _scoreGoalClearedAnnounced = false;
            _fruitAllClearedAnnounced = false;

            Debug.Log($"[MapManager] EnterStage(1-based)={stageNumber} -> idx0={idx0} => mapIdx={mapIdx}");
            SetGameMode(GameMode.Adventure);

            _currentMapData = _mapList[mapIdx];

            if (_currentMapData != null && _currentMapData.goalKind == MapGoalKind.Fruit)
                SyncFruitRuntimeFromMap(_currentMapData);
            else
                ClearFruitRuntime();

            SetMapDataToGrid(mapIdx);

            if (_currentMapData != null)
            {
                if (_currentMapData.goalKind == MapGoalKind.Fruit) { ResetFruitProgress(); SetAdvFruitObjects(); }
                else if (_currentMapData.goalKind == MapGoalKind.Score) { SetAdvScoreObjects(); }
            }

            StartCoroutine(Co_PostEnterSignals(GameMode.Adventure));
        }

        private void ClearFruitRuntime()
        {
            Array.Clear(_fruitEnabledRuntime, 0, _fruitEnabledRuntime.Length);
            Array.Clear(_fruitGoalsRuntime, 0, _fruitGoalsRuntime.Length);
            _activeFruitCodes.Clear();
            _fruitGoalsByCode.Clear();
            _fruitAllClearedAnnounced = false;
        }

        // 과일 클리어 시 호출 (과일 코드, 개수)
        public void OnFruitCleared(int fruitCode, int count = 1)
        {
            // 과일 코드가 아니거나 활성화 안 되어 있으면 무시
            if (!IsFruitCode(fruitCode) || !_activeFruitCodes.Contains(fruitCode))
                return;

            // 코드→인덱스(0~4) 변환
            int idx = fruitCode - 201; // FruitBaseCode
            if ((uint)idx >= (uint)_fruitGoalsRuntime.Length) return;

            // 현재 수집값 누적 (목표 초과 캡)
            int target = Mathf.Max(0, _fruitGoalsInitial[idx]);
            int nextCurrent = Mathf.Clamp(fruitCurrentsRuntime[idx] + Mathf.Max(0, count), 0, target);
            fruitCurrentsRuntime[idx] = nextCurrent;

            // UI: 남은 = target - current 를 내부에서 계산해 표시
            _fruitUI?.SetCurrent(idx, nextCurrent);

            // 전부 달성 체크
            AnnounceFruitAllCleared();
#if UNITY_EDITOR
            Debug.Log($"[Fruit] cleared code={fruitCode} idx={idx} current={nextCurrent}/{target} (remain={Mathf.Max(0, target - nextCurrent)})");
#endif
        }

        public bool IsAllFruitCleared()
        {
            // 과일 모드가 아니거나 활성 과일이 없으면 false
            if (CurrentMapData == null || CurrentMapData.goalKind != MapGoalKind.Fruit) return false;
            if (_activeFruitCodes == null || _activeFruitCodes.Count == 0) return false;

            // 활성 과일들의 남은 목표가 모두 0이하인지
            foreach (var code in _activeFruitCodes)
            {
                int idx = code - FruitBaseCode;
                int target = Mathf.Max(0, _fruitGoalsInitial[idx]);
                int current = Mathf.Clamp(fruitCurrentsRuntime[idx], 0, target);
                Debug.Log($"[FruitChk] code={code} current={current}/{target}");
                if (current < target) return false;
            }
            return true;
        }

        private void AnnounceFruitAllCleared()
        {
            if (_fruitAllClearedAnnounced) return;
            if (!IsAllFruitCleared()) return;
            _fruitAllClearedAnnounced = true;

            int index = Mathf.Max(1, _currentMapData?.mapIndex ?? 1);
            string name = _currentMapData?.stageName ?? $"Stage{index}";
            saveManager?.TryUpdateAdventureBest(index, name);

            int finalScore = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
            var ev = new AdventureStageCleared(MapGoalKind.Fruit, finalScore);
            Game.Bus?.PublishSticky(ev, alsoEnqueue: false);
            Game.Bus?.PublishImmediate(ev);
            Debug.Log($"[Fruit] ALL CLEARED -> AdventureStageCleared(Fruit, score={finalScore})");
        }

        public int GetInitialFruitGoalByCode(int code)
        {
            int idx = code - 201;
            if ((uint)idx >= (uint)_fruitGoalsInitial.Length) return 0;
            return _fruitGoalsInitial[idx];
        }

        private void SyncFruitRuntimeFromMap(MapData map)
        {
            ClearFruitRuntime();
            if (map == null) return;

            // 1) enabled / goals 복사 (길이 보호)
            if (map.fruitEnabled != null && map.fruitEnabled.Length >= FruitCount)
                Array.Copy(map.fruitEnabled, _fruitEnabledRuntime, FruitCount);
            if (map.fruitGoals != null && map.fruitGoals.Length >= FruitCount)
                Array.Copy(map.fruitGoals, _fruitGoalsRuntime, FruitCount);
            // 초기 목표도 저장
            if (map?.fruitGoals != null && map.fruitGoals.Length >= FruitCount)
                Array.Copy(map.fruitGoals, _fruitGoalsInitial, FruitCount);


            // 2) enabled 기준으로 활성 코드/목표 구성 (201..205)
            for (int i = 0; i < FruitCount; i++)
            {
                if (!_fruitEnabledRuntime[i]) continue;
                if (_fruitGoalsRuntime[i] <= 0) continue;
                int code = FruitBaseCode + i;
                _activeFruitCodes.Add(code);
                _fruitGoalsByCode[code] = _fruitGoalsRuntime[i];
            }
            _fruitAllClearedAnnounced = false;
#if UNITY_EDITOR
            UnityEngine.Debug.Log(
                $"[MapManager] Fruit synced | enabled={string.Join(",", _fruitEnabledRuntime.Select(b => b ? 1 : 0))} " +
                $"goals={string.Join(",", _fruitGoalsRuntime)} codes={string.Join(",", _activeFruitCodes)}");
#endif
        }
        /// <summary>
        /// 점수 슬라이더 셋팅하기
        /// </summary>
        public void SetAdvScoreObjects()
        {
            if (CurrentMode != GameMode.Adventure) return;
            int target = Mathf.Max(1, _currentMapData?.scoreGoal ?? 1);
            int current = 0; // 이어하기면 saveManager.gameData.currentScore 등으로 대체 가능

            var stage = StageManager.Instance;
            if (stage == null || stage.adventureScoreModeObjects == null || stage.adventureScoreModeObjects.Length <= 1)
            {
                Debug.LogWarning("[AdvScore] UI 루트가 없습니다.");
                return;
            }

            var root = stage.adventureScoreModeObjects[1];
            if (!root)
            {
                Debug.LogWarning("[AdvScore] ScoreProgress 루트가 비어있습니다.");
                return;
            }

            var ui = root.GetComponent<AdventureScoreProgress>();
            if (!ui)
            {
                Debug.LogWarning("[AdvScore] AdventureScoreProgress 컴포넌트가 없습니다.");
                return;
            }

            // UI 초기화
            ui.Initialize(target, current);

            // 점수 이벤트 구독 (중복 구독 방지)
            var scoreMgr = ScoreManager.Instance;
            if (scoreMgr != null)
            {
                if (_scoreProgressHandler != null)
                    scoreMgr.OnScoreChanged -= _scoreProgressHandler;

                _scoreProgressHandler = (newScore) =>
                {
                    ui.UpdateCurrent(newScore);

                    // 점수 모드일 때만 체크
                    if (_currentMapData != null && _currentMapData.goalKind == MapGoalKind.Score)
                    {
                        if (!_scoreGoalClearedAnnounced && newScore >= target)
                        {
                            _scoreGoalClearedAnnounced = true;

                            // 신기록 저장 시도 (최고 스테이지 갱신)
                            int index = Mathf.Max(1, _currentMapData.mapIndex);
                            string name = _currentMapData.stageName ?? $"Stage{index}";
                            saveManager?.TryUpdateAdventureBest(index, name);

                            int finalScore = scoreMgr ? scoreMgr.Score : newScore;

                            // 스테이지 클리어 이벤트 발행(점수 모드)
                            Game.Bus?.PublishImmediate(new AdventureStageCleared(MapGoalKind.Score, finalScore));
                        }
                    }
                };
                scoreMgr.OnScoreChanged += _scoreProgressHandler;

                // 이어하기 케이스: 현재 점수로 바로 한 번 평가
                int now = scoreMgr.Score;
                ui.UpdateCurrent(now);
                if (_currentMapData != null && _currentMapData.goalKind == MapGoalKind.Score
                    && !_scoreGoalClearedAnnounced && now >= target)
                {
                    _scoreGoalClearedAnnounced = true;

                    int index = Mathf.Max(1, _currentMapData.mapIndex);
                    string name = _currentMapData.stageName ?? $"Stage{index}";
                    saveManager?.TryUpdateAdventureBest(index, name);

                    Game.Bus?.PublishImmediate(new AdventureStageCleared(MapGoalKind.Score, now));
                }
            }
        }
        /// <summary>
        /// 활성화된 과일 종류, 갯수 붙이기
        /// </summary>
        public void SetAdvFruitObjects()
        {
            if (CurrentMode != GameMode.Adventure) return;
            //StageManager의 adventureFruitModeObjects[1] 번에 과일모드 현재 과일 목표들 오브젝트 있음
            var stage = StageManager.Instance;
            if (stage == null || stage.adventureFruitModeObjects == null || stage.adventureFruitModeObjects.Length == 0)
            {
                Debug.LogWarning("[AdvFruit] StageManager UI 루트가 없습니다.");
                return;
            }

            var fruitRoot = stage.adventureFruitModeObjects[1]; // <- 실제 인덱스에 맞게
            if (!fruitRoot)
            {
                Debug.LogWarning("[AdvFruit] Fruit UI 루트가 비어있습니다.");
                return;
            }

            _fruitUI = fruitRoot.GetComponent<AdventureFruitProgress>();
            if (_fruitUI == null)
            {
                Debug.LogWarning("[AdvFruit] AdventureFruitProgress 컴포넌트가 없습니다.");
                return;
            }

            // 런타임 데이터(활성/목표). 이름은 프로젝트 필드명에 맞게 치환
            // 예: fruitEnabledRuntime: bool[], fruitGoalsRuntime: int[]
            bool[] enabled = _fruitEnabledRuntime;
            int[] goals = _fruitGoalsRuntime;

            // 아이콘 스프라이트: GameDataStorage에서 로딩됨
            var icons = _00.WorkSpace.GIL.Scripts.GDS.I.FruitIconsSprites; // GameDataStorage
            _fruitUI.Initialize(enabled, goals, icons);
            // 이어하기, 현재값 반영할때 사용
            _fruitUI.SetCurrents(fruitCurrentsRuntime);
        }

        private void ResetFruitProgress()
        {
            if (fruitCurrentsRuntime == null || fruitCurrentsRuntime.Length != FruitCount)
                fruitCurrentsRuntime = new int[FruitCount];
            else
                Array.Clear(fruitCurrentsRuntime, 0, fruitCurrentsRuntime.Length);

            _fruitUI?.SetCurrents(fruitCurrentsRuntime);
        }
        private void OnGameOverConfirmed(GameOverConfirmed e)
        {
            if (_shownOnce) return;
            _shownOnce = true;

            var mm = MapManager.Instance;
            var sm = Game.Save;
            var mode = mm?.CurrentMode ?? GameMode.Classic;

            if (mode == GameMode.Adventure)
            {
                sm?.ClearRunState(true);
                return;
            }

            // Classic 경로
            sm?.UpdateClassicScore(e.score);
            sm?.ClearRunState(true);
            Sfx.GameOver();
        }
        public int GetFruitCurrentByCode(int code)
        {
            int idx = code - FruitBaseCode;
            if ((uint)idx >= (uint)fruitCurrentsRuntime.Length) return 0;
            int target = GetInitialFruitGoalByCode(code);
            return Mathf.Clamp(fruitCurrentsRuntime[idx], 0, target);
        }

        // 남은 수량(target - current, 최소 0)
        public int GetFruitRemainingByCode(int code)
        {
            int target = GetInitialFruitGoalByCode(code);
            int current = GetFruitCurrentByCode(code);
            return Mathf.Max(0, target - current);
        }

        // 과일 아이콘(있으면 전용 아이콘, 없으면 블록스프라이트로 폴백)
        public Sprite GetFruitIconByCode(int code)
        {
            var icons = GDS.I?.FruitIconsSprites;
            int idx = code - FruitBaseCode;
            if (icons != null && idx >= 0 && idx < icons.Length) return icons[idx];
            return GetFruitSpriteByCode(code);
        }
        public void EnterAdventureByIndex0(int idx0)
        {
            // 0-based 스테이지 인덱스를 안전하게 클램프
            idx0 = Mathf.Clamp(idx0, 0, _mapList.Length - 1);

            // 실제 맵 인덱스는 +1 (0은 튜토리얼)
            int mapIdx = Mathf.Clamp(idx0 + 1, 1, _mapList.Length - 1);

            _currentMapData = null;
            _scoreGoalClearedAnnounced = false;
            _fruitAllClearedAnnounced = false;

            Debug.Log($"[MapManager] EnterAdventureByIndex0 idx0={idx0} => mapIdx={mapIdx}");
            SetGameMode(GameMode.Adventure);

            ScoreManager.Instance?.ResetAll();
            GridManager.Instance?.ResetBoardToEmpty();
            UnityEngine.Object.FindFirstObjectByType<BlockStorage>()?.ClearHand();
            saveManager?.ClearRunState(save: true);
            saveManager?.SkipNextSnapshot("AdventureEnter");
            saveManager?.SuppressSnapshotsFor(1.0f);

            _currentMapData = _mapList[mapIdx];


            var kind = _currentMapData != null ? _currentMapData.goalKind : MapGoalKind.Score;
            SetGoalKind(kind);
            StageManager.Instance?.SetObjectsByGameModeNGoalKind(GameMode.Adventure, kind);
            if (kind == MapGoalKind.Fruit) SyncFruitRuntimeFromMap(_currentMapData);
            else ClearFruitRuntime();


            SetMapDataToGrid(mapIdx, publishGridReady: true);

            if (_currentMapData != null)
            {
                if (kind == MapGoalKind.Fruit) { ResetFruitProgress(); SetAdvFruitObjects(); }
                else { SetAdvScoreObjects(); }
            }

            StartCoroutine(Co_PostEnterSignals(GameMode.Adventure));
        }

        public void ClearAdventureListeners()
        {
            if (_scoreProgressHandler != null)
            {
                var sm = ScoreManager.Instance;
                if (sm != null) sm.OnScoreChanged -= _scoreProgressHandler;
                _scoreProgressHandler = null;
            }
            _fruitUI = null;
        }
        public void DisableAdventureObjects()
        {
            var stage = StageManager.Instance; if (!stage) return;
            void ToggleAll(GameObject[] arr, bool on)
            {
                if (arr == null) return;
                for (int i = 0; i < arr.Length; i++) if (arr[i]) arr[i].SetActive(on);
            }
            ToggleAll(stage.adventureScoreModeObjects, false);
            ToggleAll(stage.adventureFruitModeObjects, false);
        }
    }
}