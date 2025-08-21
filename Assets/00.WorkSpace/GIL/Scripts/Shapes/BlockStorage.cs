using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockStorage : MonoBehaviour
{
    [Header("Block Prefab & Data")]
    public List<ShapeData> shapeData;
    public List<Block> blockList;

    void Start()
    {
        foreach (Block blockGenerator in blockList)
        {
            var blockIndex = Random.Range(0, shapeData.Count);
            blockGenerator.GenerateBlock(shapeData[blockIndex]);
        }
    }
}
