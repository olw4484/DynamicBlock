using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceManager.cs
// Desc    : ����/���� ����/����
// ================================
public class PieceManager : MonoBehaviour, IManager
{
    [SerializeField] private PieceSetSO _pieceSet;
    [SerializeField] private int _handSize = 3;

    private readonly List<Vector2Int[]> _hand = new();

    public void PreInit() { }
    public void Init() { RefillHand(); }
    public void PostInit() { }

    public IReadOnlyList<Vector2Int[]> CurrentHand => _hand;

    public void Consume(int index)
    {
        _hand.RemoveAt(index);
        if (_hand.Count == 0) RefillHand();
        EventBus.PublishHandRefilled();
    }

    private void RefillHand()
    {
        _hand.Clear();
        // ���� ����(����: Bag �ý������� ���� ����)
        for (int i = 0; i < _handSize; i++)
        {
            var shape = _pieceSet.shapes[Random.Range(0, _pieceSet.shapes.Count)];
            _hand.Add(shape);
        }
    }
}
