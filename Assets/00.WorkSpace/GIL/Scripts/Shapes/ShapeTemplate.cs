using UnityEngine;

[CreateAssetMenu(fileName = "Shape", menuName = "New Shape", order = 1)]
public class ShapeTemplate : ScriptableObject
{
    public string Id;
    public ShapeData[] rows = new ShapeData[5];

    [Header("Classic Mode")]
    public int scoreForSpawn = 1;
    public float chanceForSpawn = 1f;

    private void OnEnable()
    {
        // Null 체크 및 초기화
        if (rows == null || rows.Length != 5)
        {
            rows = new ShapeData[5];
        }

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null)
                rows[i] = new ShapeData();
        }
    }
}