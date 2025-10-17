using System.Collections.Generic;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        /// <summary>
        /// Revive(게임오버 직후) 전용 웨이브 생성:
        /// 1) 라인 보정으로 정확히 라인을 지우는 1개 블록(균등 확률) 선택
        /// 2) 가상 보드에 배치 + 라인 제거 시뮬
        /// 3) 남은 (count-1)개는 역가중치(tiles^-a)로, 현재 보드에 실제 배치 가능한 후보 중에서 선택
        /// 4) 모자라면 재롤 → 끝까지 실패 시 Safety Wave로 대체
        /// 5) 중복 한도 / 동일 구성 n연속 방지 후 이력 반영
        /// </summary>
        public bool TryGenerateReviveWave(int count, out List<ShapeData> wave, out List<FitInfo> fits)
        {
            if (count <= 0) count = 3;
            wave = new List<ShapeData>(count);
            fits = new List<FitInfo>(count);
            _dupeCounter.Clear();

            // 현재 보드 스냅샷
            var board = SnapshotBoard();

            // 1) 현재 보드에서 "실제로 배치 가능한" 전체 후보 수집
            var placeables = new List<(ShapeData s, FitInfo fit)>();
            foreach (var s in shapeData)
            {
                if (s == null) continue;
                if (IsDupesExceeded(s.Id)) continue;
                if (TryFindFitFromRandomStart(board, s, out var fit))
                    placeables.Add((s, fit));
            }
            if (placeables.Count == 0) return false; // 진짜 막힌 상태면 리바이브 불가

            // 2) 우선 1장은 "라인 보정(즉시 제거)" 가능한 것 중에서 선택
            (ShapeData s, FitInfo fit) lcPick = default;
            bool hasLC = TryApplyLineCorrectionOnce(board, null, null, out var lcShape, out var lcFit);
            if (hasLC)
            {
                lcPick = (lcShape, lcFit);
                wave.Add(lcPick.s); fits.Add(lcPick.fit); IncreaseDupes(lcPick.s.Id, null);
                // placeables에서 제거
                placeables.RemoveAll(p => p.s == lcPick.s);
            }

            // 3) 나머지는 “현재 보드 기준” 역가중치(tiles^-a)로 뽑기 (가상 보드 시뮬 X)
            float a = Mathf.Clamp(ComputeAForGate(), aMin, aMax);
            System.Func<(ShapeData s, FitInfo fit), float> weight = p =>
            {
                int tiles = Mathf.Max(1, p.s.activeBlockCount);
                return 1f / Mathf.Pow(tiles, Mathf.Max(0.0001f, a));
            };

            while (wave.Count < count && placeables.Count > 0)
            {
                // 가중 랜덤
                float total = 0f; foreach (var c in placeables) total += weight(c);
                float r = Random.value * total;
                int idx = 0;
                for (; idx < placeables.Count; idx++)
                {
                    float w = weight(placeables[idx]);
                    if (r < w) break;
                    r -= w;
                }
                var pick = placeables[Mathf.Clamp(idx, 0, placeables.Count - 1)];
                wave.Add(pick.s); fits.Add(pick.fit); IncreaseDupes(pick.s.Id, null);
                placeables.RemoveAt(Mathf.Clamp(idx, 0, placeables.Count - 1));

                // 중복 한도 초과 ID 제거
                placeables.RemoveAll(p => IsDupesExceeded(p.s.Id));
            }

            // 4) 부족하면 실패로 보자(안전). 필요시 랜덤 채움 가능하나 즉시 배치 불가 수가 섞이면 다시 막힘.
            if (wave.Count == 0) return false;

            SetLastGeneratedFits(fits);
            return true;
        }

        /// <summary>역가중치(1/tiles^a)로, 현재 보드에 실제 배치 가능한 Shape 하나를 고른다.</summary>
        private bool TryPickInverseWeightedAmongPlaceableFromRandom(
            bool[,] board,
            HashSet<string> excludedByPenalty,
            HashSet<string> excludedByDupes,
            float a,
            out ShapeData chosen,
            out FitInfo chosenFit)
        {
            chosen = null; chosenFit = default;
            var candidates = new List<(ShapeData s, FitInfo fit, float w)>();

            foreach (var s in shapeData)
            {
                if (s == null) continue;
                if (excludedByPenalty != null && excludedByPenalty.Contains(s.Id)) continue;
                if (excludedByDupes != null && excludedByDupes.Contains(s.Id)) continue;

                if (!TryFindFitFromRandomStart(board, s, out var fit)) continue;

                int tiles = Mathf.Max(1, s.activeBlockCount);
                float w = 1f / Mathf.Pow(tiles, Mathf.Max(0.0001f, a));
                candidates.Add((s, fit, w));
            }
            if (candidates.Count == 0) return false;

            float total = 0f; foreach (var c in candidates) total += c.w;
            float r = Random.value * total;
            foreach (var c in candidates)
            {
                if (r < c.w) { chosen = c.s; chosenFit = c.fit; return true; }
                r -= c.w;
            }
            var last = candidates[^1];
            chosen = last.s; chosenFit = last.fit;
            return true;
        }

        /// <summary>가상 보드에 Fit 커버 셀을 점유 표시</summary>
        private void ApplyFitOnBoard(FitInfo fit, bool[,] board)
        {
            if (fit.CoveredSquares == null) return;
            foreach (var sq in fit.CoveredSquares)
            {
                int r = sq.RowIndex, c = sq.ColIndex;
                if (r >= 0 && c >= 0 && r < board.GetLength(0) && c < board.GetLength(1))
                    board[r, c] = true;
            }
        }

        /// <summary>가상 보드에서 가득 찬 행/열을 찾아 비움(라인 제거 시뮬레이션)</summary>
        private void RemoveFullLines(bool[,] board)
        {
            int rows = board.GetLength(0), cols = board.GetLength(1);

            // 행
            for (int r = 0; r < rows; r++)
            {
                bool full = true;
                for (int c = 0; c < cols; c++) if (!board[r, c]) { full = false; break; }
                if (full) for (int c = 0; c < cols; c++) board[r, c] = false;
            }
            // 열
            for (int c = 0; c < cols; c++)
            {
                bool full = true;
                for (int r = 0; r < rows; r++) if (!board[r, c]) { full = false; break; }
                if (full) for (int r = 0; r < rows; r++) board[r, c] = false;
            }
        }

        // --- per-wave 중복 한도 유틸 ---
        private readonly Dictionary<string, int> _dupeCounter = new Dictionary<string, int>();
        private void IncreaseDupes(string id, HashSet<string> excludedByDupes)
        {
            if (string.IsNullOrEmpty(id)) return;
            _dupeCounter.TryGetValue(id, out var cnt);
            cnt++;
            _dupeCounter[id] = cnt;
            if (cnt >= maxDuplicatesPerWave) excludedByDupes?.Add(id);
        }
        private bool IsDupesExceeded(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            _dupeCounter.TryGetValue(id, out var cnt);
            return cnt >= maxDuplicatesPerWave;
        }

        // =======================
        // 🔽 추가된 보조 메서드들
        // =======================

        private static bool IsValidFit(FitInfo fit)
        {
            if (fit.CoveredSquares == null) return false;
            foreach (var _ in fit.CoveredSquares) return true; // 하나라도 있으면 유효
            return false;
        }

        private static bool HasAnyValidFit(List<FitInfo> fits)
        {
            if (fits == null) return false;
            for (int i = 0; i < fits.Count; i++)
                if (IsValidFit(fits[i])) return true;
            return false;
        }

        /// <summary>
        /// Safety Wave: 현재 보드에서 **반드시 놓일 수 있는** 조합을 count 개 만든다.
        /// 작은 타일수 선호, 배치할 때마다 보드에 가상 적용하여 다음 선택 난이도 완화.
        /// </summary>
        private void BuildSafetyWaveAndFits(
            bool[,] board,
            int count,
            HashSet<string> excludedDupes,
            out List<ShapeData> wave,
            out List<FitInfo> fits)
        {
            wave = new List<ShapeData>(count);
            fits = new List<FitInfo>(count);

            // 타일 수 오름차순(작은 조각 우선)
            var byTilesAsc = shapeData
                .Where(s => s != null && (excludedDupes == null || !excludedDupes.Contains(s.Id)))
                .OrderBy(s => Mathf.Max(1, s.activeBlockCount))
                .ToList();

            int safetyTries = 0, MAX_TRIES = 128;

            while (wave.Count < count && safetyTries++ < MAX_TRIES)
            {
                ShapeData chosen = null; FitInfo fit = default;

                // 작은 조각부터 배치 가능한 것을 찾는다
                foreach (var s in byTilesAsc)
                {
                    if (TryFindFitFromRandomStart(board, s, out var f))
                    {
                        chosen = s; fit = f;
                        break;
                    }
                }

                if (chosen == null)
                {
                    // 정말 아무 것도 못 놓으면 중단
                    break;
                }

                wave.Add(chosen);
                fits.Add(fit);
                IncreaseDupes(chosen.Id, excludedDupes);

                // 가상 배치 → 라인 제거로 다음 선택 용이하게
                ApplyFitOnBoard(fit, board);
                RemoveFullLines(board);
            }

            // 부족하면 마지막으로 "가중치 랜덤"으로 채우되, 배치 실패 fit은 default로 둔다
            while (wave.Count < count)
            {
                var fallback = GetRandomShapeByWeightExcluding(null, excludedDupes);
                if (fallback == null) break;
                wave.Add(fallback);
                fits.Add(default);
                IncreaseDupes(fallback.Id, excludedDupes);
            }
        }

        /// <summary>
        /// wave와 fits 길이/유효성 보강. fit이 default거나 무효이면 현 시점 보드 기준으로 재탐색하여 채운다.
        /// </summary>
        private void EnsureFitsForWave(bool[,] startBoard, List<ShapeData> wave, List<FitInfo> fits)
        {
            if (wave == null) return;
            if (fits == null) fits = new List<FitInfo>(wave.Count);

            // 길이 보정
            while (fits.Count < wave.Count) fits.Add(default);

            // 보드 복제본에서 순차 적용
            var board = (bool[,])startBoard.Clone();

            for (int i = 0; i < wave.Count; i++)
            {
                var s = wave[i];
                var f = (i < fits.Count) ? fits[i] : default;

                if (s == null)
                {
                    fits[i] = default;
                    continue;
                }

                if (!IsValidFit(f))
                {
                    // 현 보드 기준으로 재탐색
                    if (!TryFindFitFromRandomStart(board, s, out f))
                    {
                        // 끝까지 못 찾으면 어쩔 수 없이 default 유지
                        fits[i] = default;
                        continue;
                    }
                }

                fits[i] = f;
                ApplyFitOnBoard(f, board);
                RemoveFullLines(board);
            }

            // 끝에서 한 번 더: 최소 하나는 유효해야 한다.
            if (!HasAnyValidFit(fits))
            {
                // 보드 초기화 후 1개는 반드시 배치 가능한 것으로 교체 시도
                var board2 = SnapshotBoard();
                if (TryPickInverseWeightedAmongPlaceableFromRandom(board2, null, null, 1.0f, out var s, out var fit))
                {
                    if (wave.Count > 0) { wave[0] = s; fits[0] = fit; }
                    else { wave.Add(s); fits.Add(fit); }
                }
            }
        }
    }
}
