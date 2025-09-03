using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        /// <summary>tile^a 기반 누적 가중치 테이블 생성</summary>
        private void BuildDynamicWeightTable(float a)
        {
            if (!useDynamicWeightByTilePowA) return;

            if (shapeData == null || shapeData.Count == 0)
            {
                _dynCumulativeWeights = null;
                _dynTotalWeight = 0;
                _lastAForWeights = a;
                return;
            }

            _dynCumulativeWeights = new int[shapeData.Count];
            int total = 0;

            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                int tiles = Mathf.Max(0, s.activeBlockCount);
                float w = tiles > 0 ? Mathf.Pow(tiles, a) : 0f;

                // 정수화 (최소 0; 0이면 실제로는 뽑히지 않음)
                int iw = Mathf.Max(0, Mathf.RoundToInt(w * _dynamicWeightScale));

                total += iw;
                _dynCumulativeWeights[i] = total;
            }

            _dynTotalWeight = total;
            _lastAForWeights = a;
        }

        /// <summary>동적 누적표에서 인덱스 하나 추첨</summary>
        private int PickIndexByDynamicWeights()
        {
            if (_dynCumulativeWeights == null || _dynTotalWeight <= 0) return -1;

            int r = Random.Range(0, _dynTotalWeight); // max 미포함
            for (int i = 0; i < _dynCumulativeWeights.Length; i++)
                if (r < _dynCumulativeWeights[i]) return i;

            return _dynCumulativeWeights.Length - 1;
        }

        /// <summary>
        /// tile^a 기반 가중치로 Shape 하나 추첨 (레거시 대비 래퍼)
        /// </summary>
        private ShapeData GetRandomShapeByWeight()
        {
            if (useDynamicWeightByTilePowA)
            {
                // a 값이 바뀌었거나, 테이블이 비었으면 재생성
                float a = ComputeAForGate(); // 이전 단계에서 만든 a 계산 함수 재사용
                if (!Mathf.Approximately(a, _lastAForWeights) || _dynCumulativeWeights == null)
                    BuildDynamicWeightTable(a);

                int idx = PickIndexByDynamicWeights();
                if (idx >= 0) return shapeData[idx];
            }

            // 폴백(혹시 동적 가중치가 0이거나 비정상일 때)
            return shapeData[Random.Range(0, shapeData.Count)];
        }

        /// <summary>
        /// 제외 목록을 고려하여 tile^a 가중치로 추첨
        /// </summary>
        private ShapeData GetRandomShapeByWeightExcluding(HashSet<string> excludedByPenalty,
                                                          HashSet<string> excludedByDupes)
        {
            // 여러 번 시도해서 제외되지 않는 후보를 찾음
            for (int guard = 0; guard < 50; guard++)
            {
                var cand = GetRandomShapeByWeight();
                if (cand == null) break;

                if (excludedByPenalty != null && excludedByPenalty.Contains(cand.Id)) continue;
                if (excludedByDupes   != null && excludedByDupes.Contains(cand.Id)) continue;

                return cand;
            }

            // 폴백: 균등 추출로라도 하나 반환 (모두 제외면 null)
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if ((excludedByPenalty != null && excludedByPenalty.Contains(s.Id)) ||
                    (excludedByDupes   != null && excludedByDupes.Contains(s.Id))) continue;
                return s;
            }

            return null;
        }
    }
}