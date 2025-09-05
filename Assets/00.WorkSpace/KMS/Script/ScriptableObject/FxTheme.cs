using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SfxId
{
    RowClear = 1001,
    ColClear = 1002,
    BlockPlace = 1003,
    BlockSelect = 1004
}


[CreateAssetMenu(fileName = "FxTheme", menuName = "FX/FxTheme")]
public sealed class FxTheme : ScriptableObject
{
    [Header("Default Colors")]
    public Color rowClearColor = Color.cyan;
    public Color colClearColor = Color.magenta;

    [Header("SFX Id Mapping")]
    public int rowClearSfxId = (int)SfxId.RowClear;
    public int colClearSfxId = (int)SfxId.ColClear;
}
