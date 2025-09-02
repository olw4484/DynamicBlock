using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Managers
{
    public partial class BlockSpawnManager
    {
        /// <summary>
        /// 현 보드(실제 상태 기준)에서 shape를 놓을 수 있는 모든 위치 중
        /// '겹치지 않는' 하나를 찾아 반환 (좌상단 우선). 못 찾으면 false.
        /// </summary>
        public bool TryFindOneFit(ShapeData shape, bool[,] virtualBoard, out FitInfo fit)
        {
            fit = default;
            var gm = GridManager.Instance;
            var squares = gm.gridSquares;

            // 경계 상자
            var (minX, maxX, minY, maxY) = GetShapeBounds(shape);
            int shRows = maxY - minY + 1, shCols = maxX - minX + 1;

            for (int oy = 0; oy <= gm.rows - shRows; oy++)
            {
                for (int ox = 0; ox <= gm.cols - shCols; ox++)
                {
                    bool ok = true;
                    var list = new List<GridSquare>(8);

                    for (int y = 0; y < shRows && ok; y++)
                    for (int x = 0; x < shCols; x++)
                    {
                        if (!shape.rows[y + minY].columns[x + minX]) continue;

                        // 실제/가상 보드 중 가상 보드 우선(중복 방지)
                        bool occupied = virtualBoard != null
                            ? virtualBoard[oy + y, ox + x]
                            : squares[oy + y, ox + x].IsOccupied;

                        if (occupied) { ok = false; break; }
                        list.Add(squares[oy + y, ox + x]);
                    }

                    if (ok)
                    {
                        fit = new FitInfo { Offset = new Vector2Int(ox, oy), CoveredSquares = list };
                        return true;
                    }
                }
            }
            return false;
        }

        

        /// <summary>
        /// 웨이브 전체에 대해 겹치지 않는 위치를 계산하고 Hover로 표시.
        /// 같은 셀 중복 하이라이트를 막기 위해 가상보드에 순차 점유 마킹.
        /// </summary>
        public void PreviewWaveNonOverlapping(List<ShapeData> wave, List<Sprite> spritesOrNull)
        {
            ClearPreview();

            var gm = GridManager.Instance;
            var rows = gm.rows; 
            var cols = gm.cols;
            var squares = gm.gridSquares;

            // 가상 보드: 현재 점유 상태를 복사하고, 미리보기로 선점한 칸은 true로 마킹
            var virtualBoard = new bool[rows, cols];
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    virtualBoard[row, col] = squares[row, col].IsOccupied;

            for (int i = 0; i < wave.Count; i++)
            {
                if (wave[i] == null) continue;
                if (TryFindOneFit(wave[i], virtualBoard, out var fit))
                {
                    // 스프라이트는 선택 사항: null이면 그리드의 기본 hover이미지로 표시됨
                    var sprite = (spritesOrNull != null && i < spritesOrNull.Count) ? spritesOrNull[i] : null;
                    ApplyPreview(fit, sprite);

                    // 가상보드 점유 마킹(다음 블록 프리뷰가 겹치지 않게)
                    foreach (var sq in fit.CoveredSquares)
                        virtualBoard[sq.RowIndex, sq.ColIndex] = true;
                }
            }
        }

        /// <summary>
        /// 지정된 배치의 셀들을 Hover 상태로 표시(점유 갱신 X)
        /// </summary>
        private void ApplyPreview(FitInfo fit, Sprite spriteOrNull)
        {
            foreach (var sq in fit.CoveredSquares)
            {
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
                    if (!squares[r, c].IsOccupied)
                        squares[r, c].SetState(GridState.Normal);
                }
            }
        }
    }
}