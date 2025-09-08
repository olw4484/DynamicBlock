using System;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Maps;
using UnityEngine;

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
        
        public void PostInit() { }
        
        private void LoadMapData()
        {
            _mapList = Resources.LoadAll<MapData>("Maps");
        }
        
        /// <summary>
        /// 맵 데이터를 토대로 그리드를 칠하기, 게임 시작 -> 블럭 생성 이전에 써야 할듯
        /// 게임 시작 위치를 정확히 모르겠어서 어디서든 코드를 사용하여 바로 붙일 수 있게 해야함.
        /// _mapList의 [i]번째 데이터를 불러와서 안의 layout 상태에 따라 그리드를 칠하기
        /// </summary>
        // 버튼/Start에서 한 줄 사용
        public void SetMapDataToGrid()
        {
            if (_mapList == null || _mapList.Length == 0) LoadMapData();
            if (_mapList == null || _mapList.Length == 0)
            {
                Debug.LogError("[MapManager] Maps 폴더에서 MapData를 찾지 못했습니다.");
                return;
            }

            int idx = Mathf.Clamp(defaultMapIndex, 0, _mapList.Length - 1);
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

            // 크기 상이시 안전 범위만 칠함(필요하면 Grid를 재생성하도록 변경 가능)
            rows = Mathf.Min(rows, squares.GetLength(0));
            cols = Mathf.Min(cols, squares.GetLength(1));

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int code = map.layout[r * map.cols + c];
                    var cell = gm.gridSquares[r, c];
                    if (!cell) continue;

                    // 항상 보이도록
                    cell.gameObject.SetActive(true);

                    bool occupied   = code is >= 1 and <= 10;     // 1~5: 블럭, 6~10: 블럭+과일
                    bool hasFruit   = code is >= 6 and <= 10;
                    int  blockIdx   = Mathf.Clamp(code - 1, 0, (map.blockImages?.Length ?? 1) - 1);
                    int  fruitIdx   = Mathf.Clamp(code - 6, 0, (map.fruitImages?.Length ?? 1) - 1);

                    if (occupied)
                    {
                        if (hasFruit && map.blockWithFruitIcons != null && fruitIdx < map.blockWithFruitIcons.Length && map.blockWithFruitIcons[fruitIdx] != null)
                        {
                            cell.SetImage(map.blockWithFruitIcons[fruitIdx]);
                            cell.SetFruitImage(false);
                        }
                        else
                        {
                            if (map.blockImages != null && blockIdx < map.blockImages.Length && map.blockImages[blockIdx] != null)
                                cell.SetImage(map.blockImages[blockIdx]);

                            if (hasFruit && map.fruitImages != null && fruitIdx < map.fruitImages.Length && map.fruitImages[fruitIdx] != null)
                            {
                                cell.SetFruitImage(true);
                            }
                            else cell.SetFruitImage(false);
                        }
                    }
                    else
                    {
                        cell.SetFruitImage(false);
                    }

                    gm.gridStates[r, c] = occupied; // true = 점유
                    cell.SetOccupied(occupied);     // Active/Normal 시각 상태 동기화
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
    }
}
