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
            // 중복 제한, 반복 구성 방지 계산용 카운터
            var perShapeCount = new Dictionary<string, int>();
            // a 지수
            var aForGate = ComputeAForGate();
            
            bool[,] waveBoard = reserveCellsDuringWave ? SnapshotBoard() : null;
            
            for (int i = 0; i < count; i++)
            {
                ShapeData chosen = null;
                FitInfo fitFromStep3 = default;
                FitInfo fitToCommit = default;
                int guard = 0; // 무한 루프 방지

                while (chosen == null && guard++ < shapeData.Count)
                {
                    ShapeData pick = null;
                    
                    bool pickedOnReserveBoard = false;
                    if (reserveCellsDuringWave)
                    {
                        //예약 보드를 기준으로 실제 배치 가능한 집합에서 가중치 선택
                        pickedOnReserveBoard = TryPickWeightedAmongPlaceableFromRandom(waveBoard, excludedByPenalty, excludedByDupes, aForGate, out pick, out fitFromStep3);
                    }
                    if (!pickedOnReserveBoard)
                    {
                        pick = GetRandomShapeByWeightExcluding(excludedByPenalty, excludedByDupes);
                    }
                    // 후보가 전무하면 기존 방식으로 폴백
                    if (pick == null) break;

                    // 소형 여부를 activeBlockCount로 판정
                    if (!PassSmallBlockGate(pick, aForGate))
                    {
                        var boardRef = reserveCellsDuringWave ? waveBoard : SnapshotBoard();
                        if (TryApplyLineCorrectionOnce(boardRef, excludedByPenalty, excludedByDupes, out var correctedShape, out var correctedFit))
                        {
                            chosen = correctedShape; // 보정 성공 → 이걸 채택
                            if (reserveCellsDuringWave) fitToCommit = correctedFit;
                            break;
                        }
                        excludedByPenalty.Add(pick.Id);
                        continue;
                    }
                    // 성공
                    chosen = pick;
                    
                    // 예약 모드면 최종 커밋용 Fit 확보 (step3에서 이미 갖고 오거나, 폴백이면 새로 탐색)
                    if (!reserveCellsDuringWave) continue;
                    if (pickedOnReserveBoard)
                    {
                        fitToCommit = fitFromStep3;
                    }
                    else
                    {
                        // 폴백으로 뽑힌 경우, 현재 예약 보드 기준으로 실제 배치 위치를 찾아야 함
                        if (TryFindFitFromRandomStart(waveBoard, chosen, out fitToCommit)) continue;
                        // 예약 보드에선 배치 불가 → 실패로 간주하고 다음 후보를 계속 탐색
                        chosen = null;
                        excludedByPenalty.Add(pick.Id);
                    }
                }
                // 소형 패널티는 무시하지만, 중복 한도 제외는 반드시 시킴
                if (chosen == null)
                {
                    Debug.LogWarning("모든 후보가 소형 페널티로 제외되어 가중치 강제 소환을 수행합니다.");                    
                    chosen = GetRandomShapeByWeightExcluding(null, excludedByDupes) ?? GetRandomShapeByWeight();
                }
                
                // 최종 확정 시, 예약 보드에 점유 마킹
                if (reserveCellsDuringWave && fitToCommit.CoveredSquares != null) ReserveAndResolveLines(waveBoard, chosen, fitToCommit);
                
                result.Add(chosen);
                
                string chosenId = chosen.Id;
                perShapeCount.TryGetValue(chosenId, out int cnt);
                cnt++;
                perShapeCount[chosenId] = cnt;
                if (cnt < maxDuplicatesPerWave) continue;
                Debug.Log($"{maxDuplicatesPerWave}이상 중복됨, 다음 선택에서 제외");
                excludedByDupes.Add(chosenId); // 다음 선택에서 제외.
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
                foreach (var s in shapeData)
                {
                    if (s == null) continue;

                    // 같은 ID로 교체하면 구성 안 바뀌므로 제외
                    if (s.Id == oldId) continue;

                    // 중복 한도 위반이면 제외
                    perShapeCount.TryGetValue(s.Id, out int cur);
                    if (cur >= maxDuplicatesPerWave) continue;

                    // (선택) 소형 페널티는 이 단계에서 무시 — “반복 깨기”가 우선
                    int tiles = s.activeBlockCount;
                    if (tiles <= bestTiles) continue;
                    bestTiles = tiles;
                    candidate = s;
                }

                if (candidate != null)
                {
                    // 카운트 갱신
                    if (perShapeCount.TryGetValue(oldId, out int oc) && oc > 0)
                        perShapeCount[oldId] = oc - 1;
                    perShapeCount.TryGetValue(candidate.Id, out int cc);
                    perShapeCount[candidate.Id] = cc + 1;

                    result[idx] = candidate;
                    wave = MakeWaveHistory(result); // 새 구성
                }
                else
                {
                    // 모든 후보가 중복 한도로 막혔을 때 최후에 아무거나 한 번 더 시도
                    var fallback = GetRandomShapeByWeightExcluding(null, excludedByDupes);
                    if (fallback != null && fallback.Id != oldId)
                    {
                        if (perShapeCount.TryGetValue(oldId, out int oc2) && oc2 > 0)
                            perShapeCount[oldId] = oc2 - 1;
                        perShapeCount.TryGetValue(fallback.Id, out int fc);
                        perShapeCount[fallback.Id] = fc + 1;
                        
                        result[idx] = fallback;
                        wave = MakeWaveHistory(result);
                    }
                }
            }
            
            bool anyPlaceable = false;
            foreach (var t in result)
            {
                if (t != null && CanPlaceShapeData(t)) { anyPlaceable = true; break; }
            }
            if (!anyPlaceable)
            {
                if (TryGuaranteePlaceableWave(result, out int rIdx2, out var nShape2, out var nFit2))
                {
                    string oldId2 = result[rIdx2].Id;
                    if (perShapeCount.TryGetValue(oldId2, out int oc3) && oc3 > 0)
                        perShapeCount[oldId2] = oc3 - 1;

                    perShapeCount.TryGetValue(nShape2.Id, out int nc3);
                    perShapeCount[nShape2.Id] = nc3 + 1;

                    result[rIdx2] = nShape2;
                    wave = MakeWaveHistory(result); // 최종 구성으로 갱신
                }
            }
            
            // 이력 등록(직전 N-1개만 유지)
            RegisterWaveHistory(wave);
            return result;
        }
    }
}