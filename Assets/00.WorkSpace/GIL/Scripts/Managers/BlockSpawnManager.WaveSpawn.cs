using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
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
            TBegin("GenerateBasicWave : 시작");

            var result = new List<ShapeData>(count);
            var fitsForWave = new List<FitInfo>(count);
            
            var Map = MapManager.Instance;
            // 튜토리얼일 경우 고정 블록 소환
            if (Map.CurrentMode == GameMode.Tutorial)
            {
                Debug.Log("[BlockSpawnManager] : 튜토리얼 블록 생성 시작 ");
                result.Add(null);
                result.Add(shapeData[31]);
                result.Add(null);

                var squares = GridManager.Instance.gridSquares;
                var fits = new List<FitInfo>(3);
                
                var tutorialFitInfo = new FitInfo()
                {
                    Offset = new Vector2Int(3, 3),
                    CoveredSquares = new List<GridSquare>(4)
                    {
                        squares[3, 3],
                        squares[3, 4],
                        squares[4, 3],
                        squares[4, 4]
                    }
                };
                
                fits.Add(default);
                fits.Add(tutorialFitInfo);
                fits.Add(default);
                SetLastGeneratedFits(fits);
                return result;
            }
            Debug.Log("[BlockSpawnManager] : 일반 블록 Wave 생성 시작 ");
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
                        pickedOnReserveBoard = TryPickWeightedAmongPlaceableFromRandom(
                            waveBoard, excludedByPenalty, excludedByDupes, aForGate, 
                            out pick, out fitFromStep3);
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
                        if (TryApplyLineCorrectionOnce(boardRef, excludedByPenalty, excludedByDupes, 
                                out var correctedShape, out var correctedFit))
                        {
                            chosen = correctedShape; // 보정 성공 → 이걸 채택
                            fitToCommit = correctedFit;
                            break;
                        }
                        excludedByPenalty.Add(pick.Id);
                        continue;
                    }
                    // 성공
                    chosen = pick;
                    
                    // 예약 모드면 최종 커밋용 fit 확보
                    if (reserveCellsDuringWave)
                    {
                        if (pickedOnReserveBoard)
                        {
                            fitToCommit = fitFromStep3;
                        }
                        else
                        {
                            // 폴백으로 뽑혔으면 현재 예약 보드에서 자리 찾기(못 찾으면 다음 후보 계속)
                            if (!TryFindFitFromRandomStart(waveBoard, chosen, out fitToCommit))
                            {
                                chosen = null;
                                excludedByPenalty.Add(pick.Id);
                            }
                        }
                    }
                    else
                    {
                        // 예약 모드가 아니어도 “당시 보드 스냅샷” 기준으로 1회만 자리 기록(재탐색 금지)
                        // 이후 블록 때문에 겹칠 수 있으니, 정확 일치가 필요하면 reserveCellsDuringWave를 켜두는 걸 권장
                        TryFindFitFromRandomStart(SnapshotBoard(), chosen, out fitToCommit); // 실패해도 default 저장
                    }
                }
                // 소형 패널티는 무시하지만, 중복 한도 제외는 반드시 시킴
                if (chosen == null)
                {
                    Debug.LogWarning("모든 후보가 소형 페널티로 제외되어 가중치 강제 소환을 수행합니다.");
                    chosen = GetRandomShapeByWeightExcluding(null, excludedByDupes) ?? GetRandomShapeByWeight();
                    // 강제 소환도 “당시 보드 스냅샷” 기준으로 1회만 fit 기록(없으면 default)
                    if (!reserveCellsDuringWave)
                        TryFindFitFromRandomStart(SnapshotBoard(), chosen, out fitToCommit);
                }
                
                // 최종 확정 시, 예약 보드에 점유 마킹
                if (reserveCellsDuringWave && fitToCommit.CoveredSquares != null) 
                    ReserveAndResolveLines(waveBoard, chosen, fitToCommit);
                
                result.Add(chosen);
                fitsForWave.Add(fitToCommit);
                
                string chosenId = chosen.Id;
                perShapeCount.TryGetValue(chosenId, out int cnt);
                cnt++;
                perShapeCount[chosenId] = cnt;
                if (cnt < maxDuplicatesPerWave) continue;
                Debug.Log($"{maxDuplicatesPerWave}이상 중복됨, 다음 선택에서 제외");
                excludedByDupes.Add(chosenId); // 다음 선택에서 제외.
            }
            
            // // 여기서 3연속 방지 검사/치환
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
            
                    // 소형 페널티는 이 단계에서 무시 — “반복 깨기”가 우선
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
            RecomputeFitsForWave(result);
            
            T("GenerateBasicWave : 종료");
            TDump(); // 디버그 종료, 출력
            
            return result;
        }
    }
}