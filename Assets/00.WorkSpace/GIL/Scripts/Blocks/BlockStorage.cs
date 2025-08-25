using System.Collections.Generic;
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
            
        private float[] _cumulativeWeights;
        private float _totalWeight;
        
        void Awake()
        {
            BuildCumulativeTable();
        }

        private void BuildCumulativeTable()
        {
            _cumulativeWeights = new float[shapeData.Count];
            _totalWeight = 0;
            
            for (int i = 0; i < shapeData.Count; i++)
            {
                _totalWeight += shapeData[i].chanceForSpawn;
                _cumulativeWeights[i] = _totalWeight;
            }
        }

        void Start()
        {
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

            foreach (var spawnPos in blockSpawnPosList)
            {
                GameObject newBlockObj = Instantiate(
                    blockPrefab, 
                    spawnPos.position, 
                    Quaternion.identity, 
                    shapesPanel);
            
                Block newBlock = newBlockObj.GetComponent<Block>();

                ShapeData selectedShape = GetRandomShapeByWeight();
                newBlock.GenerateBlock(selectedShape);

                _currentBlocks.Add(newBlock);
            }
        }
        
        private ShapeData GetRandomShapeByWeight()
        {
            float randomValue = Random.Range(0f, _totalWeight);

            for (int i = 0; i < _cumulativeWeights.Length; i++)
            {
                if (randomValue <= _cumulativeWeights[i])
                    return shapeData[i];
            }

            return shapeData[shapeData.Count - 1];
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
