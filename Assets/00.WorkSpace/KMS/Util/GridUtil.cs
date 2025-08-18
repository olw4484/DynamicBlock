using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GridUtil.cs
// Desc    : 큐브의 회전 및 정규화
// ================================

public static class GridUtil
{
    public static Vector2Int[] Rotate(Vector2Int[] shape, int times90)
    {
        var rotated = new Vector2Int[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            var c = shape[i];
            switch (times90 % 4)
            {
                case 0: rotated[i] = new Vector2Int(c.x, c.y); break;
                case 1: rotated[i] = new Vector2Int(-c.y, c.x); break;
                case 2: rotated[i] = new Vector2Int(-c.x, -c.y); break;
                case 3: rotated[i] = new Vector2Int(c.y, -c.x); break;
            }
        }
        Normalize(rotated);
        return rotated;
    }

    public static void Normalize(Vector2Int[] cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
        }
        var offset = new Vector2Int(minX, minY);
        for (int i = 0; i < cells.Length; i++)
            cells[i] -= offset;
    }
}
