using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PlacementValidator.cs
// Desc    : ��ġ ��Ģ ����(ȸ��/���/��ħ)
// ================================
public class PlacementValidator : MonoBehaviour, IManager
{
    [SerializeField] private BoardManager _board;

    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }

    public bool TryGetValidPlacement(Vector2Int[] pieceCells, Vector2Int gridPos, out Vector2Int origin)
    {
        // ������ gridPos�� ���� �ĺ��� ���
        origin = gridPos;

        // ȸ�� ��� ��, ȸ�� ��ȯ�� �� ���յ� �˻�
        if (_board.CanPlace(pieceCells, origin)) return true;

        // �ʿ��ϸ� �ֺ� Ÿ�� ������ Ž��(���� ���� ����)
        return false;
    }
}