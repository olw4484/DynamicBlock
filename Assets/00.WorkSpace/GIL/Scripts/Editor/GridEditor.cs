using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Grid))]
public class GridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 원래 Inspector 먼저 그리기
        DrawDefaultInspector();

        Grid grid = (Grid)target;

        // 버튼 UI
        GUILayout.Space(10);
        
        // 버튼 2개 나란히 표시
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Grid"))
        {
            grid.CreateGrid();
        }
        if (GUILayout.Button("Clear Grid"))
        {
            // 실수로 삭제하는 것을 방지
            if (EditorUtility.DisplayDialog("Clear Grid", "정말 모든 그리드를 삭제하시겠습니까?", "삭제", "취소"))
                grid.ClearGrid();
        }
        GUILayout.EndHorizontal();
    }
}
