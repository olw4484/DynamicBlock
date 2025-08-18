using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : BoardManager.cs
// Desc    : 격자/배치/클리어 담당
// ================================
public class BoardManager : MonoBehaviour, IManager
{
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;

    // 보드 점유 상태
    private bool[,] _occupied;

    public void PreInit() { }
    public void Init() { _occupied = new bool[_width, _height]; }
    public void PostInit() { }

    // 배치 가능 검증(PlacementValidator가 호출)
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

    // 실제 배치
    public void Place(Vector2Int[] localCells, Vector2Int origin)
    {
        foreach (var c in localCells)
            _occupied[origin.x + c.x, origin.y + c.y] = true;

        // 라인/패턴 클리어
        var cleared = ClearLines();
        if (cleared > 0)
        {
            EventBus.PublishBoardCleared();
        }
    }

    private int ClearLines()
    {
        int cleared = 0;
        // 가로줄/세로줄 모두 체크(블록블라스트는 변형 룰 가능-SO로 스위치)
        for (int y = 0; y < _height; y++)
        {
            bool full = true;
            for (int x = 0; x < _width; x++) if (!_occupied[x, y]) { full = false; break; }
            if (full) { cleared++; for (int x = 0; x < _width; x++) _occupied[x, y] = false; }
        }
        // 세로도 필요시 반복
        return cleared;
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;
}