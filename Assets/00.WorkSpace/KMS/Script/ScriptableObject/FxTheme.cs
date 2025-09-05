using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "FxTheme", menuName = "FX/FxTheme")]
public sealed class FxTheme : ScriptableObject
{
    [Header("Default Colors")]
    public Color rowClearColor = Color.cyan;
    public Color colClearColor = Color.magenta;

    [Header("SFX Id Mapping")]
    [SerializeField] public int rowClearSfxId = (int)SfxId.LineClear1;
    [SerializeField] public int colClearSfxId = (int)SfxId.LineClear1;
}
