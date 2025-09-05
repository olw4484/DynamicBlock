using System.Collections.Generic;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Maps
{
    [CreateAssetMenu(fileName = "Map", menuName = "New Map", order = 1)]
    public class MapData : ScriptableObject
    {
        [Header("ID")] 
        public string id;
        public int mapIndex;

        public enum MapType
        {
            Tutorial,
            Score,
            Fruit
        }

        public int scoreGoal;
        public Dictionary<string, int> FruitGoal;

        public bool[,] GridLayout;
    }
}
