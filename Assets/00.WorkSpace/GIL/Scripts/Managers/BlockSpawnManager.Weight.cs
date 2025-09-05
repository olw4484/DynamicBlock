using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        private float ComputeAForGate()
        {
            var score = 0;
            if (ScoreManager.Instance != null) score = ScoreManager.Instance.Score;

            // a = 0.3 + 0.1 * floor(score/2000), 최대 1.0
            aExponent = 0.3f + 0.1f * Mathf.Floor(score / 2000f);
            return Mathf.Clamp(aExponent, aMin, aMax);
        }

        private float EvalSmallBlockSuccessPercent(int tileCount, float a)
        {
            if (tileCount > 3) return 100f; // 작은 블록만 게이트

            // a를 0~1로 정규화 후 선형보간
            var t = Mathf.InverseLerp(aMin, aMax, a);

            for (var i = 0; i < smallBlockGates.Length; i++)
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
            
            var n = s.activeBlockCount;
            if (n > 3) return true;
            
            var p = EvalSmallBlockSuccessPercent(n, a) * 0.01f;
            var isSuccess = Random.value < p;
#if UNITY_EDITOR
            Debug.LogWarning(isSuccess ? $"{p}의 확률, {a}의 계수 a로 {s.Id} 생성 성공!" : $"{p}의 확률, {a}의 계수로 {s.Id} 생성 실패!");
#endif            
            return isSuccess;
        }
    }
}