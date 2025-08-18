using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceSetSO.cs
// Desc    : ���� ���
// ================================
[CreateAssetMenu(menuName = "Game/PieceSet")]
public class PieceSetSO : ScriptableObject
{
    public List<Vector2Int[]> shapes; // �� ������ �� ��ǥ ����
}
