using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public struct FitInfo
    {
        public Vector2Int Offset;                 // 좌상단 오프셋 (col=x, row=y)
        public List<GridSquare> CoveredSquares;   // 이 배치로 덮게 될 셀들
    }

    public partial class BlockSpawnManager : MonoBehaviour, IManager
    {
        public static BlockSpawnManager Instance { get; private set; }

        public int Order => 12;
        private EventQueue _bus;

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

        public void SetDependencies(EventQueue bus) { _bus = bus; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); return;
            }
            Instance = this;
        }

        public void PreInit()
        {
            LoadResources();
        }

        public void Init()
        {
            BuildWeightTable();
        }

        public void PostInit() { }

        private void LoadResources()
        {
            shapeData = new List<ShapeData>(Resources.LoadAll<ShapeData>(resourcesPath));
        }

        public void BuildWeightTable()
        {
            BuildCumulativeTable();
            BuildInverseCumulativeTable();
        }
    }
}


