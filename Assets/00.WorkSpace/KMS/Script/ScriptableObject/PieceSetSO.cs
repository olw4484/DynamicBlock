using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceSetSO.cs
// Desc    : 조각 모양
// ================================
[CreateAssetMenu(menuName = "Game/PieceSet")]
public class PieceSetSO : ScriptableObject
{
    public List<Vector2Int[]> shapes; // 각 조각의 셀 좌표 집합
}
