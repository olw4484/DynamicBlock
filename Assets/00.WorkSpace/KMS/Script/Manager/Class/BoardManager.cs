using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : BoardManager.cs
// Desc    : ����/��ġ/Ŭ���� ���
// ================================
public class BoardManager : MonoBehaviour, IManager
{
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;

    // ���� ���� ����
    private bool[,] _occupied;

    public void PreInit() { }
    public void Init() { _occupied = new bool[_width, _height]; }
    public void PostInit() { }

    // ��ġ ���� ����(PlacementValidator�� ȣ��)
    public bool CanPlace(Vector2Int[] localCells, Vector2Int origin)
    {
        foreach (var c in localCells)
        {
            int x = origin.x + c.x;
            int y = origin.y + c.y;
            if (!InBounds(x, y) || _occupied[x, y]) return false;
        }
        return true;
    }

    // ���� ��ġ
    public void Place(Vector2Int[] localCells, Vector2Int origin)
    {
        foreach (var c in localCells)
            _occupied[origin.x + c.x, origin.y + c.y] = true;

        // ����/���� Ŭ����
        var cleared = ClearLines();
        if (cleared > 0)
        {
            EventBus.PublishBoardCleared();
        }
    }

    private int ClearLines()
    {
        int cleared = 0;
        // ������/������ ��� üũ(��Ϻ��Ʈ�� ���� �� ����-SO�� ����ġ)
        for (int y = 0; y < _height; y++)
        {
            bool full = true;
            for (int x = 0; x < _width; x++) if (!_occupied[x, y]) { full = false; break; }
            if (full) { cleared++; for (int x = 0; x < _width; x++) _occupied[x, y] = false; }
        }
        // ���ε� �ʿ�� �ݺ�
        return cleared;
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;
}