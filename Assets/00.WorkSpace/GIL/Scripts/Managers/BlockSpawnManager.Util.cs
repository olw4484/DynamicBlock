using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        public bool CanPlaceShapeData(ShapeData shape)
        {
            var gm = GridManager.Instance;
            var states = gm.gridStates;

            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shapeRows = maxY - minY + 1;
            int shapeCols = maxX - minX + 1;

            for (int yOff = 0; yOff <= gm.rows - shapeRows; yOff++)
            {
                for (int xOff = 0; xOff <= gm.cols - shapeCols; xOff++)
                {
                    if (CanPlace(shape, minX, minY, shapeRows, shapeCols, states, yOff, xOff))
                        return true;
                }
            }

            return false;
        }

        private bool CanPlace(ShapeData shape, int minX, int minY, int shapeRows, int shapeCols,
            bool[,] grid, int yOffset, int xOffset)
        {
            for (int y = 0; y < shapeRows; y++)
            for (int x = 0; x < shapeCols; x++)
            {
                if (!shape.rows[y + minY].columns[x + minX]) continue;
                if (grid[y + yOffset, x + xOffset]) return false;
            }

            return true;
        }
        
        private (int minX, int maxX, int minY, int maxY) GetShapeBounds(ShapeData shape)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            for (int y = 0; y < shape.rows.Length; y++)
            {
                for (int x = 0; x < shape.rows[y].columns.Length; x++)
                {
                    if (!shape.rows[y].columns[x]) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX >= 0) return (minX, maxX, minY, maxY);
            minX = minY = 0;
            maxX = maxY = 0;

            return (minX, maxX, minY, maxY);
        }
        
        private void ForEachOffsetFromRandomStart(int boardRows, int boardCols, int shapeRows, int shapeCols, System.Action<int,int> body)
        {
            int oyMax = boardRows - shapeRows;
            int oxMax = boardCols - shapeCols;
            if (oyMax < 0 || oxMax < 0) return;

            int startOy = Random.Range(0, oyMax + 1);
            int startOx = Random.Range(0, oxMax + 1);
            
            
            for (int dy = 0; dy <= oyMax; dy++)
            {
                int oy = startOy + dy;
                if (oy > oyMax) oy -= oyMax + 1;

                for (int dx = 0; dx <= oxMax; dx++)
                {
                    int ox = startOx + dx;
                    if (ox > oxMax) ox -= oxMax + 1;

                    body(oy, ox);
                }
            }
        }
        
        private void ReserveAndResolveLines(bool[,] board, ShapeData shape, FitInfo fit)
        {
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int rows = maxY - minY + 1, cols = maxX - minX + 1;
            int oy = fit.Offset.y, ox = fit.Offset.x;

            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                if (shape.rows[minY + y].columns[minX + x])
                    board[oy + y, ox + x] = true;

            ResolveCompletedLinesInPlace(board);
        }

        private void ResolveCompletedLinesInPlace(bool[,] board)
        {
            var gm = GridManager.Instance;
            int rows = gm.rows, cols = gm.cols;

            // 가득 찬 행/열 수집
            List<int> fullRows = null;
            for (int r = 0; r < rows; r++)
            {
                bool full = true;
                for (int c = 0; c < cols; c++)
                    if (!board[r, c]) { full = false; break; }
                if (full) (fullRows ??= new List<int>()).Add(r);
            }

            List<int> fullCols = null;
            for (int c = 0; c < cols; c++)
            {
                bool full = true;
                for (int r = 0; r < rows; r++)
                    if (!board[r, c]) { full = false; break; }
                if (full) (fullCols ??= new List<int>()).Add(c);
            }

            // 동시 제거
            if (fullRows != null)
                foreach (int r in fullRows)
                    for (int c = 0; c < cols; c++)
                        board[r, c] = false;

            if (fullCols == null) return;
            {
                foreach (int c in fullCols)
                    for (int r = 0; r < rows; r++)
                        board[r, c] = false;
            }
        }
    }
}