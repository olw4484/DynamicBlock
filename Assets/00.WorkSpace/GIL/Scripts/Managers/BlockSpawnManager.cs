using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager : MonoBehaviour, IManager
    {
        public static BlockSpawnManager Instance { get; private set; }

        public int Order => 12;
        private EventQueue _bus;

        [Header("Resources")] 
        [SerializeField] private string resourcesPath = "Shapes";
        [SerializeField] private List<ShapeData> shapeData;
        
        [Header("Wave Rules")] 
        [SerializeField, Range(1, 3)] private int maxDuplicatesPerWave = 2;
        [SerializeField, Range(2, 5)] private int maxSameWaveStreak = 3;
        private readonly Queue<string> _lastWaves = new();
        
        [Header("Block Spawn Rules")]
        [SerializeField] private bool useSmallBlockSuccessGate = true;
        [SerializeField] private bool reserveCellsDuringWave = true;
        
        [Header("Dynamic Weight (tile^a)")] 
        public float aExponent = 0.3f;
        [SerializeField] private bool useDynamicWeightByTilePowA = true;
        [SerializeField] private float aMin;
        [SerializeField] private float aMax = 1.0f;
        
        [SerializeField] private SmallBlockGate[] smallBlockGates =
        {
            new() { tiles=1, percentAtAMin=30, percentAtAMax=10 },
            new() { tiles=2, percentAtAMin=50, percentAtAMax=20 },
            new() { tiles=3, percentAtAMin=70, percentAtAMax=30 },
        };
        // 정수 누적표를 만들 때 소수값 손실을 줄이기 위한 스케일
        
        private readonly int _dynamicWeightScale = 100;
        private int[] _dynCumulativeWeights;
        private int   _dynTotalWeight;
        private float _lastAForWeights = -999f;
        
// a 범위 (문서 기준 0.3~1.0)

        private int[] _cumulativeWeights;
        private int[] _inverseCumulativeWeights;
        private int _totalWeight;
        private int _inverseTotalWeight;

        public void SetDependencies(EventQueue bus) { _bus = bus; }

        private void Awake()
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

        public void Init() { }
        
        public void PostInit() { }

        private void LoadResources()
        {
            shapeData = new List<ShapeData>(Resources.LoadAll<ShapeData>(resourcesPath));
        }
    }
    
    public struct FitInfo
    {
        public Vector2Int Offset;                 // 좌상단 오프셋 (col=x, row=y)
        public List<GridSquare> CoveredSquares;   // 이 배치로 덮게 될 셀들
    }
    
    [System.Serializable]
    public struct SmallBlockGate
    {
        [Tooltip("이 타일 수에 대해 성공 확률을 적용 (1,2,3만 사용)")]
        public int tiles; // 1,2,3
        [Range(0,100)] public int percentAtAMin; // a가 최저일 때 성공 확률(%)
        [Range(0,100)] public int percentAtAMax; // a가 최고일 때 성공 확률(%)
    }
}


