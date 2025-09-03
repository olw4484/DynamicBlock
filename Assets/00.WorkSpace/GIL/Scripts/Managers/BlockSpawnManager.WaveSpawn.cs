using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        /// <summary>
        /// 가중치 기반으로 블럭 생성, 3개 이하 블럭에 대해서는 별도의 생성 확률 적용
        /// </summary>
        public List<ShapeData> GenerateBasicWave(int count)
        {
            var result = new List<ShapeData>(count);
            
            // 이번 웨이브에서 "소환 실패한 블록" 은 이후 검색에서 제외
            var excludedByPenalty = new HashSet<string>();
            var excludedByDupes = new HashSet<string>();
            var perShapeCount = new Dictionary<string, int>();
            
            for (int i = 0; i < count; i++)
            {
                ShapeData chosen = null;
                int guard = 0; // 무한 루프 방지

                while (chosen == null && guard++ < shapeData.Count)
                {
                    // 제외 목록 반영해서 가중치 추첨
                    var pick = GetRandomShapeByWeightExcluding(excludedByPenalty, excludedByDupes);
                    if (pick == null) break; // 전부 제외된 경우

                    // 소형 여부를 activeBlockCount로 판정
                    bool isSmall = pick.activeBlockCount <= smallBlockTileThreshold;
                    if (smallBlockPenaltyMode && isSmall && Random.value < smallBlockFailRate)
                    {
                        // 소환 실패 → 이번 웨이브에서 제외하고 재검색
                        excludedByPenalty.Add(pick.Id);
                        continue;
                    }

                    // 성공
                    chosen = pick;
                }
                // 소형 패널티는 무시하지만, 중복 한도 제외는 반드시 시킴
                if (chosen == null)
                {
                    chosen = GetRandomShapeByWeightExcluding(null, excludedByDupes);
                    // 그래도 못 뽑았으면 가중치 기반 생성 ( 4개 이상 블럭은 삭제되지 않아서 발생할 확률이 없음 )
                    if (chosen == null)
                    {
                        Debug.LogWarning("모든 후보가 소형 페널티로 제외되어 가중치 강제 소환을 수행합니다.");                    
                        chosen = GetRandomShapeByWeight();
                    }
                }
                result.Add(chosen);
                
                string chosenId = chosen.Id;
                perShapeCount.TryGetValue(chosenId, out int cnt);
                cnt++;
                perShapeCount[chosenId] = cnt;
                if (cnt >= maxDuplicatesPerWave)
                {
#if UNITY_EDITOR
                    Debug.Log($"{maxDuplicatesPerWave}이상 중복됨, 다음 선택에서 제외");
#endif                    
                    excludedByDupes.Add(chosenId); // 다음 선택에서 제외.
                }
            }
            
            // 여기서 3연속 방지 검사/치환
            string wave = MakeWaveHistory(result);
            
            if (WouldBecomeNStreak(wave, maxSameWaveStreak))
            {
                // 교체할 인덱스 선택(랜덤 하나)
                int idx = Random.Range(0, result.Count);
                string oldId = result[idx].Id;

                // 교체 후보: 중복 한도 및 현재 카운트 고려, '더 큰 타일 수' 우선
                ShapeData candidate = null; int bestTiles = int.MinValue;
                for (int i = 0; i < shapeData.Count; i++)
                {
                    var s = shapeData[i];
                    if (s == null) continue;

                    // 같은 ID로 교체하면 구성 안 바뀌므로 제외
                    if (s.Id == oldId) continue;

                    // 중복 한도 위반이면 제외
                    perShapeCount.TryGetValue(s.Id, out int cur);
                    if (cur >= maxDuplicatesPerWave) continue;

                    // (선택) 소형 페널티는 이 단계에서 무시 — “반복 깨기”가 우선
                    int tiles = s.activeBlockCount;
                    if (tiles > bestTiles)
                    {
                        bestTiles = tiles;
                        candidate = s;
                    }
                }

                if (candidate != null)
                {
                    // 카운트 갱신
                    perShapeCount[oldId] -= 1;
                    perShapeCount.TryGetValue(candidate.Id, out int cc);
                    perShapeCount[candidate.Id] = cc + 1;

                    result[idx] = candidate;
                    wave = MakeWaveHistory(result); // 새 구성
                }
                else
                {
                    // 모든 후보가 중복 한도로 막혔을 때: last resort로 아무거나 한 번 더 시도
                    var fallback = GetRandomShapeByWeightExcluding(null, excludedByDupes);
                    if (fallback != null && fallback.Id != oldId)
                    {
                        perShapeCount[oldId] -= 1;
                        perShapeCount.TryGetValue(fallback.Id, out int fc);
                        perShapeCount[fallback.Id] = fc + 1;
                        result[idx] = fallback;
                        wave = MakeWaveHistory(result);
                    }
                }
            }

            // 이력 등록(직전 N-1개만 유지)
            RegisterWaveHistory(wave);
            
            return result;
        }
        private ShapeData GetRandomShapeByWeightExcluding(HashSet<string> exPenalty, HashSet<string> exDupes)
        {
            if (shapeData == null || shapeData.Count == 0) return null;

            var total = 0;
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if ((exPenalty != null && exPenalty.Contains(s.Id)) ||
                    (exDupes   != null && exDupes.Contains(s.Id))) continue;
                total += s.chanceForSpawn;
            }
            if (total <= 0) return null;

            var r = Random.Range(0, total); // [0,total)
            var acc = 0;
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if ((exPenalty != null && exPenalty.Contains(s.Id)) ||
                    (exDupes   != null && exDupes.Contains(s.Id))) continue;
                acc += s.chanceForSpawn;
                if (r < acc) return s;
            }
            return null;
        }
    }
}