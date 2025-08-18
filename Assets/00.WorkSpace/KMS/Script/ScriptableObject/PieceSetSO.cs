using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceSetSO.cs
// Desc    : ���� ���� ��� ���� (7-bag��)
// ================================
[CreateAssetMenu(menuName = "Game/PieceSet")]
public class PieceSetSO : ScriptableObject
{
    [System.Serializable]
    public class Shape
    {
        [Tooltip("������ �����ϴ� �� ��ǥ�� (0,0 ���� ��� ��ǥ)")]
        public Vector2Int[] cells;
    }

    [Header("���� ��� ����Ʈ")]
    public List<Shape> shapes = new();
}
