using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : PieceManager.cs
// Desc    : 손패/조각 생성/소진 (7-bag 랜덤)
// ================================
public class PieceManager : MonoBehaviour, IManager
{
    [SerializeField] private PieceSetSO _pieceSet;

    [Header("손패 설정")]
    [SerializeField, Min(1)] private int _handSize = 3;

    [Header("Bag 설정")]
    [Tooltip("한 묶음 크기(테트리스식 7-bag). shapes 수가 이 값보다 작으면 shapes 수를 사용.")]
    [SerializeField, Min(1)] private int _bagGroupSize = 7;

    // 손패는 Vector2Int[]로 유지 (배치에 바로 사용)
    private readonly List<Vector2Int[]> _hand = new();

    // 7-bag 인덱스 큐 (현재 묶음에서 아직 안 뽑힌 인덱스들)
    private readonly List<int> _bag = new();
    private int _bagCursor = 0; // 현재 bag에서 몇 번째 인덱스를 소비 중인지

    // =====================================
    // IManager
    // =====================================
    public void PreInit() { }

    public void Init()
    {
        if (!ValidatePieceSet()) return;

        RefillBagIfNeeded(force: true); // 시작 시 가방 채우기
        RefillHand();
        EventBus.OnHandRefilled?.Invoke();
    }

    public void PostInit() { }

    public IReadOnlyList<Vector2Int[]> CurrentHand => _hand;

    // =====================================
    // 외부 API
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
            RefillHand(); // 모두 소진 시 새 손패
            EventBus.OnHandRefilled?.Invoke();
        }
    }

    // =====================================
    // 내부 구현
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
                Debug.LogError("[PieceManager] RefillHand 실패: 유효한 조각을 충분히 뽑지 못했습니다.");
                break;
            }

            var cells = DrawNextShapeCells();
            if (cells == null || cells.Length == 0) { i--; continue; }

            _hand.Add(cells);
        }
    }

    /// <summary>
    /// 7-bag에서 다음 조각 하나를 뽑아 cells 반환
    /// </summary>
    private Vector2Int[] DrawNextShapeCells()
    {
        RefillBagIfNeeded();

        int shapeIndex = _bag[_bagCursor++];
        var shape = _pieceSet.shapes[shapeIndex];

        if (shape == null || shape.cells == null || shape.cells.Length == 0)
            return null;

        int rot = Random.Range(0, 4); // 0~3 랜덤
        return GridUtil.Rotate(shape.cells, rot);
    }

    /// <summary>
    /// bag이 모두 소비되었거나 비어있으면 새 묶음을 구성하고 셔플
    /// </summary>
    private void RefillBagIfNeeded(bool force = false)
    {
        if (!force && _bagCursor < _bag.Count) return;

        _bag.Clear();
        _bagCursor = 0;

        int total = _pieceSet.shapes.Count;
        if (total <= 0)
        {
            Debug.LogError("[PieceManager] PieceSet.shapes가 비어 있습니다.");
            return;
        }

        // 그룹 크기는 shapes 수를 초과할 수 없음
        int group = Mathf.Min(_bagGroupSize, total);

        // 0..total-1 중에서 group개를 '중복 없이' 뽑아 담는다.
        // total이 group보다 크면 매 묶음마다 랜덤하게 group개를 샘플링.
        // total == group이면 사실상 전체를 셔플한 것과 동일.
        FillBagWithUniqueRandomIndices(total, group, _bag);

        // 셔플(Fisher-Yates)
        Shuffle(_bag);
    }

    private static void FillBagWithUniqueRandomIndices(int total, int take, List<int> outList)
    {
        // 효율적인 유니크 샘플링: 간단히 리스트 만들고 셔플 후 앞부분 take개 사용
        // (total이 아주 크지 않다는 가정. 퍼즐 세트면 문제 없음)
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
            Debug.LogError("[PieceManager] PieceSetSO가 할당되지 않았습니다.");
            return false;
        }
        if (_pieceSet.shapes == null || _pieceSet.shapes.Count == 0)
        {
            Debug.LogError("[PieceManager] PieceSetSO.shapes가 비어 있습니다. 최소 1개 이상 등록하세요.");
            return false;
        }
        return true;
    }

    // GC 줄이기 위한 static 임시 버퍼
    private static readonly List<int> s_tmpIndices = new(64);
}