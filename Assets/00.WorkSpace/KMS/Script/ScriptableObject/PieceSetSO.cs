using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceSetSO.cs
// Desc    : 퍼즐 조각 모양 집합 (7-bag용)
// ================================
[CreateAssetMenu(menuName = "Game/PieceSet")]
public class PieceSetSO : ScriptableObject
{
    [System.Serializable]
    public class Shape
    {
        [Tooltip("조각을 구성하는 셀 좌표들 (0,0 기준 상대 좌표)")]
        public Vector2Int[] cells;
    }

    [Header("조각 모양 리스트")]
    public List<Shape> shapes = new();
}
