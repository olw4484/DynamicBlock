using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : InputManager.cs
// Desc    : 드래그 & 드롭 입력 처리
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/InputManager")]
public class InputManager : MonoBehaviour, IManager
{
    // =====================================
    // # Fields
    // =====================================
    [SerializeField] private Camera _cam;
    [SerializeField] private BoardManager _board;
    [SerializeField] private PlacementValidator _validator;
    [SerializeField] private PieceManager _pieceManager;
    [SerializeField] private SoundManager _soundManager;

    [Header("보드 레이캐스트")]
    [SerializeField] private LayerMask _boardMask;
    [SerializeField] private float _cellSize = 1f; // 스냅 단위

    private int _selectedIndex = -1;         // 현재 손패 인덱스
    private Vector2Int[] _selectedShape;     // 현재 드래그 중인 셀들(상대 좌표)
    private bool _dragging;

    // =====================================
    // # Lifecycle
    // =====================================
    public void PreInit() { if (_cam == null) _cam = Camera.main; }
    public void Init() { }
    public void PostInit() { }

    // =====================================
    // # Update Loop
    // =====================================
    private void Update()
    {
        if (TryBeginDrag())
            _dragging = true;

        if (_dragging)
        {
            Vector2Int gridPos;
            if (TryGetGridPos(out gridPos))
            {
                // 고스트 프리뷰는 여기서 UI/VFX로 처리 가능
                if (Input.GetMouseButtonUp(0) || TouchEnded())
                {
                    TryPlaceAt(gridPos);
                    _dragging = false;
                }
            }
            else if (Input.GetMouseButtonUp(0) || TouchEnded())
            {
                _dragging = false; // 놓을 수 없는 위치 => 취소
            }
        }
    }

    // =====================================
    // # Drag Begin
    // =====================================
    private bool TryBeginDrag()
    {
        if (!(Input.GetMouseButtonDown(0) || TouchBegan()))
            return false;

        var hand = _pieceManager.CurrentHand;
        if (hand.Count == 0)
            return false;

        _selectedIndex = 0;
        _selectedShape = hand[0];
        return true;
    }

    // =====================================
    // # Grid Raycast
    // =====================================
    private bool TryGetGridPos(out Vector2Int gridPos)
    {
        gridPos = default;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 100f, _boardMask))
        {
            // 히트 포인트를 보드 좌표로 스냅 (보드 원점/오프셋은 프로젝트에 맞게 보정)
            Vector3 p = hit.point;
            int gx = Mathf.RoundToInt(p.x / _cellSize);
            int gy = Mathf.RoundToInt(p.z / _cellSize);
            gridPos = new Vector2Int(gx, gy);
            return true;
        }
        return false;
    }

    // =====================================
    // # Place
    // =====================================
    private void TryPlaceAt(Vector2Int gridPos)
    {
        if (_selectedShape == null) return;

        if (_validator.TryGetValidPlacement(_selectedShape, gridPos, out var origin))
        {
            _board.Place(_selectedShape, origin);
            _pieceManager.Consume(_selectedIndex);
            EventBus.OnPiecePlaced?.Invoke();
        }

        _selectedShape = null;
        _selectedIndex = -1;
    }

    // =====================================
    // # Touch helpers
    // =====================================
    private bool TouchBegan()
    {
        if (Input.touchCount == 0) return false;
        return Input.touches[0].phase == TouchPhase.Began;
    }
    private bool TouchEnded()
    {
        if (Input.touchCount == 0) return false;
        return Input.touches[0].phase == TouchPhase.Ended || Input.touches[0].phase == TouchPhase.Canceled;
    }
}