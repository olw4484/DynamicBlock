using System;

[Serializable]
public class ShapeRow
{
    public bool[] cells = new bool[5];

    public ShapeRow()
    {
        for (int i = 0; i < cells.Length; i++)
            cells[i] = false;
    }
}