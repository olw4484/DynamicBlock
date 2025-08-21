using System;

[Serializable]
public class ShapeData
{
    public bool[] columns = new bool[5];

    public ShapeData()
    {
        for (int i = 0; i < columns.Length; i++)
            columns[i] = false;
    }
}