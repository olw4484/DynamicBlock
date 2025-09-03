using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        public enum LineAxis { Row, Col }

        [System.Serializable]
        public struct LineFirstCorrectionConfig
        {
            public bool UseLineFirstCorrection;
            public int MaxRunLength;           
            public bool ScanRows;              
            public bool ScanCols;              
            public bool AllowNonLineShapes;    
            public int PairBudget;
        }

        [Header("Line-First Correction")]
        [SerializeField] private LineFirstCorrectionConfig correctionCfg = new LineFirstCorrectionConfig
        {
            UseLineFirstCorrection = true,
            MaxRunLength = 5,
            ScanRows = true,
            ScanCols = true,
            AllowNonLineShapes = true,
            PairBudget = 200
        };

        public readonly struct LineRun
        {
            public LineAxis Axis { get; }
            public int Index { get; }   
            public int Start { get; }   
            public int Length { get; }

            public LineRun(LineAxis axis, int index, int start, int length)
            {
                Axis = axis; Index = index; Start = start; Length = length;
            }
        }

        public readonly struct CorrectionCandidate
        {
            public LineRun Run { get; }
            public ShapeData Shape { get; }
            public FitInfo Fit { get; }

            public CorrectionCandidate(LineRun run, ShapeData shape, FitInfo fit)
            {
                Run = run; Shape = shape; Fit = fit;
            }
        }
        
        private bool[,] SnapshotBoard()
        {
            var gm = GridManager.Instance;
            var rows = gm.rows;
            var cols = gm.cols;
            var squares = gm.gridSquares;
            var board = new bool[rows, cols];

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    board[r, c] = squares[r, c].IsOccupied;

            return board;
        }

        private List<LineRun> FindLineCandidates(bool[,] board, LineFirstCorrectionConfig cfg)
        {
            var gm = GridManager.Instance;
            int R = gm.rows, C = gm.cols;
            var runs = new List<LineRun>(16);

            void ScanLine(LineAxis axis, int idx)
            {
                int length = (axis == LineAxis.Row) ? C : R;
                int zeroStart = -1, zeroLen = 0, zeroSegments = 0;

                for (int i = 0; i < length; i++)
                {
                    bool occupied = (axis == LineAxis.Row) ? board[idx, i] : board[i, idx];
                    bool isZero = !occupied;

                    if (isZero)
                    {
                        if (zeroLen == 0) zeroStart = i;
                        zeroLen++;
                    }
                    if (!isZero || i == length - 1)
                    {
                        if (zeroLen > 0)
                        {
                            zeroSegments++;
                            // 조건: 연속 0 런이 정확히 1개이며, 길이 제한 필요
                            if (zeroSegments == 1 && zeroLen <= cfg.MaxRunLength)
                            {
                                // 나머지 칸이 모두 1인지 확인
                                bool othersAllOne = true;
                                for (int j = 0; j < length; j++)
                                {
                                    if (j >= zeroStart && j < zeroStart + zeroLen) continue;
                                    bool occ = (axis == LineAxis.Row) ? board[idx, j] : board[j, idx];
                                    if (!occ) { othersAllOne = false; break; }
                                }
                                if (othersAllOne)
                                    runs.Add(new LineRun(axis, idx, zeroStart, zeroLen));
                            }
                            // 다음 런 탐색 초기화
                            zeroStart = -1; zeroLen = 0;
                        }
                    }
                }
            }

            if (cfg.ScanRows)
                for (int r = 0; r < R; r++) ScanLine(LineAxis.Row, r);
            if (cfg.ScanCols)
                for (int c = 0; c < C; c++) ScanLine(LineAxis.Col, c);

            return runs;
        }

        private IEnumerable<ShapeData> EnumerateShapesForRun(LineRun run, IReadOnlyList<ShapeData> allShapes, LineFirstCorrectionConfig cfg)
        {
            int L = run.Length;

            for (int i = 0; i < allShapes.Count; i++)
            {
                var s = allShapes[i];
                if (s == null) continue;
                if (s.activeBlockCount < L) continue;
                yield return s;
            }
        }

        private bool TryFitInRun(LineRun run, ShapeData shape, bool[,] virtualBoard, out FitInfo fit, LineFirstCorrectionConfig cfg)
        {
            var gm = GridManager.Instance;
            var squares = gm.gridSquares;

            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shRows = maxY - minY + 1;
            int shCols = maxX - minX + 1;

            int targetIndex = run.Index;
            int runStart = run.Start;
            int runEnd = run.Start + run.Length - 1;

            FitInfo found = default;
            bool okFound = false;
            List<GridSquare> tmp = new List<GridSquare>(shape.activeBlockCount);

            ForEachOffsetFromRandomStart(gm.rows, gm.cols, shRows, shCols, (oy, ox) =>
            {
                if (okFound) return;

                tmp.Clear();
                bool ok = true;

                int coveredCountOnLine = 0;
                int lineSpanMin = int.MaxValue, lineSpanMax = int.MinValue;

                for (int sy = 0; sy < shRows && ok; sy++)
                {
                    for (int sx = 0; sx < shCols; sx++)
                    {
                        if (!shape.rows[sy + minY].columns[sx + minX]) continue;

                        int br = oy + sy; 
                        int bc = ox + sx; 

                        if (br < 0 || bc < 0 || br >= gm.rows || bc >= gm.cols) { ok = false; break; }

                        if (run.Axis == LineAxis.Row)
                        {
                            if (br == targetIndex)
                            {
                                // 라인 '위'의 셀 -> 구간 정확 커버 검증
                                if (bc < runStart || bc > runEnd) { ok = false; break; }
                                coveredCountOnLine++;
                                if (bc < lineSpanMin) lineSpanMin = bc;
                                if (bc > lineSpanMax) lineSpanMax = bc;
                            }
                            else
                            {
                                // 라인 밖으로 나간 셀은 전부 빈 칸이어야 함
                                if (virtualBoard[br, bc]) { ok = false; break; }
                            }
                        }
                        else
                        {
                            if (bc == targetIndex)
                            {
                                if (br < runStart || br > runEnd) { ok = false; break; }
                                coveredCountOnLine++;
                                if (br < lineSpanMin) lineSpanMin = br;
                                if (br > lineSpanMax) lineSpanMax = br;
                            }
                            else
                            {
                                if (virtualBoard[br, bc]) { ok = false; break; }
                            }
                        }

                        tmp.Add(squares[br, bc]);
                    }
                }

                if (!ok) return;

                if (coveredCountOnLine != run.Length) return;
                if (lineSpanMin != runStart || lineSpanMax != runEnd) return;

                found = new FitInfo
                {
                    Offset = new Vector2Int(ox, oy),
                    CoveredSquares = new List<GridSquare>(tmp)
                };
                okFound = true;
            });

            fit = found;
            return okFound;
        }

        private bool TrySelectLineCorrection(
            List<LineRun> runs,
            IReadOnlyList<ShapeData> shapes,
            bool[,] virtualBoard,
            HashSet<string> excludedByPenalty,
            HashSet<string> excludedByDupes,
            LineFirstCorrectionConfig cfg,
            out CorrectionCandidate chosen)
        {
            chosen = default;

            if (runs == null || runs.Count == 0) return false;

            
            var pairs = new List<CorrectionCandidate>(32);

            foreach (var run in runs)
            {
                foreach (var shape in EnumerateShapesForRun(run, shapes, cfg))
                {
                    if (shape == null) continue;
                    if (excludedByPenalty != null && excludedByPenalty.Contains(shape.Id)) continue;
                    if (excludedByDupes   != null && excludedByDupes.Contains(shape.Id)) continue;

                    if (TryFitInRun(run, shape, virtualBoard, out var fit, cfg))
                    {
                        pairs.Add(new CorrectionCandidate(run, shape, fit));
                        if (pairs.Count >= cfg.PairBudget) break;
                    }
                }
                if (pairs.Count >= cfg.PairBudget) break;
            }

            if (pairs.Count == 0) return false;

            int pick = Random.Range(0, pairs.Count);
            chosen = pairs[pick];
            return true;
        }

        private bool TryApplyLineCorrectionOnce(
            bool[,] virtualBoard,
            HashSet<string> excludedByPenalty,
            HashSet<string> excludedByDupes,
            out ShapeData correctedShape,
            out FitInfo correctedFit)
        {
            correctedShape = null;
            correctedFit = default;

            if (!correctionCfg.UseLineFirstCorrection) return false;

            var runs = FindLineCandidates(virtualBoard, correctionCfg);
            if (runs == null || runs.Count == 0) return false;

            bool ok = TrySelectLineCorrection(
                runs,
                shapeData,
                virtualBoard,
                excludedByPenalty,
                excludedByDupes,
                correctionCfg,
                out var chosen);

            if (!ok) return false;

            correctedShape = chosen.Shape;
            correctedFit = chosen.Fit;
            return true;
        }

        private bool TryGuaranteePlaceableWave(
            List<ShapeData> wave,
            out int replacedIndex,
            out ShapeData newShape,
            out FitInfo newFit)
        {
            replacedIndex = -1;
            newShape = null;
            newFit = default;

            if (wave == null || wave.Count == 0) return false;

            bool anyPlaceable = false;
            for (int i = 0; i < wave.Count; i++)
            {
                if (wave[i] != null && CanPlaceShapeData(wave[i]))
                {
                    anyPlaceable = true; break;
                }
            }
            if (anyPlaceable) return false;

            var board = SnapshotBoard();

            var excludedPenalty = new HashSet<string>();
            var excludedDupes   = new HashSet<string>();
            if (!TryApplyLineCorrectionOnce(board, excludedPenalty, excludedDupes, out var shape, out var fit)) 
                return false;

            replacedIndex = 0;
            newShape = shape;
            newFit = fit;
            return true;
        }
    }
}