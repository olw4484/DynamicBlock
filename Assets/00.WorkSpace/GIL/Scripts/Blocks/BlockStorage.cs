using System.Collections;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Blocks
{
    public class BlockStorage : MonoBehaviour
    {
        [Header("Block Prefab & Data")]
        public GameObject blockPrefab;
        public List<ShapeData> shapeData;
    
        [Header("Spawn Positions")]
        public List<Transform> blockSpawnPosList;

        public Transform shapesPanel;
    
        private List<Block> _currentBlocks = new();
            
        private int[] _cumulativeWeights;
        private int[] _inverseCumulativeWeights;
        private int _totalWeight;
        private int _inverseTotalWeight;
        
        void Awake()
        {
            BuildCumulativeTable();
            BuildInverseCumulativeTable();
        }

        private void BuildCumulativeTable()
        {
            _cumulativeWeights = new int[shapeData.Count];
            _totalWeight = 0;
            
            for (int i = 0; i < shapeData.Count; i++)
            {
                _totalWeight += shapeData[i].chanceForSpawn;
                _cumulativeWeights[i] = _totalWeight;
            }
            
            Debug.Log($"가중치 계산 완료: {_totalWeight}");
        }
        
        private void BuildInverseCumulativeTable()
        {
            _inverseCumulativeWeights = new int[shapeData.Count];
            _inverseTotalWeight = 0;
            
            for (int i = 0; i < shapeData.Count; i++)
            {
                _inverseTotalWeight += (_totalWeight - shapeData[i].chanceForSpawn);
                _inverseCumulativeWeights[i] = _inverseTotalWeight;
            }
            
            Debug.Log($"역가중치 계산 완료: {_inverseTotalWeight}");
        }
        
        void Start()
        {
            StartCoroutine(GenerateBlocksNextFrame());
        }

        private IEnumerator GenerateBlocksNextFrame()
        {
            // 한 프레임 대기
            yield return null;

            // GridManager 준비 확인
            if (GridManager.Instance == null || GridManager.Instance.gridSquares == null)
                yield break;

            GenerateAllBlocks();
        }

        private void GenerateAllBlocks()
        {
            foreach (var blk in _currentBlocks)
            {
                if (blk != null)
                    Destroy(blk.gameObject);
            }
            _currentBlocks.Clear();
            
            bool guaranteedPlaced = false;
            
            for (int i = 0; i < blockSpawnPosList.Count; i++)
            {
                GameObject newBlockObj = Instantiate(blockPrefab,
                    blockSpawnPosList[i].position,
                    Quaternion.identity,
                    shapesPanel);

                Block newBlock = newBlockObj.GetComponent<Block>();

                ShapeData selectedShape;

                if (!guaranteedPlaced)
                {
                    selectedShape = GetGuaranteedPlaceableShape();
                    guaranteedPlaced = true;
                }
                else
                {
                    selectedShape = GetRandomShapeByWeight();
                }

                newBlock.GenerateBlock(selectedShape);
                _currentBlocks.Add(newBlock);
            }
        }
        
        private ShapeData GetRandomShapeByWeight()
        {
            float randomValue = Random.Range(0, _totalWeight);

            for (int i = 0; i < _cumulativeWeights.Length; i++)
            {
                if (randomValue <= _cumulativeWeights[i])
                    return shapeData[i];
            }

            return shapeData[shapeData.Count - 1];
        }
        
        private ShapeData GetGuaranteedPlaceableShape()
        {
            foreach (var shape in shapeData)
            {
                if (CanPlaceShapeData(shape))
                    return shape;
            }

            return GetRandomShapeByWeight();
        }
        
        private bool CanPlaceShapeData(ShapeData shape)
        {
            var grid = GridManager.Instance.gridSquares;
            if (grid == null)
                return false;
            
            int gridRows = GridManager.Instance.rows;
            int gridCols = GridManager.Instance.cols;
            if (shape.rows == null || shape.rows.Length == 0 || shape.rows[0].columns == null)
                return false;
            
            int shapeRows = shape.rows.Length;
            int shapeCols = shape.rows[0].columns.Length;

            for (int yOffset = 0; yOffset <= gridRows - shapeRows; yOffset++)
            {
                for (int xOffset = 0; xOffset <= gridCols - shapeCols; xOffset++)
                {
                    bool canPlace = CanPlace(shape, shapeRows, shapeCols, grid, yOffset, xOffset);

                    if (canPlace) return true;
                }
            }

            return false;
        }

        private bool CanPlace(ShapeData shape, int shapeRows, int shapeCols, GridSquare[,] grid, int yOffset, int xOffset)
        {
            bool canPlace = true;

            for (int y = 0; y < shapeRows; y++)
            {
                for (int x = 0; x < shapeCols; x++)
                {
                    if (shape.rows[y].columns[x])
                    {
                        if (grid[y + yOffset, x + xOffset].IsOccupied == true)
                        {
                            canPlace = false;
                            break;
                        }
                    }
                }
                if (!canPlace) break;
            }

            return canPlace;
        }

        public void OnBlockPlaced(Block placedBlock)
        {
            _currentBlocks.Remove(placedBlock);

            if (_currentBlocks.Count == 0)
            {
                GenerateAllBlocks();
            }
        }
    }
}
