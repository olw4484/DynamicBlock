using UnityEngine;

[CreateAssetMenu(fileName = "Shape", menuName = "BlockPuzzleGameToolkit/Items/Shape", order = 1)]
public class ShapeTemplate : ScriptableObject
{
    public ShapeRow[] rows = new ShapeRow[5];

    [Header("Classic Mode")]
    public int scoreForSpawn = 1;
    public float chanceForSpawn = 1f;

    [Header("Adventure Mode")]
    public int spawnFromLevel = 1;

    private void OnEnable()
    {
        // Null 체크 및 초기화
        if (rows == null || rows.Length != 5)
        {
            rows = new ShapeRow[5];
        }

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null)
                rows[i] = new ShapeRow();
        }
    }
}