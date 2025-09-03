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
        
// 점수→a (가중치에는 아직 쓰지 않고 오직 성공확률에만 사용)
        private float ComputeAForGate()
        {
            int score = 0;
            if (ScoreManager.Instance != null) score = ScoreManager.Instance.Score;

            // a = 0.3 + 0.1 * floor(score/2000), 최대 1.0
            aExponent = 0.3f + 0.1f * Mathf.Floor(score / 2000f);
            return Mathf.Clamp(aExponent, aMin, aMax);
        }

        private float EvalSmallBlockSuccessPercent(int tileCount, float a)
        {
            if (tileCount > 3) return 100f; // 작은 블록만 게이트

            // a를 0~1로 정규화 후 선형보간
            float t = Mathf.InverseLerp(aMin, aMax, a);

            for (int i = 0; i < smallBlockGates.Length; i++)
            {
                if (smallBlockGates[i].tiles == tileCount)
                {
                    return Mathf.Lerp(
                        smallBlockGates[i].percentAtAMin,
                        smallBlockGates[i].percentAtAMax,
                        t
                    );
                }
            }

            // 설정이 비어있으면 통과
            return 100f;
        }

        private bool PassSmallBlockGate(ShapeData s, float a)
        {
            if (!useSmallBlockSuccessGate) return true;
            
            int n = s.activeBlockCount;
            if (n > 3) return true;
            
            float p = EvalSmallBlockSuccessPercent(n, a) * 0.01f;
            bool isSuccess = Random.value < p;
#if UNITY_EDITOR            
            if (isSuccess)
            {
                Debug.LogWarning($"{p}의 확률, {a}의 계수 a로 {s.Id} 생성 성공!");
            }
            else
            {
                Debug.LogWarning($"{p}의 확률, {a}의 계수로 {s.Id} 생성 실패!");
            }
#endif            
            return isSuccess;
        }
    }
}