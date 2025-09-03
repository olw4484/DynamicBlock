using _00.WorkSpace.GIL.Scripts.Shapes;
using _00.WorkSpace.GIL.Scripts.Grids;
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
        
        private static (int minX, int maxX, int minY, int maxY) GetShapeBounds(ShapeData shape)
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

            if (maxX < 0)
            {
                minX = minY = 0;
                maxX = maxY = 0;
            }

            return (minX, maxX, minY, maxY);
        }
        
        private static void ForEachOffsetFromRandomStart(int boardRows, int boardCols, int shapeRows, int shapeCols, System.Action<int,int> body)
        {
            int oyMax = boardRows - shapeRows;
            int oxMax = boardCols - shapeCols;
            if (oyMax < 0 || oxMax < 0) return;

            int startOy = UnityEngine.Random.Range(0, oyMax + 1);
            int startOx = UnityEngine.Random.Range(0, oxMax + 1);

            for (int dy = 0; dy <= oyMax; dy++)
            {
                int oy = startOy + dy;
                if (oy > oyMax) oy -= (oyMax + 1);

                for (int dx = 0; dx <= oxMax; dx++)
                {
                    int ox = startOx + dx;
                    if (ox > oxMax) ox -= (oxMax + 1);

                    body(oy, ox);
                }
            }
        }
    }
}