using System;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Shapes
{
    [CreateAssetMenu(fileName = "Shape", menuName = "New Shape", order = 1)]
    public class ShapeData : ScriptableObject
    {
        [Header("ID & Grid")]
        public string Id;
        public ShapeRow[] rows = new ShapeRow[5];

        [Header("Classic Mode")]
        public int chanceForSpawn;
        public int activeBlockCount;


        private void OnValidate()
        {
            int count = GetActiveShapeCount();
            chanceForSpawn = activeBlockCount = count;
        }

        private int GetActiveShapeCount()
        {
            if (rows == null) return 0;
            int count = 0;
            for (int i = 0; i < 5; i++)
            {
                var row = rows[i];
                if (row?.columns == null) continue;
                for (int x = 0; x < 5; x++)
                    if (row.columns[x]) count++;
            }
            return count;
        }
        private void OnEnable()
        {
            if (rows == null || rows.Length != 5)
            {
                rows = new ShapeRow[5];
            }

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null)
                    rows[i] = new ShapeRow();
            }
        }
    }

    [Serializable]                                                                                                                                     
    public class ShapeRow
    {
        public bool[] columns = new bool[5];

        public ShapeRow()
        {
            for (int i = 0; i < columns.Length; i++)
                columns[i] = false;
        }
    }
}