using _00.WorkSpace.GIL.Scripts.Grids;
using UnityEditor;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.Editors
{
    [CustomEditor(typeof(GridGenerator))]
    public class GridEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GridGenerator gridGenerator = (GridGenerator)target;

            GUILayout.Space(10);
        
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Grid"))
            {
                gridGenerator.CreateGrid();
            }
            if (GUILayout.Button("Clear Grid"))
            {
                // 실수로 삭제하는 것을 방지
                if (EditorUtility.DisplayDialog("Clear Grid", "정말 모든 그리드를 삭제하시겠습니까?", "삭제", "취소"))
                    gridGenerator.ClearGrid();
            }
            GUILayout.EndHorizontal();
        }
    }
}

