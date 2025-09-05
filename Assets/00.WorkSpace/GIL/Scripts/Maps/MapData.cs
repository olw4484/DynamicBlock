using System.Collections.Generic;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Maps
{
    public enum MapGoalKind { Tutorial, Score, Fruit }
    [CreateAssetMenu(fileName = "Map", menuName = "Maps/Map Data", order = 1)]
    public class MapData : ScriptableObject
    {
        [Header("ID")] 
        // 맵 이름($"Stage{mapIndex}") 이렇게 저장
        // mapIndex는 추후에 ShapeEditor처럼 맵들을 순회할 때 사용할 예정
        public string id = "Stage1";
        public int mapIndex = 1;
        
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
        
        private void OnValidate()
        {
            int total = rows * cols;
            layout ??= new List<int>(total);

            fruitImages = Resources.LoadAll<Sprite>("FruitIcons");
            blockImages = Resources.LoadAll<Sprite>("BlockImages");
            blockWithFruitIcons = Resources.LoadAll<Sprite>("BlockWithFruitImages");
            // rows/cols 변경 시 layout 크기 맞추기
            // 8x8로 고정, 하지만 확장성을 고려해 추가
            if (layout.Count == total) return;
            if (layout.Count < total)
                layout.AddRange(new int[total - layout.Count]);
            else
                layout.RemoveRange(total, layout.Count - total);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            if (fruitEnabled == null || fruitEnabled.Length != 6) fruitEnabled = new bool[6];
            if (fruitGoals   == null || fruitGoals.Length   != 6) fruitGoals   = new int[6];
        }
    }
}
