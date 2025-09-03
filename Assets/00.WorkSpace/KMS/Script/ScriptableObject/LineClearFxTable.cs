using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LineClearFxTable", menuName = "FX/LineClearFxTable")]
public sealed class LineClearFxTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Range(1, 6)] public int lines;
        public Color rowColor = Color.cyan;
        public Color colColor = Color.magenta;
        public int rowSfxId = (int)SfxId.RowClear;
        public int colSfxId = (int)SfxId.ColClear;
    }

    public Entry[] entries;

    public Entry ForLines(int n)
    {
        foreach (var e in entries) if (e.lines == n) return e;
        return entries != null && entries.Length > 0 ? entries[0] : null;
    }
}