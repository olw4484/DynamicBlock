using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : InputManager.cs
// Desc    : �巡�� & ��� �Է� ó��
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

    [Header("���� ����ĳ��Ʈ")]
    [SerializeField] private LayerMask _boardMask;
    [SerializeField] private float _cellSize = 1f; // ���� ����

    private int _selectedIndex = -1;         // ���� ���� �ε���
    private Vector2Int[] _selectedShape;     // ���� �巡�� ���� ����(��� ��ǥ)
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
                // ��Ʈ ������� ���⼭ UI/VFX�� ó�� ����
                if (Input.GetMouseButtonUp(0) || TouchEnded())
                {
                    TryPlaceAt(gridPos);
                    _dragging = false;
                }
            }
            else if (Input.GetMouseButtonUp(0) || TouchEnded())
            {
                _dragging = false; // ���� �� ���� ��ġ => ���
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
            // ��Ʈ ����Ʈ�� ���� ��ǥ�� ���� (���� ����/�������� ������Ʈ�� �°� ����)
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