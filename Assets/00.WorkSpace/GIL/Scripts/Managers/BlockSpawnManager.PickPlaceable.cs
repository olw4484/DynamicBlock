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
            var gm = GridManager.Instance;
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shapeRows = maxY - minY + 1;
            int shapeCols = maxX - minX + 1;
            
            FitInfo resultFit = default;
            var found = false;
            
            // 랜덤 시작점에서 전체 탐색
            ForEachOffsetFromRandomStart(gm.rows, gm.cols, shapeRows, shapeCols, (oy, ox) =>
            {
                if (found) return; // 이미 찾았으면 중단

                if (!CanPlaceAt(board, shape, minX, minY, shapeRows, shapeCols, oy, ox)) return;
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
            });

            fit = resultFit;
            
            return found;
        }

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
