using System.Collections.Generic;
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
        /// 3) 남은 2개는 역가중치(tiles^-a)로, 실제 배치 가능한 후보 중에서 선택
        /// 4) 중복 한도 / 동일 구성 n연속 방지 후 이력 반영
        /// </summary>
        public bool TryGenerateReviveWave(
            int count,
            out List<ShapeData> wave,
            out List<FitInfo> fits)
        {
            if (count <= 0) count = 3;
            wave = new List<ShapeData>(count);
            fits = new List<FitInfo>(count);

            // 0) per-wave 중복 카운터 초기화
            _dupeCounter.Clear();

            // 1) 현재 보드 스냅샷
            var board = SnapshotBoard();

            // 1-a) 라인 보정 1회 시도 (균등 확률)
            var excludedPenalty = new HashSet<string>(); // Revive에서는 소형 게이트 미적용
            var excludedDupes   = new HashSet<string>();

            if (!TryApplyLineCorrectionOnce(board, excludedPenalty, excludedDupes, out var firstShape, out var firstFit))
                return false; // 보정 가능한 라인이 없으면 Revive 제공 불가

            wave.Add(firstShape);
            fits.Add(firstFit);
            IncreaseDupes(firstShape.Id, excludedDupes);

            // 2) 가상 보드에 1번을 "가상 배치" + 라인 제거 시뮬레이션
            ApplyFitOnBoard(firstFit, board);
            RemoveFullLines(board);

            // 3) 남은 (count-1)개를 역가중치로 선택 (소형 추가확률 게이트 사용 안 함)
            float a = Mathf.Clamp(ComputeAForGate(), aMin, aMax);
            for (int i = 1; i < count; i++)
            {
                if (!TryPickInverseWeightedAmongPlaceableFromRandom(
                        board, null, excludedDupes, a, out var s, out var fit))
                    break; // 더 이상 배치 가능한 후보가 없으면 종료

                wave.Add(s);
                fits.Add(fit);
                IncreaseDupes(s.Id, excludedDupes);

                ApplyFitOnBoard(fit, board);
                RemoveFullLines(board);
            }

            // 부족하면 마지막 보정(가중치 랜덤으로 채움; 배치 가능성 보장은 안 함)
            while (wave.Count < count)
            {
                var fallback = GetRandomShapeByWeightExcluding(null, excludedDupes);
                if (fallback == null) break;
                wave.Add(fallback);
                fits.Add(default);
                IncreaseDupes(fallback.Id, excludedDupes);
            }

            // 동일 구성 n연속 방지(기존 규칙과 동일)
            var waveKey = MakeWaveHistory(wave);
            if (WouldBecomeNStreak(waveKey, maxSameWaveStreak))
            {
                int idx = Random.Range(0, wave.Count);
                string oldId = wave[idx]?.Id;

                ShapeData candidate = null; int bestTiles = int.MinValue;
                foreach (var s in shapeData)
                {
                    if (s == null) continue;
                    if (s.Id == oldId) continue;
                    if (IsDupesExceeded(s.Id)) continue;
                    int tiles = s.activeBlockCount;
                    if (tiles > bestTiles) { bestTiles = tiles; candidate = s; }
                }

                if (candidate != null)
                {
                    wave[idx] = candidate;
                    fits[idx] = default;
                }
                waveKey = MakeWaveHistory(wave);
            }

            RegisterWaveHistory(waveKey);
            
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
                if (excludedByDupes   != null && excludedByDupes.Contains(s.Id)) continue;

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
    }
}