using _00.WorkSpace.GIL.Scripts.Maps;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public class MapManager : MonoBehaviour, IManager
    {
        public static MapManager Instance;
        private MapData[] _mapList; 
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
        public void SetMapDataToGrid(MapData mapData = null)
        {
            if (mapData == null)
            {
                mapData = _mapList[0];
            }

            int rows = mapData.rows;
            int cols = mapData.cols;

            if (mapData.layout == null || mapData.layout.Count != rows * cols)
            {
                Debug.LogError($"[MapManager] 레이아웃 불일치 rows*cols={rows * cols}, layout={mapData.layout?.Count ?? 0}");
                return;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx  = r * cols + c;
                    int code = mapData.layout[idx];

                    var info = DecodeLayoutCode(mapData, code);
                    Debug.Log($"[MapManager] 행 {r} / 열 {c} 의 블럭 {info.desc}로 {(info.isActive ? "활성화" : "비활성화")}");
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
