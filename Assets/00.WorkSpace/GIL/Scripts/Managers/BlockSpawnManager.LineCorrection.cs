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
            public bool useLineFirstCorrection;
            public int maxRunLength;           
            public bool scanRows;              
            public bool scanCols;              
            public bool allowNonLineShapes;    
            public int pairBudget;
        }

        [Header("Line-First Correction")]
        [SerializeField] private LineFirstCorrectionConfig correctionCfg = new ()
        {
            useLineFirstCorrection = true,
            maxRunLength = 5,
            scanRows = true,
            scanCols = true,
            allowNonLineShapes = true,
            pairBudget = 200
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
            TDo2("보드 스냅샷 생성");
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
            TDo2("라인 후보 스캔: 연속 런 탐색");
            var gm = GridManager.Instance;
            int row = gm.rows, col = gm.cols;
            var runs = new List<LineRun>(16);

            if (cfg.scanRows)
                for (int r = 0; r < row; r++) ScanLine(LineAxis.Row, r);
            if (!cfg.scanCols) return runs;
            for (int c = 0; c < col; c++) ScanLine(LineAxis.Col, c);

            TDo2($"후보 라인 수={runs.Count}");
            return runs;

            void ScanLine(LineAxis axis, int index)
            {
                int length = axis == LineAxis.Row ? col : row;
                int zeroStart = -1, zeroLen = 0, zeroSegments = 0;

                for (int i = 0; i < length; i++)
                {
                    bool occupied = axis == LineAxis.Row ? board[index, i] : board[i, index];
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
                            if (zeroSegments == 1 && zeroLen <= cfg.maxRunLength)
                            {
                                // 나머지 칸이 모두 1인지 확인
                                bool othersAllOne = true;
                                for (int j = 0; j < length; j++)
                                {
                                    if (j >= zeroStart && j < zeroStart + zeroLen) continue;
                                    bool occ = axis == LineAxis.Row ? board[index, j] : board[j, index];
                                    if (occ) continue;
                                    othersAllOne = false; break;
                                }
                                if (othersAllOne)
                                {
                                    runs.Add(new LineRun(axis, index, zeroStart, zeroLen));
                                    TSampled("LineCorr.run", 20, $"run: axis={axis}, index={index}, start={zeroStart}, len={zeroLen}");
                                }
                            }
                            // 다음 런 탐색 초기화
                            zeroStart = -1; zeroLen = 0;
                        }
                    }
                }
            }
        }

        private IEnumerable<ShapeData> EnumerateShapesForRun(LineRun run, IReadOnlyList<ShapeData> allShapes)
        {
            TDo2($"런 길이={run.Length} → 후보 도형 열거");
            int runLength = run.Length;

            foreach (var allShape in allShapes)
            {
                if (allShape == null) continue;
                if (allShape.activeBlockCount < runLength) continue;
                TSampled("LineCorr.shapeEnum", 50, $"열거: shape={allShape.Id}, tiles={allShape.activeBlockCount}");
                yield return allShape;
            }
        }

        private bool TryFitInRun(LineRun run, ShapeData shape, bool[,] virtualBoard, out FitInfo fit)
        {
            //TDo($"런 적합성 검사 시작: axis={run.Axis}, index={run.Index}, start={run.Start}, len={run.Length}, shape={shape?.Id}");
            TDo("b.iv.3. 해당 열과 인접한 0을 탐색 / 기획서");
            var gm = GridManager.Instance;
            var squares = gm.gridSquares;

            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shRows = maxY - minY + 1;
            int shCols = maxX - minX + 1;

            int targetIndex = run.Index;
            int runStart = run.Start;
            int runEnd = run.Start + run.Length - 1;

            FitInfo found = default;
            var okFound = false;
            var tmp = new List<GridSquare>(shape.activeBlockCount);

            ForEachOffsetFromRandomStart(gm.rows, gm.cols, shRows, shCols, (oy, ox) =>
            {
                if (okFound) return;

                TSampled("LineCorr.offset", 64, $"offset=({oy},{ox})");

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
                TDo($"런 적합성 성공: origin=({oy},{ox}), cover={tmp.Count}");
            });

            fit = found;
            if (!okFound) TDo2("런 적합성 실패");
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

            TDo("라인 보정 후보 페어링 시작");
            TDo2($"runs={runs.Count}, budget={cfg.pairBudget}");

            var pairs = new List<CorrectionCandidate>(32);

            TDo("b.iv.4. 열 제거가 가능한 배치 가능한 블록의 목록과 배치 시 제거 되는 1의 개수를 계산 / 기획서");

            foreach (var run in runs)
            {
                foreach (var shape in EnumerateShapesForRun(run, shapes))
                {
                    if (shape == null) continue;
                    if (excludedByPenalty != null && excludedByPenalty.Contains(shape.Id)) continue;
                    if (excludedByDupes != null && excludedByDupes.Contains(shape.Id)) continue;

                    if (!TryFitInRun(run, shape, virtualBoard, out var fit)) continue;
                    int removableOnes = run.Length; // (간단 대치치; 더 정확한 산식 있으면 교체)
                    TDo($"ㄴ {shape.Id} 발견, 제거 가능한 활성화된 타일 수 {removableOnes} 개");
                    pairs.Add(new CorrectionCandidate(run, shape, fit));
                    TSampled("LineCorr.pair", 20, $"pair: run=({run.Axis},{run.Index},{run.Start},{run.Length}) shape={shape.Id} fit=({fit.Offset.y},{fit.Offset.x})");
                    if (pairs.Count >= cfg.pairBudget) break;
                }
                if (pairs.Count >= cfg.pairBudget) break;
            }

            if (pairs.Count == 0) return false;

            int pick = Random.Range(0, pairs.Count);
            chosen = pairs[pick];
            //TDo($"라인 보정 랜덤 선택: pick={pick}/{pairs.Count}, shape={chosen.Shape.Id}, origin=({chosen.Fit.Offset.y},{chosen.Fit.Offset.x})");
            TDo($"b.iv.5. 제거 가능 블록 계산…"); // 제목 1회
            TDo($"ㄴ 전환되는 수가 가장 높은 {chosen.Shape.Id} 선택");
            return true;
        }

        private bool TryApplyLineCorrectionOnce(
            bool[,] virtualBoard,
            HashSet<string> excludedByPenalty,
            HashSet<string> excludedByDupes,
            out ShapeData correctedShape,
            out FitInfo correctedFit)
        {
            TDo("b.iv.2. x열 or y열을 기준으로 한 열에 5개 이하의 연속된 0이 있고 나머지는 모두 1인 열을 탐색 / 기획서");

            correctedShape = null;
            correctedFit = default;

            if (!correctionCfg.useLineFirstCorrection) return false;

            var runs = FindLineCandidates(virtualBoard, correctionCfg);
            if (runs == null || runs.Count == 0)
            {
                TDo("라인 보정 불가: 후보 라인 없음");
                return false;
            }

            int candLines = runs?.Count ?? 0;
            TDo($"ㄴ {candLines} 개 라인 탐색");

            bool ok = TrySelectLineCorrection(
                runs,
                shapeData,
                virtualBoard,
                excludedByPenalty,
                excludedByDupes,
                correctionCfg,
                out var chosen);

            if (!ok)
            {
                TDo("b.iv.5. 제거 가능 블록 계산… 실패(페어 없음)");
                return false;
            }

            correctedShape = chosen.Shape;
            correctedFit = chosen.Fit;
            TDo($"라인 보정 채택 완료: shape={correctedShape.Id}, origin=({correctedFit.Offset.y},{correctedFit.Offset.x})");
            TDo($"b.iv.6. 계산된 소환 블록 위치 정보 저장 후 해당 열을 0으로 취급 / 기획서");
            TDo($"   · row={correctedFit.Offset.y}, col={correctedFit.Offset.x} 저장");
            return true;
        }

        private bool TryGuaranteePlaceableWave(List<ShapeData> wave, out int replacedIndex, out ShapeData newShape, out FitInfo newFit)
        {
            replacedIndex = -1;
            newShape = null;
            newFit = default;

            if (wave == null || wave.Count == 0) return false;

            bool anyPlaceable = false;
            foreach (var t in wave)
            {
                if (t == null || !CanPlaceShapeData(t)) continue;
                anyPlaceable = true; break;
            }
            if (anyPlaceable)
            {
                TDo2("보장 스킵: 웨이브 내 이미 배치 가능한 블록 존재");
                return false;
            }

            var board = SnapshotBoard();

            var excludedPenalty = new HashSet<string>();
            var excludedDupes   = new HashSet<string>();
            if (!TryApplyLineCorrectionOnce(board, excludedPenalty, excludedDupes, out var shape, out var fit))
            {
                TDo("보장 실패: 라인 보정으로 대체 불가");
                return false;
            }
                

            replacedIndex = 0;
            newShape = shape;
            newFit = fit;
            TDo($"보장 성공: index={replacedIndex} 교체 → shape={newShape.Id}, origin=({newFit.Offset.y},{newFit.Offset.x})");
            return true;
        }
    }
}