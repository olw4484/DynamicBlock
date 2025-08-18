using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PlacementValidator.cs
// Desc    : 배치 규칙 검증(회전/경계/겹침)
// ================================
public class PlacementValidator : MonoBehaviour, IManager
{
    [SerializeField] private BoardManager _board;

    public void PreInit() { }
    public void Init() { }
    public void PostInit() { }

    public bool TryGetValidPlacement(Vector2Int[] pieceCells, Vector2Int gridPos, out Vector2Int origin)
    {
        // 스냅된 gridPos를 원점 후보로 사용
        origin = gridPos;

        // 회전 허용 시, 회전 변환한 셀 집합도 검사
        if (_board.CanPlace(pieceCells, origin)) return true;

        // 필요하면 주변 타일 오프셋 탐색(스냅 오차 보정)
        return false;
    }
}