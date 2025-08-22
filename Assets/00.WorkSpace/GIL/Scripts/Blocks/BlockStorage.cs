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
    
        private List<Block> currentBlocks = new();
    
        void Start()
        {
            GenerateAllBlocks();
        }

        private void GenerateAllBlocks()
        {
            foreach (var blk in currentBlocks)
            {
                if (blk != null)
                    Destroy(blk.gameObject);
            }
            currentBlocks.Clear();

            foreach (var spawnPos in blockSpawnPosList)
            {
                GameObject newBlockObj = Instantiate(
                    blockPrefab, 
                    spawnPos.position, 
                    Quaternion.identity, 
                    shapesPanel);
            
                Block newBlock = newBlockObj.GetComponent<Block>();

                int blockIndex = Random.Range(0, shapeData.Count);
                newBlock.GenerateBlock(shapeData[blockIndex]);

                currentBlocks.Add(newBlock);
            }
        
        }
    
        public void OnBlockPlaced(Block placedBlock)
        {
            currentBlocks.Remove(placedBlock);

            if (currentBlocks.Count == 0)
            {
                GenerateAllBlocks();
            }
        }
    }
}
