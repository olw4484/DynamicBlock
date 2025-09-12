using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;
using _00.WorkSpace.GIL.Scripts.Utils;

public enum GameMode{Tutorial, Classic, Adventure}

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class MapManager : MonoBehaviour, IManager
    {
        public static MapManager Instance;

        [Header("Save Tutorial")] 
        public SaveManager saveManager;
        
        [Header("Map Runtime")]
        [SerializeField] private int defaultMapIndex = 0;
        [SerializeField] private GameObject grid;
        public GameMode GameMode;
        private MapData[] _mapList;
        
        private readonly Dictionary<int, Sprite> _codeToSprite = new();
        private static readonly Regex s_CodeRegex = new(@"^\s*(\d+)(?=_)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        
        private Sprite[] _blockSpriteList;
        private Sprite[] _fruitSpriteList;
        private Sprite[] _fruitBackgroundSprite;

        private bool _codeMapsBuilt = false;
        
        private void Awake()
        {
            Debug.Log("[MapManager] : 튜토리얼 정보 초기화는 F1");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); return;
            }
            Instance = this;
            
            if(_mapList == null) LoadMapData();
            saveManager.LoadGame();
            GameMode = saveManager.gameData.isTutorialPlayed? GameMode.Classic : GameMode.Tutorial;
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
        
        /// <summary>
        /// 게임 모드 변경, 바꿀 때 이걸 쓰기(추적 용이함)
        /// </summary>
        public void SetGameMode(GameMode gameMode)
        {
            var currMode = GameMode;
            if (currMode == GameMode.Tutorial)
            {
                saveManager.gameData.isTutorialPlayed = true;
                saveManager.SaveGame();
            }
            GameMode = gameMode;
            Debug.Log($"[MapManager] 게임 모드 변경 : {currMode} -> {GameMode}");
        }
        
        public void PostInit() { }
        
        private void LoadMapData()
        {
            _mapList = Resources.LoadAll<MapData>("Maps");
            _blockSpriteList = Resources.LoadAll<Sprite>("BlockImages");
            _fruitSpriteList = Resources.LoadAll<Sprite>("BlockWithFruitImages");
            _fruitBackgroundSprite = Resources.LoadAll<Sprite>("FruitBackgroundImage");
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
        
        public static bool IsFruitCode(int code) => (code >= 200 && code < 300);
        
        /// <summary>
        /// 맵 데이터를 토대로 그리드를 칠하기, 게임 시작 -> 블럭 생성 이전에 써야 할듯
        /// 게임 시작 위치를 정확히 모르겠어서 어디서든 코드를 사용하여 바로 붙일 수 있게 해야함.
        /// _mapList의 [i]번째 데이터를 불러와서 안의 layout 상태에 따라 그리드를 칠하기
        /// </summary>
        /// <param name="index">생성할 맵 Index, 0일 경우 튜토리얼, 1 이상일 경우 스테이지 번호</param>
        // 버튼/Start에서 한 줄 사용
        public void SetMapDataToGrid(int mapIndex = 0)
        {
            var gm = GridManager.Instance;
            if (!gm || gm.gridSquares == null)
            {
                Debug.LogWarning("[MapManager] SetMapDataToGrid: GridManager not ready.");
                return;
            }

            // 이전 잔상 제거
            gm.ResetBoardToEmpty();

            // 코드 -> 스프라이트 매핑 준비(최초 1회만)
            if (!_codeMapsBuilt) { BuildCodeMaps(); _codeMapsBuilt = true; }

            // 튜토리얼 맵 로드
            var map = (_mapList != null && mapIndex >= 0 && mapIndex < _mapList.Length)
                ? _mapList[mapIndex]
                : null;

            if (map == null)
            {
                Debug.LogError($"[MapManager] SetMapDataToGrid: map not found for index={mapIndex}");
                return;
            }

            int rows = Mathf.Min(map.rows, gm.rows);
            int cols = Mathf.Min(map.cols, gm.cols);

            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int code = map.layout[r * map.cols + c];
                if (code > 0 && _codeToSprite.TryGetValue(code, out var sprite) && sprite)
                    gm.SetCellOccupied(r, c, true, sprite);
                else
                    gm.SetCellOccupied(r, c, false);
            }

            // 셀 표시 → 상태 배열 동기화(안전)
            gm.SyncStatesFromSquares();

            // 디버깅
            gm.ValidateGridConsistency();
            Debug.Log("[MapManager] Tutorial map applied via GridManager.");
        }
        
        /// <summary>
        /// 현재 클래식 모드 맵 상태를 불러오기, SaveManager에 있는 맵 정보를 그대로 집어넣기
        /// </summary>
        public void LoadCurrentClassicMap()
        {
            MapData mapData = new();
            mapData.layout = saveManager.gameData.currentMapLayout;
            ApplyMapToCurrentGrid(mapData);
        }
        
        private void ApplyMapToCurrentGrid(MapData map)
        {
            var gm = GridManager.Instance;
            if (!gm || gm.gridSquares == null) return;
            
            // 1) 이전 잔상 제거
            gm.ResetBoardToEmpty();

            // 2) 코드 매핑 준비
            if (!_codeMapsBuilt) { BuildCodeMaps(); _codeMapsBuilt = true; }
            
            int rows = Mathf.Min(map.rows, gm.rows);
            int cols = Mathf.Min(map.cols, gm.cols);
            
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int code = map.layout[r * map.cols + c];
                if (code > 0 && _codeToSprite.TryGetValue(code, out var sprite) && sprite)
                    gm.SetCellOccupied(r, c, true, sprite);
                else
                    gm.SetCellOccupied(r, c, false);
            }

            gm.ValidateGridConsistency();
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
            if (GameMode != GameMode.Classic) return;

            var gm   = GridManager.Instance;
            var grid = gm?.gridSquares;
            if (grid == null) { Debug.LogError("[ClassicStart] gridSquares is null"); return; }

            // 1) 소환 풀: 난이도 3~4 (없으면 전체)
            var spawner = BlockSpawnManager.Instance;
            if (spawner == null || spawner.shapeData == null || spawner.shapeData.Count == 0)
            {
                Debug.LogError("[ClassicStart] BlockSpawnManager/shapeData missing");
                return;
            }
            
            Debug.Log("[ClassicStartingMap] 클래식 맵 제작 시작");
            
            var pool = spawner.shapeData.Where(s => s != null && s.difficulty >= 3 && s.difficulty <= 4).ToList();
            if (pool.Count == 0) pool = spawner.shapeData.ToList();

            // 2) 예약 보드(점유도)
            var occ = SnapshotOccupied(grid);

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

            // 5) 상태 동기화(안전장치)
            gm.SyncStatesFromSquares();

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
            { this.xMin=xMin; this.xMax=xMax; this.yMin=yMin; this.yMax=yMax; this.corner=corner; }
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
        private bool[,] SnapshotOccupied(GridSquare[,] grid)
        {
            var gm = GridManager.Instance;
            int rows = gm.rows, cols = gm.cols;
            var occ = new bool[rows, cols];

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    occ[r, c] = gm.gridStates[r, c];

            return occ;
        }

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
        
        public void EnterClassic(ClassicEnterPolicy policy = ClassicEnterPolicy.ResumeIfAliveElseLoadSaveElseNew)
        {
            var gm = GridManager.Instance;
            if (!gm) return;

            bool hasLiveBoard = gm.HasAnyOccupied();
            bool hasSavable =
                saveManager?.gameData?.isClassicModePlaying == true &&
                saveManager.gameData.currentMapLayout != null &&
                saveManager.gameData.currentMapLayout.Count == gm.rows * gm.cols;

            switch (policy)
            {
                case ClassicEnterPolicy.ForceNew:
                    gm.ResetBoardToEmpty();
                    StartNewClassicMap();                 // 내부에서 ApplyMapToCurrentGrid 호출
                    GameSnapShot.SaveGridSnapshot();      // 선택
                    break;

                case ClassicEnterPolicy.ForceLoadSave:
                    gm.ResetBoardToEmpty();
                    if (hasSavable) LoadCurrentClassicMap(); else StartNewClassicMap();
                    break;

                default: // ResumeIfAliveElseLoadSaveElseNew
                    if (hasLiveBoard)
                    {
                        gm.HealBoardFromStates();         // 유령 인덱스 소거
                        gm.ValidateGridConsistency();
                    }
                    else if (hasSavable)
                    {
                        gm.ResetBoardToEmpty();
                        LoadCurrentClassicMap();
                    }
                    else
                    {
                        gm.ResetBoardToEmpty();
                        StartNewClassicMap();
                        GameSnapShot.SaveGridSnapshot();
                    }
                    break;
            }
        }
        
        public void StartNewClassicMap()
        {
            var gm = GridManager.Instance;
            if (!gm || gm.gridSquares == null)
            {
                Debug.LogWarning("[MapManager] StartNewClassicMap: GridManager not ready.");
                return;
            }

            // 1) 보드 완전 초기화
            gm.ResetBoardToEmpty();

            // 2) 코드 -> 스프라이트 매핑 준비
            if (!_codeMapsBuilt) { BuildCodeMaps(); _codeMapsBuilt = true; }

            // 3) 클래식 시작 보드 생성 알고리즘 실행
            //    필요 시 파라미터 조정
            GenerateClassicStartingMap();

            // 4) 상태/일관성 정리
            gm.SyncStatesFromSquares();     // 셀 표시 -> 상태 배열 동기화
            gm.ValidateGridConsistency();   // 디버그 확인

            // 5) 세이브 상태 세팅 & 저장
            if (saveManager != null && saveManager.gameData != null)
            {
                saveManager.gameData.isClassicModePlaying = true;
                saveManager.gameData.currentMapLayout     = gm.ExportLayoutCodes(); // 현재 보드 -> 코드 리스트
                saveManager.gameData.currentScore         = 0;
                saveManager.gameData.currentCombo         = 0;
                saveManager.SaveGame();
            }

            // 6) 첫 스냅샷 저장
            GameSnapShot.SaveGridSnapshot();

            Debug.Log("[MapManager] StartNewClassicMap: classic board generated.");
        }
        
        public enum ClassicEnterPolicy
        {
            ResumeIfAliveElseLoadSaveElseNew,  // 기본: 라이브 보드 그대로, 없으면 저장 복원, 그것도 없으면 신규
            ForceLoadSave,                     // 항상 저장 복원
            ForceNew                           // Retry/패배: 완전 초기화 -> 신규
        }
        
        public void EnterTutorial(int tutorialMapIndex = 0)
        {
            var gm = GridManager.Instance;
            if (!gm || gm.gridSquares == null)
            {
                Debug.LogWarning("[MapManager] EnterTutorial: GridManager not ready.");
                return;
            }

            // 1) 보드 비우기
            gm.ResetBoardToEmpty();

            // 2) 튜토리얼 맵 적용(반드시 GridManager 경유)
            SetMapDataToGrid(tutorialMapIndex);

            // 3) 상태 동기화
            gm.SyncStatesFromSquares();
            gm.ValidateGridConsistency();

            // 4) 스폰러가 손패를 채우도록 GridReady만 발행 (ResetRuntime 사용 X)
            Game.Bus?.PublishImmediate(new GridReady(gm.rows, gm.cols));

            Debug.Log("[MapManager] EnterTutorial done (no GameResetRequest).");
        }
    }
}
