using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        private bool TryPickWeightedAmongPlaceableFromRandom(
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
                float w = Mathf.Pow(Mathf.Max(1, s.activeBlockCount), a);
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
            chosen = last.s; chosenFit = last.fit; return true;
        }
        
        private void SetLastGeneratedFits(List<FitInfo> src)
        {
            _lastGeneratedFits.Clear();
            if (src == null) return;
            for (int i = 0; i < src.Count; i++) _lastGeneratedFits.Add(src[i]);
        }
        /// <summary>
        /// Revive 등에서 이미 계산된 fits를 그대로 사용해 프리뷰 표시.
        /// fits가 null/부족/충돌이면 안전하게 재탐색으로 폴백.
        /// </summary>
        public void PreviewWaveNonOverlapping(
            List<ShapeData> wave,
            IReadOnlyList<FitInfo> fitsOrNull,
            List<Sprite> spritesOrNull = null)
        {
            ClearPreview();

            var gm = GridManager.Instance;
            int rows = gm.rows, cols = gm.cols;
            var squares = gm.gridSquares;

            var virtualBoard = new bool[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    virtualBoard[r, c] = squares[r, c].IsOccupied;

            for (int i = 0; i < wave.Count; i++)
            {
                var shape = wave[i];
                if (shape == null) continue;

                FitInfo fit = default;
                bool useGivenFit = false;

                // 1) 전달된 fits 우선 사용
                if (fitsOrNull != null && i < fitsOrNull.Count && fitsOrNull[i].CoveredSquares != null)
                {
                    var f = fitsOrNull[i];
                    bool conflict = false;
                    foreach (var sq in f.CoveredSquares)
                    {
                        int rr = sq.RowIndex, cc = sq.ColIndex;
                        if (rr < 0 || cc < 0 || rr >= rows || cc >= cols || virtualBoard[rr, cc])
                        { conflict = true; break; }
                    }
                    if (!conflict) { fit = f; useGivenFit = true; }
                }

                // 2) 없거나 충돌이면 재탐색 폴백(랜덤 시작 권장)
                if (!useGivenFit)
                    if (!TryFindFitFromRandomStart(virtualBoard, shape, out fit))
                        continue;

                var sprite = (spritesOrNull != null && i < spritesOrNull.Count) ? spritesOrNull[i] : null;
                ApplyPreview(fit, sprite);

                if (fit.CoveredSquares != null)
                    foreach (var sq in fit.CoveredSquares)
                        virtualBoard[sq.RowIndex, sq.ColIndex] = true;
            }
        }

        /// 호환 : 기존 2파라미터 버전은 ‘저장된 Fit’이 있으면 그걸 쓰고, 없으면 재탐색
        public void PreviewWaveNonOverlapping(List<ShapeData> wave, List<Sprite> spritesOrNull)
        {
            if (_lastGeneratedFits != null && _lastGeneratedFits.Count == wave.Count)
            {
                PreviewWaveNonOverlapping(wave, _lastGeneratedFits, spritesOrNull); // ✅ 실제 선택 위치 사용
                return;
            }

            // 저장된 fit이 없으면 재탐색으로 폴백
            PreviewWaveNonOverlapping(wave, null, spritesOrNull);
        }
            
            /// <summary>
            /// 지정된 배치의 셀들을 Hover 상태로 표시(점유 갱신 X)
            /// </summary>
            private void ApplyPreview(FitInfo fit, Sprite spriteOrNull)
            {
                if (fit.CoveredSquares == null || fit.CoveredSquares.Count == 0)
                {
                    Debug.LogError("fit 설정이 안되어있다.");
                    return;
                }
                
                foreach (var sq in fit.CoveredSquares)
                {
                    if (sq == null) 
                        continue;
                    if (spriteOrNull != null) 
                        sq.SetImage(spriteOrNull);
                    if (!sq.IsOccupied) 
                        sq.SetState(GridState.Hover); // Hover 이미지를 켬
                }
            }

            /// <summary>
            /// 현재 보드에서 점유되지 않은 셀들의 Hover를 모두 해제
            /// </summary>
            public void ClearPreview()
            {
                var gm = GridManager.Instance;
                var squares = gm.gridSquares;

                for (int r = 0; r < gm.rows; r++)
                {
                    for (int c = 0; c < gm.cols; c++)
                    {
                        bool occupied = gm.gridStates[r, c];
                        squares[r, c].SetOccupied(occupied);
                    }
                }
            }

            private void RecomputeFitsForWave(List<ShapeData> wave)
            {
                _lastGeneratedFits.Clear();

                var gm = GridManager.Instance;
                int rows = gm.rows, cols = gm.cols;
                var squares = gm.gridSquares;

                var virtualBoard = new bool[rows, cols];
                for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    virtualBoard[r, c] = squares[r, c].IsOccupied;

                for (int i = 0; i < wave.Count; i++)
                {
                    var shape = wave[i];
                    if (shape == null) { _lastGeneratedFits.Add(default); continue; }

                    if (TryFindFitFromRandomStart(virtualBoard, shape, out var fit))
                    {
                        _lastGeneratedFits.Add(fit);
                        if (fit.CoveredSquares != null)
                            foreach (var sq in fit.CoveredSquares)
                                virtualBoard[sq.RowIndex, sq.ColIndex] = true;

                        // 라인 제거 시뮬까지 반영하려면, 여기서 가상 보드에 적용
                        ResolveCompletedLinesInPlace(virtualBoard);
                    }
                    else
                    {
                        _lastGeneratedFits.Add(default);
                    }
                }
            }
            
    }
}