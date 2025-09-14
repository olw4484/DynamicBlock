using System;
using System.Collections.Generic;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Maps;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _00.WorkSpace.GIL.Scripts
{
    [DefaultExecutionOrder(-10000)]
    public class GameDataStorage : MonoBehaviour
    {
        private static GameDataStorage _instance;
        public static GameDataStorage Instance => _instance;

        // Paths (Resources 폴더 내)
        [Header("Resources Paths")]
        [SerializeField] private string mapsPath                 = "Maps";
        [SerializeField] private string shapesPath               = "Shapes";
        [SerializeField] private string blockImagesPath          = "BlockImages";
        [SerializeField] private string blockWithFruitImagesPath = "BlockWithFruitImages";
        [SerializeField] private string fruitBackgroundPath      = "FruitBackgroundImage";
        // 언젠가 쓰일 수 있는 과일 이미지 
        [SerializeField] private string fruitIconsPath = "FruitIcons";
        // 필요시 여기에 오디오/아이콘 등 추가

        // Loaded Assets
        [Header("Loaded Assets (ReadOnly)")]
        [NonSerialized] public MapData[]    Maps;
        [NonSerialized] public ShapeData[]  Shapes;
        [NonSerialized] public Sprite[]     BlockSprites;
        [NonSerialized] public Sprite[]     BlockWithFruitSprites;
        [NonSerialized] public Sprite[]     FruitBackgroundSprites;
        [NonSerialized] public Sprite[]     FruitIconsSprites;

        // Fast Lookup
        private Dictionary<string, MapData>   _mapByName;
        private Dictionary<string, ShapeData> _shapeByName;
        private Dictionary<string, int>       _blockNameToIndex;
        private Dictionary<string, int>       _blockFruitNameToIndex;
        private Dictionary<string, int>       _fruitBgNameToIndex;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            SafeInit();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && this == Instance)
            {
                SafeInit();
            }
#endif
        }

        private void SafeInit()
        {
            if (IsInitialized) return;
            try
            {
                LoadAll();
                BuildIndexes();
                IsInitialized = true;
                Debug.Log($"[GameDataStorage] Initialized. Maps:{Maps.Length} | Shapes:{Shapes.Length} | " +
                          $"Blocks:{BlockSprites.Length} | BlockWithFruit:{BlockWithFruitSprites.Length} | FruitBG:{FruitBackgroundSprites.Length}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameDataStorage] Init failed: {e}");
            }
        }

        private void LoadAll()
        {
            Maps                   = Resources.LoadAll<MapData>(mapsPath)                ?? Array.Empty<MapData>();
            Shapes                 = Resources.LoadAll<ShapeData>(shapesPath)            ?? Array.Empty<ShapeData>();
            BlockSprites           = Resources.LoadAll<Sprite>(blockImagesPath)          ?? Array.Empty<Sprite>();
            BlockWithFruitSprites  = Resources.LoadAll<Sprite>(blockWithFruitImagesPath) ?? Array.Empty<Sprite>();
            FruitBackgroundSprites = Resources.LoadAll<Sprite>(fruitBackgroundPath)      ?? Array.Empty<Sprite>();
            // 과일 이미지 스프라이트에서 뽑아오기
            FruitIconsSprites      = Resources.LoadAll<Sprite>(fruitIconsPath)           ?? Array.Empty<Sprite>();
            // 필요시 다른 데이터들도 추가 가능
        }

        private void BuildIndexes()
        {
            _mapByName   = new Dictionary<string, MapData>(StringComparer.Ordinal);
            foreach (var m in Maps) if (m) _mapByName[m.name] = m;

            _shapeByName = new Dictionary<string, ShapeData>(StringComparer.Ordinal);
            foreach (var s in Shapes) if (s) _shapeByName[s.name] = s;

            _blockNameToIndex = BuildNameToIndex(BlockSprites);
            _blockFruitNameToIndex = BuildNameToIndex(BlockWithFruitSprites);
            _fruitBgNameToIndex    = BuildNameToIndex(FruitBackgroundSprites);
        }

        private static Dictionary<string, int> BuildNameToIndex(IReadOnlyList<Sprite> arr)
        {
            var dict = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < arr.Count; i++)
            {
                var s = arr[i];
                if (!s) continue;
                if (!dict.ContainsKey(s.name)) dict[s.name] = i;
            }
            return dict;
        }

        // Public APIs

        // Maps / Shapes
        public MapData   GetMapByName(string name)   => name != null && _mapByName.TryGetValue(name, out var m) ? m : null;
        public ShapeData GetShapeByName(string name) => name != null && _shapeByName.TryGetValue(name, out var s) ? s : null;

        // Block Sprites
        public Sprite GetBlockSprite(int index) => (index >= 0 && index < BlockSprites.Length) ? BlockSprites[index] : null;
        public int    GetBlockSpriteIndex(Sprite s) => s && _blockNameToIndex.TryGetValue(s.name, out var i) ? i : -1;
        public bool   TryGetBlockSpriteByName(string name, out Sprite s)
        {
            s = null;
            if (name == null) return false;
            if (_blockNameToIndex.TryGetValue(name, out var i) && i >= 0 && i < BlockSprites.Length)
            { s = BlockSprites[i]; return true; }
            return false;
        }

        // BlockWithFruit Sprites
        public Sprite GetBlockWithFruitSprite(int index) => (index >= 0 && index < BlockWithFruitSprites.Length) ? BlockWithFruitSprites[index] : null;
        public int    GetBlockWithFruitSpriteIndex(Sprite s) => s && _blockFruitNameToIndex.TryGetValue(s.name, out var i) ? i : -1;

        // Fruit Background Sprites
        public Sprite GetFruitBackgroundSprite(int index) => (index >= 0 && index < FruitBackgroundSprites.Length) ? FruitBackgroundSprites[index] : null;
        public int    GetFruitBackgroundSpriteIndex(Sprite s) => s && _fruitBgNameToIndex.TryGetValue(s.name, out var i) ? i : -1;

        // 유틸
        public T[] GetAll<T>(string resourcesPath) where T : Object
            => Resources.LoadAll<T>(resourcesPath);

        public void ReloadAllInEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            LoadAll();
            BuildIndexes();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    // 짧게 쓰는 헬퍼 ( 실험용 )
    public static class GDS
    {
        public static GameDataStorage I => GameDataStorage.Instance;
    }
}
