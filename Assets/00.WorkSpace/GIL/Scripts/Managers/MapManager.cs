using System;
using System.Collections.Generic;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public enum GameMode{Tutorial, Classic, Adventure}

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class MapManager : MonoBehaviour, IManager
    {
        public static MapManager Instance;
        
        [Header("Map Runtime")]
        [SerializeField] private int defaultMapIndex = 0;
        [SerializeField] private GameObject grid;
        private MapData[] _mapList;
        private Sprite[] _blockSpriteList;
        private Sprite[] _fruitSpriteList;
        private Sprite[] _fruitBackgroundSprite;

        public GameMode GameMode;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); return;
            }
            Instance = this;
            
            if(_mapList == null) LoadMapData();
        }
        public int Order => 13;
        public void PreInit() { }

        public void Init()
        {
            LoadMapData();
        }

        /// <summary>
        /// 게임 모드 변경, 바꿀 때 이걸 쓰기(추적 용이함)
        /// </summary>
        public void SetGameMode(GameMode gameMode)
        {
            var currMode = GameMode;
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

        /// <summary>
        /// 맵 데이터를 토대로 그리드를 칠하기, 게임 시작 -> 블럭 생성 이전에 써야 할듯
        /// 게임 시작 위치를 정확히 모르겠어서 어디서든 코드를 사용하여 바로 붙일 수 있게 해야함.
        /// _mapList의 [i]번째 데이터를 불러와서 안의 layout 상태에 따라 그리드를 칠하기
        /// </summary>
        /// <param name="index">생성할 맵 Index, 0일 경우 튜토리얼, 1 이상일 경우 스테이지 번호</param>
        // 버튼/Start에서 한 줄 사용
        public void SetMapDataToGrid(int index = 0)
        {
            if (_mapList == null || _mapList.Length == 0) LoadMapData();
            if (_mapList == null || _mapList.Length == 0)
            {
                Debug.LogError("[MapManager] Maps 폴더에서 MapData를 찾지 못했습니다.");
                return;
            }
            int idx = Mathf.Clamp(index, 0, _mapList.Length - 1);
            if(idx == 0) idx = defaultMapIndex;
            
            var map = _mapList[idx];
            if (map == null)
            {
                Debug.LogError($"[MapManager] MapData[{idx}]가 null 입니다.");
                return;
            }

            ApplyMapToCurrentGrid(map);
        }
        
        private void ApplyMapToCurrentGrid(MapData map)
        {
            var gm = GridManager.Instance;
            if (gm == null) { Debug.LogError("[MapManager] GridManager.Instance 없음"); return; }
            var squares = gm.gridSquares;
            if (squares == null) { Debug.LogError("[MapManager] gridSquares 미초기화"); return; }

            int rows = map.rows, cols = map.cols;
            if (map.layout == null || map.layout.Count != rows * cols)
            {
                Debug.LogError($"[MapManager] 레이아웃 불일치 rows*cols={rows*cols}, layout={map.layout?.Count ?? 0}");
                return;
            }

            rows = Mathf.Min(rows, squares.GetLength(0));
            cols = Mathf.Min(cols, squares.GetLength(1));
            
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int code = map.layout[r * map.cols + c];
                    var cell = gm.gridSquares[r, c];
                    if (!cell) continue;

                    // 항상 보이게
                    cell.gameObject.SetActive(true);

                    bool occupied = (code >= 1 && code <= 10); // 1~5: 일반 블록, 6~10: 과일 블록(합쳐진거)
                    bool hasFruit = (code >= 6 && code <= 10);

                    if (occupied)
                    {
                        if (hasFruit)
                        {
                            // 과일 블록 (6~10): 합친 스프라이트 사용
                            int f = code - 6; // 0~4
                            if (map.blockWithFruitIcons != null &&
                                f < map.blockWithFruitIcons.Length &&
                                map.blockWithFruitIcons[f] != null)
                            {
                                cell.SetImage(map.blockWithFruitIcons[f]);
                            }
                            else
                            {
                                // 과일데이터 누락 시 경고만 남기고 스킵
                                Debug.LogWarning($"[MapManager] blockWithFruitIcons[{f}] 없음");
                            }
                            cell.SetFruitImage(false, null); // 오버레이는 항상 OFF
                        }
                        else
                        {
                            // 일반 블록 (1~5)
                            int i = code - 1; // 0~4
                            if (map.blockImages != null &&
                                i < map.blockImages.Length &&
                                map.blockImages[i] != null)
                            {
                                cell.SetImage(map.blockImages[i]);
                            }
                            cell.SetFruitImage(false, null); // 오버레이 OFF (잔상 방지)
                        }
                    }
                    else
                    {
                        // 빈 칸 (0)
                        cell.SetFruitImage(false, null); // 혹시 남은 오버레이 제거
                    }

                    // 모델(점유) → 뷰 동기화
                    gm.gridStates[r, c] = occupied; // true = 점유
                    cell.SetOccupied(occupied);     // Active/Normal 시각 상태 적용
                }
            }
            
        }
        
        // 블록 규칙성
        // 0     : 빈칸(비활성)
        // 1..5  : 일반 블록 (index = code-1)
        // 6..10 : 과일 블록 (index = code-6)
        private static (string desc, bool isActive) DecodeLayoutCode(MapData data, int code)
        {
            if (code == 0)
                return ("빈칸(구멍)", false);

            if (code >= 1 && code <= 5)
            {
                int i = code - 1;
                string name = (data?.blockImages != null && i < data.blockImages.Length && data.blockImages[i] != null)
                    ? data.blockImages[i].name : $"idx:{i}";
                return ($"일반타일({name})", true);
            }

            if (code >= 6 && code <= 10)
            {
                int f = code - 6;
                string name = (data?.blockWithFruitIcons != null && f < data.blockWithFruitIcons.Length && data.blockWithFruitIcons[f] != null)
                    ? data.blockWithFruitIcons[f].name : $"fruit:{f}";
                return ($"과일타일({name})", true);
            }

            // 정의 외 값은 간단히 알림만
            return ($"알수없음(code:{code})", true);
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
        // -1) 클래식 모드가 아니면 즉시 종료
        if (GameMode != GameMode.Classic) return;
        
        var grid = GridManager.Instance?.gridSquares;
        if (grid == null) { Debug.LogError("[ClassicStart] gridSquares is null"); return; }

        // 0) 보드 초기화(원하면 주석 해제)
        // ClearBoard(grid);

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
        var occ = SnapshotOccupied(grid);

        // 3) 4분면 정의(문서: 중앙 반대 코너에서 시작)
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
            bool moveNextQuadrant = false; // ★ 초과 시 다음 분면으로

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

                // 분면 한도 검사: 초과하면 이 블록은 놓지 않고 "다음 분면"으로 이동
                if (quadTileSums[qi] + tcount > perQuadrantTileCap)
                {
                    break; // 현재 분면 스캔 종료 → for 루프가 다음 분면으로 진행
                }

                // 정상 배치
                Stamp(occ, pick.s, pick.ox, pick.oy, true);
                placed.Add(pick);
                sumTiles += tcount;
                quadTileSums[qi] += tcount;
            }
        }
        // 4) 실제 보드에 반영(시각/상태)
        ApplyPlacementsToGrid(grid, placed);
        SyncGridStatesWithSquares();
        Debug.Log($"[ClassicStart] placed={placed.Count}, sumTiles={sumTiles}");
        
        // TODO : 여기에서 블럭 생성시키라는 알림 보내기
        var gm = GridManager.Instance;
        Game.Bus?.ClearSticky<GridReady>();
        Game.Bus?.PublishSticky(new GridReady(gm.rows, gm.cols));
    }

    #region helpers

    private struct Quad
    {
        public int xMin, xMax, yMin, yMax; public Vector2Int corner;
        public Quad(int xMin, int xMax, int yMin, int yMax, Vector2Int corner)
        { this.xMin=xMin; this.xMax=xMax; this.yMin=yMin; this.yMax=yMax; this.corner=corner; }
    }
    
    private void SyncGridStatesWithSquares()
    {
        var gm = GridManager.Instance;
        if (gm == null || gm.gridSquares == null || gm.gridStates == null) return;

        int rows = gm.gridSquares.GetLength(0);
        int cols = gm.gridSquares.GetLength(1);
            Debug.Log("동기화 시작.");
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            gm.gridStates[y, x] = gm.gridSquares[y, x].IsOccupied;
        }
        Debug.Log("동기화 완료");
    }
    
    private IEnumerable<Vector2Int> EnumerateCellsInQuad(Quad q)
    {
        var cells = new List<Vector2Int>();
        for (int y = q.yMin; y <= q.yMax; y++)
            for (int x = q.xMin; x <= q.xMax; x++)
                cells.Add(new Vector2Int(x, y));

        // 코너에서 가까운 순(맨해튼 거리)
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

    private bool[,] SnapshotOccupied(GridSquare[,] grid)
    {
        var occ = new bool[BOARD_SIZE, BOARD_SIZE]; // [y,x]
        int rows = Mathf.Min(grid.GetLength(0), BOARD_SIZE);
        int cols = Mathf.Min(grid.GetLength(1), BOARD_SIZE);
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                occ[y, x] = grid[y, x].IsOccupied;
        return occ;
    }

    private void ClearBoard(GridSquare[,] grid)
    {
        int rows = Mathf.Min(grid.GetLength(0), BOARD_SIZE);
        int cols = Mathf.Min(grid.GetLength(1), BOARD_SIZE);
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            grid[y, x].IsOccupied = false;
            // GridSquare에 SetState가 있다면 활성→일반으로
            try { grid[y, x].SetState(GridState.Normal); } catch { /* ignore if not present */ }
        }
    }

    private static int GetTileCount(ShapeData s)
    {
        // s.tileCount가 있다면 그 값 사용, 없으면 rows로 계산
        if (s.GetType().GetField("tileCount") != null)
            return (int)s.GetType().GetField("tileCount").GetValue(s);

        int cnt = 0;
        // ShapeData.rows[y].cells[x] 형태(프로젝트 초반 구조) 가정
        if (s.rows != null)
            for (int y = 0; y < s.rows.Length; y++)
                for (int x = 0; x < s.rows[y].columns.Length; x++)
                    if (s.rows[y].columns[x]) cnt++;
        return cnt;
    }

    private static bool TryGetShapeBounds(ShapeData s, out int w, out int h)
    {
        // width/height 필드가 있으면 우선 사용
        var wf = s.GetType().GetField("width");
        var hf = s.GetType().GetField("height");
        if (wf != null && hf != null)
        { w = (int)wf.GetValue(s); h = (int)hf.GetValue(s); return true; }

        // 없으면 rows 기반(5x5 그리드 가정)으로 최소 경계 계산
        int minX = 999, minY = 999, maxX = -1, maxY = -1;
        if (s.rows == null) { w = h = 0; return false; }
        for (int y = 0; y < s.rows.Length; y++)
            for (int x = 0; x < s.rows[y].columns.Length; x++)
                if (s.rows[y].columns[x])
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }

        if (maxX < 0) { w = h = 0; return false; }
        w = (maxX - minX + 1);
        h = (maxY - minY + 1);
        return true;
    }

    private static bool CanPlaceAt(bool[,] occ, ShapeData s, int ox, int oy)
    {
        // width/height가 있을 수도, 없을 수도 있으므로 rows 기준으로 체크
        if (s.rows == null) return false;

        // 모양의 최소 경계 산출
        int minX = 999, minY = 999, maxX = -1, maxY = -1;
        for (int y = 0; y < s.rows.Length; y++)
            for (int x = 0; x < s.rows[y].columns.Length; x++)
                if (s.rows[y].columns[x])
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }

        if (maxX < 0) return false; // 빈 모양

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (ox + w > BOARD_SIZE || oy + h > BOARD_SIZE) return false;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (s.rows[minY + y].columns[minX + x] && occ[oy + y, ox + x])
                    return false;

        return true;
    }

    private static void Stamp(bool[,] occ, ShapeData s, int ox, int oy, bool fill)
    {
        if (s.rows == null) return;

        // 최소 경계
        int minX = 999, minY = 999, maxX = -1, maxY = -1;
        for (int y = 0; y < s.rows.Length; y++)
            for (int x = 0; x < s.rows[y].columns.Length; x++)
                if (s.rows[y].columns[x])
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (s.rows[minY + y].columns[minX + x])
                    occ[oy + y, ox + x] = fill;
    }

    private static bool MakesFullLineIfStamped(bool[,] occ, ShapeData s, int ox, int oy)
    {
        // 가상 스탬프 후, 가로/세로 풀라인이 생기는지 확인
        var temp = (bool[,])occ.Clone();
        Stamp(temp, s, ox, oy, true);

        for (int y = 0; y < BOARD_SIZE; y++)
        {
            bool full = true;
            for (int x = 0; x < BOARD_SIZE; x++) if (!temp[y, x]) { full = false; break; }
            if (full) return true;
        }
        for (int x = 0; x < BOARD_SIZE; x++)
        {
            bool full = true;
            for (int y = 0; y < BOARD_SIZE; y++) if (!temp[y, x]) { full = false; break; }
            if (full) return true;
        }
        return false;
    }

    private void ApplyPlacementsToGrid(GridSquare[,] grid, List<(ShapeData s, int ox, int oy)> placed)
    {
        Sprite blockImage = _blockSpriteList[Random.Range(0, _blockSpriteList.Length - 1)];
        
        foreach (var p in placed)
        {
            // grid는 [y,x]
            if (p.s.rows == null) continue;

            // 최소 경계 구하기
            int minX = 999, minY = 999, maxX = -1, maxY = -1;
            for (int y = 0; y < p.s.rows.Length; y++)
                for (int x = 0; x < p.s.rows[y].columns.Length; x++)
                    if (p.s.rows[y].columns[x])
                    { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!p.s.rows[minY + y].columns[minX + x]) continue;
                var sq = grid[p.oy + y, p.ox + x];
                sq.SetImage(blockImage);
                sq.IsOccupied = true;
                try { sq.SetState(GridState.Active); } catch { /* 이미지 없는 경우 무시 */ }
            }
        }
    }

    #endregion
        
    }
}
