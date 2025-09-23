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
        public static GameDataStorage Instance
        {
            get
            {
                if (_instance) return _instance;
                _instance = FindObjectOfType<GameDataStorage>();
                if (_instance) { _instance.SafeInit(); return _instance; }

                var prefab = Resources.Load<GameDataStorage>("GameDataStorage");
                if (prefab)
                {
                    _instance = Instantiate(prefab);
                    _instance.name = "GameDataStorage (Runtime)";
                    DontDestroyOnLoad(_instance.gameObject);
                    _instance.SafeInit();
                    return _instance;
                }

                var go = new GameObject("GameDataStorage (Auto)");
                _instance = go.AddComponent<GameDataStorage>();
                DontDestroyOnLoad(go);
                _instance.SafeInit();
                return _instance;
            }
        }

        [Header("Resources Paths")]
        [SerializeField] private string mapsPath                 = "Maps";
        [SerializeField] private string shapesPath               = "Shapes";
        [SerializeField] private string blockImagesPath          = "BlockImages";
        [SerializeField] private string blockWithFruitImagesPath = "BlockWithFruitImages";
        [SerializeField] private string fruitBackgroundPath      = "FruitBackgroundImage";
        [SerializeField] private string fruitIconsPath           = "FruitIcons";
        
        public MapData[]   Maps;
        public ShapeData[] Shapes;
        public Sprite[]    BlockSprites;
        public Sprite[]    BlockWithFruitSprites;
        public Sprite[]    FruitBackgroundSprites;
        public Sprite[]     FruitIconsSprites;
        
        private Dictionary<string, ShapeData> _shapeByName;
        private Dictionary<string, int> _blockNameToIndex;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SafeInit();
        }

        public void SafeInit()
        {
            if (IsInitialized) return;
            LoadAll();
            BuildIndexes();
            IsInitialized = true;
            Debug.Log($"[GDS] Init OK. Shapes:{Shapes.Length} | Blocks:{BlockSprites.Length}");
        }

        private void LoadAll()
        {
            Maps                  = Resources.LoadAll<MapData>(mapsPath)                ?? Array.Empty<MapData>();
            Shapes                = Resources.LoadAll<ShapeData>(shapesPath)            ?? Array.Empty<ShapeData>();
            BlockSprites          = Resources.LoadAll<Sprite>(blockImagesPath)          ?? Array.Empty<Sprite>();
            BlockWithFruitSprites = Resources.LoadAll<Sprite>(blockWithFruitImagesPath) ?? Array.Empty<Sprite>();
            FruitBackgroundSprites= Resources.LoadAll<Sprite>(fruitBackgroundPath)      ?? Array.Empty<Sprite>();
            FruitIconsSprites     = Resources.LoadAll<Sprite>(fruitIconsPath)           ?? Array.Empty<Sprite>();
        }

        private void BuildIndexes()
        {
            _shapeByName = Shapes.Where(s => s).ToDictionary(s => s.name, s => s, StringComparer.Ordinal);
            _blockNameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < BlockSprites.Length; i++)
            {
                var s = BlockSprites[i];
                if (s && !_blockNameToIndex.ContainsKey(s.name)) _blockNameToIndex[s.name] = i;
            }
        }

        public ShapeData GetShapeByName(string name)
            => (name != null && _shapeByName.TryGetValue(name, out var s)) ? s : null;

        public int GetBlockSpriteIndex(Sprite s)
            => (s && _blockNameToIndex.TryGetValue(s.name, out var idx)) ? idx : -1;

        public Sprite GetBlockSpriteByName(string name)
        {
            var result = default(Sprite);
            if (name == null) return null;
            if (_blockNameToIndex.TryGetValue(name, out var i))
            {
                result = (i >= 0 && i < BlockSprites.Length) ? BlockSprites[i] : null;
            }
            
            return result;
        }
        public Sprite GetBlockSpriteByIndex(int idx)
        {
            return (idx >= 0 && idx < BlockSprites.Length) ? BlockSprites[idx] : null;
        }
        public int GetLayoutCodeForSprite(Sprite s)
        {
            if (!s) return 0;
            if (_blockNameToIndex != null && _blockNameToIndex.TryGetValue(s.name, out var idx))
                return idx + 1;
            return 0;
        }
        public Sprite GetBlockSpriteByLayoutCode(int code)
        {
            if (code <= 0) return null;
            int idx = code - 1;
            var arr = BlockSprites;
            return (arr != null && idx >= 0 && idx < arr.Length) ? arr[idx] : null;
        }
    }

    public static class GDS
    {
        public static GameDataStorage I => GameDataStorage.Instance;
    }
}
