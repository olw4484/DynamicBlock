using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
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
        
        private ShapeData GetRandomShapeByWeight()
        {
            if (shapeData == null || shapeData.Count == 0) return null;
            if (_cumulativeWeights == null || _cumulativeWeights.Length != shapeData.Count) BuildCumulativeTable();

            int r = Random.Range(0, Mathf.Max(1, _totalWeight));
            for (int i = 0; i < _cumulativeWeights.Length; i++)
            {
                if (r < _cumulativeWeights[i])
                {
                    return shapeData[i];
                }
            }
            
            return shapeData[^1];
        }
    }
}