using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        private struct PlaceableCandidate
        {
            public ShapeData Shape;
            public FitInfo Fit;   // 기존 프로젝트에 정의된 FitInfo 사용 (Offset, CoveredSquares 등)
            public float Weight;
        }

        /// <summary>
        /// [엄밀화 3단계]
        /// 현재 보드 스냅샷 기준으로, (랜덤 시작 오프셋 스캔 순서로) 실제로 놓일 수 있는 셋만 수집하고
        /// tile^a 가중치로 1개를 선택함.
        /// </summary>
        private bool TryPickWeightedAmongPlaceablesFromRandomStart(
            HashSet<string> excludedByPenalty,
            HashSet<string> excludedByDupes,
            float a,
            out ShapeData chosen,
            out FitInfo chosenFit)
        {
            chosen = null;
            chosenFit = default;

            var board = SnapshotBoard();    // 현재 보드 점유 상태
            var gm = GridManager.Instance;

            var candidates = new List<PlaceableCandidate>(16);

            // 모든 Shape 중 제외 목록 제거 → 실제 배치 가능하면 후보에 추가
            for (int i = 0; i < shapeData.Count; i++)
            {
                var s = shapeData[i];
                if (s == null) continue;
                if (excludedByPenalty != null && excludedByPenalty.Contains(s.Id)) continue;
                if (excludedByDupes   != null && excludedByDupes.Contains(s.Id)) continue;

                if (TryFindFitFromRandomStart(board, s, out var fit))
                {
                    float w = Mathf.Pow(Mathf.Max(1, s.activeBlockCount), a);
                    candidates.Add(new PlaceableCandidate { Shape = s, Fit = fit, Weight = w });
                }
            }

            if (candidates.Count == 0) return false;

            // 가중치 랜덤 선택
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++) total += candidates[i].Weight;
            float r = Random.value * total;

            for (int i = 0; i < candidates.Count; i++)
            {
                var cnd = candidates[i];
                if (r < cnd.Weight)
                {
                    chosen = cnd.Shape;
                    chosenFit = cnd.Fit;
                    return true;
                }
                r -= cnd.Weight;
            }

            // 폴백(이론상 도달 X)
            var last = candidates[candidates.Count - 1];
            chosen = last.Shape;
            chosenFit = last.Fit;
            return true;
        }

        /// <summary>
        /// 현재 보드에서 (랜덤 시작 오프셋 스캔 순서로) shape가 들어갈 수 있는 첫 Fit을 찾음.
        /// </summary>
        private bool TryFindFitFromRandomStart(bool[,] board, ShapeData shape, out FitInfo fit)
        {
            var gm = GridManager.Instance;
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shapeRows = maxY - minY + 1;
            int shapeCols = maxX - minX + 1;
            
            FitInfo resultFit = default;
            bool found = false;
            
            // 랜덤 시작점에서 전체 탐색
            ForEachOffsetFromRandomStart(gm.rows, gm.cols, shapeRows, shapeCols, (oy, ox) =>
            {
                if (found) return; // 이미 찾았으면 중단

                if (CanPlaceAt(board, shape, minX, minY, shapeRows, shapeCols, oy, ox))
                {
                    // FitInfo 구성
                    var squares = gm.gridSquares;
                    var covered = new List<GridSquare>(shape.activeBlockCount);
                    for (int y = 0; y < shapeRows; y++)
                    for (int x = 0; x < shapeCols; x++)
                    {
                        if (!shape.rows[minY + y].columns[minX + x]) continue;
                        covered.Add(squares[oy + y, ox + x]);
                    }
                    resultFit = new FitInfo
                    {
                        Offset = new Vector2Int(ox, oy),
                        CoveredSquares = covered
                    };
                    found = true;
                }
            });

            fit = resultFit;
            
            return found;
        }

        /// <summary>
        /// (보드·오프셋 고정) 해당 위치에 shape를 올릴 수 있는지 검사
        /// </summary>
        private static bool CanPlaceAt(
            bool[,] board, ShapeData shape,
            int minX, int minY, int shapeRows, int shapeCols,
            int yOffset, int xOffset)
        {
            for (int y = 0; y < shapeRows; y++)
            for (int x = 0; x < shapeCols; x++)
            {
                if (!shape.rows[minY + y].columns[minX + x]) continue;
                if (board[yOffset + y, xOffset + x]) return false;
            }
            return true;
        }
    }
}
