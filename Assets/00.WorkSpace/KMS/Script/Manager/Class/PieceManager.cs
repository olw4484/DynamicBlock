using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceManager.cs
// Desc    : ����/���� ����/���� (7-bag ����)
// ================================
public class PieceManager : MonoBehaviour, IManager
{
    [SerializeField] private PieceSetSO _pieceSet;

    [Header("���� ����")]
    [SerializeField, Min(1)] private int _handSize = 3;

    [Header("Bag ����")]
    [Tooltip("�� ���� ũ��(��Ʈ������ 7-bag). shapes ���� �� ������ ������ shapes ���� ���.")]
    [SerializeField, Min(1)] private int _bagGroupSize = 7;

    // ���д� Vector2Int[]�� ���� (��ġ�� �ٷ� ���)
    private readonly List<Vector2Int[]> _hand = new();

    // 7-bag �ε��� ť (���� �������� ���� �� ���� �ε�����)
    private readonly List<int> _bag = new();
    private int _bagCursor = 0; // ���� bag���� �� ��° �ε����� �Һ� ������

    // =====================================
    // IManager
    // =====================================
    public void PreInit() { }

    public void Init()
    {
        if (!ValidatePieceSet()) return;

        RefillBagIfNeeded(force: true); // ���� �� ���� ä���
        RefillHand();
        EventBus.OnHandRefilled?.Invoke();
    }

    public void PostInit() { }

    public IReadOnlyList<Vector2Int[]> CurrentHand => _hand;

    // =====================================
    // �ܺ� API
    // =====================================
    public void Consume(int index)
    {
        if (index < 0 || index >= _hand.Count)
        {
            Debug.LogWarning($"[PieceManager] Consume index out of range: {index}");
            return;
        }

        _hand.RemoveAt(index);

        if (_hand.Count == 0)
        {
            if (!ValidatePieceSet()) return;
            RefillHand(); // ��� ���� �� �� ����
            EventBus.OnHandRefilled?.Invoke();
        }
    }

    // =====================================
    // ���� ����
    // =====================================
    private void RefillHand()
    {
        _hand.Clear();
        int tries = 0;                       
        int maxTries = _handSize * 10;

        for (int i = 0; i < _handSize; i++)
        {
            if (tries++ > maxTries)
            {
                Debug.LogError("[PieceManager] RefillHand ����: ��ȿ�� ������ ����� ���� ���߽��ϴ�.");
                break;
            }

            var cells = DrawNextShapeCells();
            if (cells == null || cells.Length == 0) { i--; continue; }

            _hand.Add(cells);
        }
    }

    /// <summary>
    /// 7-bag���� ���� ���� �ϳ��� �̾� cells ��ȯ
    /// </summary>
    private Vector2Int[] DrawNextShapeCells()
    {
        RefillBagIfNeeded();

        int shapeIndex = _bag[_bagCursor++];
        var shape = _pieceSet.shapes[shapeIndex];

        if (shape == null || shape.cells == null || shape.cells.Length == 0)
            return null;

        int rot = Random.Range(0, 4); // 0~3 ����
        return GridUtil.Rotate(shape.cells, rot);
    }

    /// <summary>
    /// bag�� ��� �Һ�Ǿ��ų� ��������� �� ������ �����ϰ� ����
    /// </summary>
    private void RefillBagIfNeeded(bool force = false)
    {
        if (!force && _bagCursor < _bag.Count) return;

        _bag.Clear();
        _bagCursor = 0;

        int total = _pieceSet.shapes.Count;
        if (total <= 0)
        {
            Debug.LogError("[PieceManager] PieceSet.shapes�� ��� �ֽ��ϴ�.");
            return;
        }

        // �׷� ũ��� shapes ���� �ʰ��� �� ����
        int group = Mathf.Min(_bagGroupSize, total);

        // 0..total-1 �߿��� group���� '�ߺ� ����' �̾� ��´�.
        // total�� group���� ũ�� �� �������� �����ϰ� group���� ���ø�.
        // total == group�̸� ��ǻ� ��ü�� ������ �Ͱ� ����.
        FillBagWithUniqueRandomIndices(total, group, _bag);

        // ����(Fisher-Yates)
        Shuffle(_bag);
    }

    private static void FillBagWithUniqueRandomIndices(int total, int take, List<int> outList)
    {
        // ȿ������ ����ũ ���ø�: ������ ����Ʈ ����� ���� �� �պκ� take�� ���
        // (total�� ���� ũ�� �ʴٴ� ����. ���� ��Ʈ�� ���� ����)
        List<int> tmp = s_tmpIndices;
        tmp.Clear();
        for (int i = 0; i < total; i++) tmp.Add(i);
        Shuffle(tmp);
        for (int i = 0; i < take; i++) outList.Add(tmp[i]);
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool ValidatePieceSet()
    {
        if (_pieceSet == null)
        {
            Debug.LogError("[PieceManager] PieceSetSO�� �Ҵ���� �ʾҽ��ϴ�.");
            return false;
        }
        if (_pieceSet.shapes == null || _pieceSet.shapes.Count == 0)
        {
            Debug.LogError("[PieceManager] PieceSetSO.shapes�� ��� �ֽ��ϴ�. �ּ� 1�� �̻� ����ϼ���.");
            return false;
        }
        return true;
    }

    // GC ���̱� ���� static �ӽ� ����
    private static readonly List<int> s_tmpIndices = new(64);
}