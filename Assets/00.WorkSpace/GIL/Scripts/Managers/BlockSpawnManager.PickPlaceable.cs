using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        /// <summary>
        /// 현재 보드에서 (랜덤 시작 오프셋 스캔 순서로) shape가 들어갈 수 있는 첫 Fit을 찾음.
        /// </summary>
        private bool TryFindFitFromRandomStart(bool[,] board, ShapeData shape, out FitInfo fit)
        {
            TCount("TryFindFitFromRandomStart");
            int _probe = 0;
            int _hitAt = -1;

            var gm = GridManager.Instance;
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shapeRows = maxY - minY + 1;
            int shapeCols = maxX - minX + 1;

            FitInfo resultFit = default;
            var found = false;

            ForEachOffsetFromRandomStart(gm.rows, gm.cols, shapeRows, shapeCols, (oy, ox) =>
            {
                if (found) return;

                _probe++;
                TSampled("PickPlaceable.scan", 50, $"좌표 샘플=({oy},{ox})");

                // 배치 판정
                if (!CanPlaceAt(board, shape, minX, minY, shapeRows, shapeCols, oy, ox))
                {
                    TSampled("PickPlaceable.fail", 100, $"실패: origin=({oy},{ox})");
                    // 실패 이유 추적, 현재는 미 실행
                    //TryExplainPlacementFailure(board, shape, oy, ox);
                    return;
                }

                // 성공: Fit 구성
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
                _hitAt = _probe;
                TDo($"성공: origin=({oy},{ox}), 커버셀={covered.Count}, 첫성공시도={_hitAt}");
                found = true;
            });

            fit = resultFit;

            if (!found) TDo($"실패: 총 시도={_probe}, 첫 성공 인덱스={_hitAt}");
            return found;
        }

        // 같은 partial 내부 아무 곳(클래스 레벨)에 추가)
#if UNITY_EDITOR
        private void TryExplainPlacementFailure(bool[,] board, ShapeData s, int r, int c)
        {
            if (board == null || s == null) return;
            int rows = board.GetLength(0), cols = board.GetLength(1);
            if (r < 0 || c < 0 || r >= rows || c >= cols)
            { TDo2($"실패 사유: 시작 좌표 경계 밖 r={r}, c={c} (rows={rows}, cols={cols})"); return; }
        }
#endif

        /// <summary>
        /// (보드·오프셋 고정) 해당 위치에 shape를 올릴 수 있는지 검사
        /// </summary>
        private bool CanPlaceAt(
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
