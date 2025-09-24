using System.Collections.Generic;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    /// <summary>
    /// v1.7.0 웨이브 생성 루틴(넘버링 + 계획/하위단계 로그 포함)
    /// - 슬롯마다: a) 블럭을 고르기 → ㄴ 내부 코드 단계들 … / b) 실패 처리 → ㄴ 라인 보정 …
    /// - Tracker.cs ( partial class 에 있음 ) API 사용(TBegin/TPlan/TDo/TDo2/TSnapshot/TSlotEnd/TDump)
    /// </summary>
    public partial class BlockSpawnManager
    {
        [Header("V1.7.0 (문서 탭 3)")]
        [Range(0f, 2f)] public float v170_exponentA = 1.0f; // (1/tiles)^a
        [Range(0f, 2f)] public float v170_exponentBeta = 1.0f; // (groupCount)^b — 빈칸 수로 런타임 산출
        [Range(0f, 2f)] public float v170_exponentC = 1.0f; // (difficulty)^c
        [Range(0f, 1f)] public float v170_spawnSuccessProb = 0.90f; // 슬롯별 성공 확률(실패 시 라인보정 시도)

        public List<ShapeData> GenerateWaveV170(int count = 3)
        {
            TBegin("GenerateWaveV170 : 문서 탭3 기반 웨이브 생성 시작");

            var gm = GridManager.Instance;
            if (gm == null || gm.gridSquares == null)
            {
                T("GridManager/gridSquares null");
                TDump(title: "GenerateWaveV170 : ABORT");
                return new List<ShapeData>(0);
            }

            // 예약 보드 스냅샷에서 시작
            bool[,] board = SnapshotBoard();

            // 전역 준비 단계(넘버링): b-i), b-ii)
            v170_exponentBeta = V170_ComputeBetaByVacancy(board);
            Tn("ComputeBByVacancy", $"ComputeBByVacancy : b={v170_exponentBeta}");

            float alpha = ComputeAForGate();
            Tn("ComputeAForGate", $"ComputeAForGate : a={alpha}");

            var wave = new List<ShapeData>(count);
            var fits = new List<FitInfo>(count);
            var perShapeCount     = new Dictionary<string, int>();
            var excludedByPenalty = new HashSet<string>();
            var excludedByDupes   = new HashSet<string>();

            for (int i = 0; i < count; i++)
            {
                int snap = TSnapshot();
                TSlotBegin(i);

                T("a. 보드 탐색 및 탐색 시작점 랜덤 선정 / 기획서");

                // 성공/실패 롤 → 실패 시 라인보정 우선 시도 (b) 분기)
                if (Random.value > v170_spawnSuccessProb)
                {
                    if (TryApplyLineCorrectionOnce(board, excludedByPenalty, excludedByDupes, out var sLC, out var fLC))
                    {
                        TDo($"라인 보정 성공 → {sLC?.Id} 선택");
                        wave.Add(sLC); fits.Add(fLC);
                        IncreaseDupes(sLC.Id, excludedByDupes);
                        ApplyFitOnBoard(fLC, board);
                        RemoveFullLines(board);
                        TSlotEndEx(i, snap, $"Pick : {sLC?.Id} [LineCorrection]");
                        continue;
                    }
                    TDo("라인 보정 실패 -> 일반 경로로 이동");
                }

                // === 그룹화/가중치/선택 ===
                var groups = new Dictionary<int, List<(ShapeData s, FitInfo fit)>>();
                T("b. 해당 칸에 들어갈 수 있는 블록들 탐색 / 기획서");
                foreach (var s in shapeData)
                {
                    if (s == null) continue;
                    if (excludedByPenalty.Contains(s.Id)) continue;
                    if (excludedByDupes.Contains(s.Id)) continue;

                    if (TryFindFitFromRandomStart(board, s, out var fit))
                    {
                        int groupsChosenTiles = Mathf.Max(1, s.activeBlockCount);
                        if (!groups.TryGetValue(groupsChosenTiles, out var list))
                        {
                            list = new List<(ShapeData, FitInfo)>();
                            groups[groupsChosenTiles] = list;
                        }
                        list.Add((s, fit));
                    }
                }
                
                int totalPlaceables = 0;
                foreach (var kv in groups) totalPlaceables += kv.Value.Count;
                TDo($"배치 가능한 블록 {totalPlaceables} 개 발견");            // b 요약
                T("b.i. 배치 가능한 블록들을 동일 타일 수 그룹으로 분리");       // b.i 제목
                foreach (var kv in groups)
                    TDo($"타일 수 {kv.Key} 개 블록 {kv.Value.Count} 개 발견");   // b.i 나열

                if (groups.Count == 0)
                {
                    TDo("배치 가능한 그룹이 없어 폴백 경로 시도");
                    if (TryPickWeightedAmongPlaceableFromRandom(board, excludedByPenalty, excludedByDupes, alpha, out var sFB, out var fFB))
                    {
                        wave.Add(sFB); fits.Add(fFB);
                        IncreaseDupes(sFB.Id, excludedByDupes);
                        ApplyFitOnBoard(fFB, board);
                        RemoveFullLines(board);
                        TSlotEndEx(i, snap, $"Pick : {sFB?.Id} [Fallback]");
                        continue;
                    }
                    TSlotEndEx(i, snap, "Pick : <none>");
                    break;
                }

                var groupWeights = new List<(int tile, float w)>(groups.Count);
                foreach (var kv in groups)
                {
                    int tileCount = Mathf.Max(1, kv.Key);
                    int cnt   = Mathf.Max(1, kv.Value.Count);
                    float w   = Mathf.Pow(1f / tileCount, Mathf.Max(0.0001f, v170_exponentA)) * Mathf.Pow(cnt, v170_exponentBeta);
                    groupWeights.Add((tileCount, w));
                }

                int chosenTile = WeightedPickKey(groupWeights, out _);

                T("b.ii. 타일 수 가중치 계산을 통해 블록 그룹 선택 / 기획서");
                TDo($"{groups.Count} 개 그룹중 {chosenTile} 그룹 선정");

                var inGroup   = groups[chosenTile];
                var inWeights = new List<(int idx, float w)>(inGroup.Count);
                for (int k = 0; k < inGroup.Count; k++)
                {
                    var s = inGroup[k].s;
                    float d = V170_GetDifficulty(s);
                    float w = Mathf.Pow(d, v170_exponentC);
                    inWeights.Add((k, w));
                }

                int chosenIdx = WeightedPickIndex(inWeights, out _);
                var chosen    = inGroup[chosenIdx];

                T("b.iii. 그룹 내 난이도 가중치 계산을 통해 블록 선정 / 기획서");
                TDo($"{inGroup.Count} 그룹 내부에서 {chosen.s.Id} 선정");

                int chosenTiles = Mathf.Max(1, chosen.s.activeBlockCount);
                float gateProb  = Mathf.Pow(1f / chosenTiles, Mathf.Max(0.0001f, alpha));
                float gateRoll = Random.value;
                bool gatePassed = gateRoll <= gateProb;
                T($"b.iv. 블록의 타일수가 {chosenTiles} 개이기 때문에 추가 확률 진행 / 기획서");
                T(gatePassed ? "b.iv.1. 확률 통과 성공, c로 이동" : "b.iv.1. 확률 통과 실패, b.iv.2 이동");

                if (perShapeCount.TryGetValue(chosen.s.Id, out int dupCnt) && dupCnt >= maxDuplicatesPerWave)
                {
                    TDo("동일 블록 한도 → 대안 탐색");
                    bool swapped = false;
                    for (int guard = 0; guard < inGroup.Count; guard++)
                    {
                        int altIdx = (chosenIdx + 1 + guard) % inGroup.Count;
                        var alt = inGroup[altIdx];
                        if (alt.s.Id != chosen.s.Id)
                        { chosenIdx = altIdx; chosen = alt; swapped = true; break; }
                    }
                    if (!swapped) TDo("대안 없음 → 그대로 진행");
                }

                wave.Add(chosen.s); fits.Add(chosen.fit);
                if (!perShapeCount.TryGetValue(chosen.s.Id, out var pc)) pc = 0;
                perShapeCount[chosen.s.Id] = pc + 1;
                IncreaseDupes(chosen.s.Id, excludedByDupes);
                ApplyFitOnBoard(chosen.fit, board);
                RemoveFullLines(board);

                T("c. 소환된 블록의 위치 정보 저장, 탐색 위치 제외");

                TSlotEndEx(i, snap, $"Pick : {chosen.s?.Id}"); // 슬롯별 버퍼에도 기록
            }

            string key = MakeWaveHistory(wave);
            RegisterWaveHistory(key);
            RecomputeFitsForWave(wave);

            TDumpSplit(); // 도입부/슬롯별/반복요약 '개별 로그' 출력
            return wave;
        }

        // 로컬 유틸
        private static int WeightedPickKey(List<(int key, float w)> weights, out float sumOut)
        {
            sumOut = 0f;
            for (int i = 0; i < weights.Count; i++) sumOut += Mathf.Max(0f, weights[i].w);
            if (sumOut <= 0f) return weights.Count > 0 ? weights[0].key : 0;
            float r = Random.Range(0f, sumOut);
            for (int i = 0; i < weights.Count; i++) { r -= Mathf.Max(0f, weights[i].w); if (r <= 0f) return weights[i].key; }
            return weights[^1].key;
        }

        private static int WeightedPickIndex(List<(int idx, float w)> weights, out float sumOut)
        {
            sumOut = 0f;
            for (int i = 0; i < weights.Count; i++) sumOut += Mathf.Max(0f, weights[i].w);
            if (sumOut <= 0f) return 0;
            float r = Random.Range(0f, sumOut);
            for (int i = 0; i < weights.Count; i++) { r -= Mathf.Max(0f, weights[i].w); if (r <= 0f) return weights[i].idx; }
            return weights[^1].idx;
        }

        private static float V170_GetDifficulty(ShapeData s)
        {
            return Mathf.Max(1, s?.activeBlockCount ?? 1);
        }

        private static float V170_ComputeBetaByVacancy(bool[,] board)
        {
            int zeros = 0;
            int rows = board.GetLength(0), cols = board.GetLength(1);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (!board[r, c]) zeros++;

            if      (zeros >= 40) return 1.2f;
            else if (zeros >= 30) return 1.1f;
            else if (zeros >= 20) return 1.0f;
            else if (zeros >= 10) return 0.9f;
            else                  return 0.8f;
        }
    }
}
