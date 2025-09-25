using System;
using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.GameEvents;
using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Grids
{
    public class GridGenerator : MonoBehaviour
    {
        [Header("Grid Settings")] [SerializeField]
        private GameObject gridSquare;

        [SerializeField] private int columns = 8;
        [SerializeField] private int rows = 8;

        [Header("Grid Layout")] [SerializeField]
        private Vector2 startPosition = Vector2.zero;

        [SerializeField] private Vector2 spacing = new Vector2(5f, 5f);

        public List<GridSquare> gridSquares = new();
        
        
        private void Start()
        {
            if (gridSquare == null)
                CreateGrid();
            
            if (GridManager.Instance != null)
                GridManager.Instance.InitializeGridSquares(gridSquares, rows, columns);
            
            // TODO : 이 구문을 튜토리얼 시작 위치로 옮기기
            // 기획서상 게임 실행 -> 튜토리얼로 갈 예정이라 여기로 유도하면 됨.
            // TODO : 이 구문을 클래식 모드 시작할 위치에 옮기기
            // 리셋, 메인화면 갔다오기 이런걸 할 경우에도 상관 없이
            // 이동 완료, PanelSwitchOnClick.cs 42줄, RestartOnClick.cs 31줄에 추가
        }
        
        public void CreateGrid()
        {
            ClearGrid();
            //if (gridSquare == null) return;

            RectTransform squareRect = gridSquare.GetComponent<RectTransform>();
            Vector2 squareSize = squareRect.sizeDelta;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    float posX = startPosition.x + col * (squareSize.x + spacing.x);
                    float posY = startPosition.y - row * (squareSize.y + spacing.y);

                    GameObject newSquare = Instantiate(gridSquare, transform);
                    newSquare.name = $"Square_{row}_{col}";
                    newSquare.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, posY);
                    
                    GridSquare gs = newSquare.GetComponent<GridSquare>();
                    gs.RowIndex = row;
                    gs.ColIndex = col;
                    
                    gridSquares.Add(gs);
                }
            }
        }

        public void ClearGrid()
        {
            for (int i = gridSquares.Count - 1; i >= 0; i--)
            {
                if (gridSquares[i] != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                         DestroyImmediate(gridSquares[i].gameObject);
                    else
                        Destroy(gridSquares[i].gameObject);
#else
            Destroy(gridSquares[i].gameObject);
#endif
                }
            }

            gridSquares.Clear();
        }
    }
}
    

