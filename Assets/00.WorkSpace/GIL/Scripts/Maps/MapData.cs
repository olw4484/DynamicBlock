using System.Collections.Generic;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Maps
{
    public enum MapGoalKind { Tutorial, Score, Fruit }
    [CreateAssetMenu(fileName = "Stage_0", menuName = "Map Data", order = 1)]
    public class MapData : ScriptableObject
    {
        [Header("ID")] 
        // 맵 이름($"Stage_{mapIndex}") 이렇게 저장
        // mapIndex는 추후에 ShapeEditor처럼 맵들을 순회할 때 사용할 예정
        public string id = "";
        public int mapIndex = 0;
        
        [Header("Board Size")]
        [Min(1)] public int rows = 8;
        [Min(1)] public int cols = 8;
        
        [Header("Icons")]
        public Sprite[] fruitImages = new Sprite[5];
        public Sprite[] blockImages = new Sprite[5];
        public Sprite[] blockWithFruitIcons = new Sprite[5];
        [Header("Board Layout (rows*cols)")]
        public List<int> layout = new(); // length == rows*cols
        
        [Header("Goal")]
        public MapGoalKind goalKind = MapGoalKind.Score;
        public int scoreGoal; // Score일 때 사용
        public bool[] fruitEnabled = new bool[5]; // Fruit 모드일 때 사용
        public int[] fruitGoals = new int[5]; // Fruit일 때 사용
        
        public int Get(int r, int c) => layout[r * cols + c];
        public void Set(int r, int c, int v) { layout[r * cols + c] = v; }
        
        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (fruitImages == null || fruitImages.Length == 0)  fruitImages = Resources.LoadAll<Sprite>("FruitIcons");
                if (blockImages == null || blockImages.Length == 0)  blockImages = Resources.LoadAll<Sprite>("BlockImages");
                if (blockWithFruitIcons == null || blockWithFruitIcons.Length == 0)
                    blockWithFruitIcons = Resources.LoadAll<Sprite>("BlockWithFruitImages");
            }
#endif
        }
        
        private void OnValidate()
        {
            int total = rows * cols;
            layout ??= new List<int>(total);

            fruitImages = Resources.LoadAll<Sprite>("FruitIcons");
            blockImages = Resources.LoadAll<Sprite>("BlockImages");
            blockWithFruitIcons = Resources.LoadAll<Sprite>("BlockWithFruitImages");

            if (layout.Count < total)      layout.AddRange(new int[total - layout.Count]);
            else if (layout.Count > total) layout.RemoveRange(total, layout.Count - total);

#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                const string prefix = "Stage_";
                if (name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out var idx))
                {
                    if (id != name || mapIndex != idx)
                    {
                        id = name;
                        mapIndex = idx;
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
#endif

            if (fruitEnabled == null || fruitEnabled.Length != 5) fruitEnabled = new bool[5];
            if (fruitGoals   == null || fruitGoals.Length   != 5) fruitGoals   = new int[5];
        }
    }
}
