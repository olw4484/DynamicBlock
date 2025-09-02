using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager : MonoBehaviour
    {
        public static BlockSpawnManager Instance { get; private set; }

        [Header("Resources")] 
        [SerializeField] private string resourcesPath = "Shapes";
        [SerializeField] private List<ShapeData> shapeData;
        
        [Header("Small block Penalty")] 
        [SerializeField] private bool smallBlockPenaltyMode = true; // 켜/끄기
        [SerializeField, Range(0f, 1f)] private float smallBlockFailRate = 0.5f; // 기본 50%
        [SerializeField] private int smallBlockTileThreshold = 3;

        [Header("Wave Rules")] 
        [SerializeField, Range(1, 3)] private int maxDuplicatesPerWave = 2;
        
        [Header("Wave Record Rules")]
        [SerializeField, Range(2, 5)] private int maxSameWaveStreak = 3;
        private readonly Queue<string> _lastWaves = new();
        
        private int[] _cumulativeWeights;
        private int[] _inverseCumulativeWeights;
        private int _totalWeight;
        private int _inverseTotalWeight;

        void Awake()
        {
            Init();

            LoadResources();
            
            BuildWeightTable();
        }

        private void Init()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }
        
        private void LoadResources()
        {
            shapeData = new List<ShapeData>(Resources.LoadAll<ShapeData>(resourcesPath));
        }

        public void BuildWeightTable()
        {
            BuildCumulativeTable();
            BuildInverseCumulativeTable();
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (shapeData == null || shapeData.Count == 0)
                    LoadResources();
                BuildWeightTable();
            }
        }
#endif
    }
}
