using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 어드벤처 모드 - 과일 목표 UI 컨트롤러.
/// 활성화된 과일만 1번 슬롯부터 순서대로 배치하고, 아이콘/개수를 표시한다.
/// </summary>
public class AdventureFruitProgress : MonoBehaviour
{
    [SerializeField] private int maxSlots = 5; // FRUIT_COUNT
    [SerializeField] private string slotPrefix = "Adven_Fruit_"; // 자식 슬롯 이름 접두사

    [Serializable]
    private class Slot
    {
        public GameObject root;
        public Image      icon;
        public TMP_Text   amountText;
    }

    [SerializeField] private List<Slot> _slots = new();

    private int[] _targetCounts; // 맵 목표치
    private int[] _currentCounts; // 실시간 달성치(필요 시 사용)
    private int[] _enabledIndexMap; // 화면 0..n-1 → 실제 fruitIndex 매핑


    /// <summary>활성화된 과일 배열/목표치/아이콘으로 UI 초기화.</summary>
    public void Initialize(bool[] enabled, int[] targets, Sprite[] fruitIcons)
    {
        if (_slots.Count == 0)
        {
            Debug.LogWarning("[AdvFruit] slots 비어있음(수동 매핑 필요).");
            return;
        }
        
        int enabledCount = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].root.SetActive(false);
        }

        // 안전 복사
        int n = Mathf.Min(enabled?.Length ?? 0, targets?.Length ?? 0);
        _targetCounts  = new int[n];
        _currentCounts = new int[n];
        _enabledIndexMap = new int[_slots.Count]; // 화면 슬롯 → 실제 fruit index, 미활성은 -1
        Array.Fill(_enabledIndexMap, -1);

        for (int fi = 0; fi < n; fi++)
        {
            if (!enabled[fi]) continue; // 비활성 과일 skip
            int slotIdx = enabledCount;
            if (slotIdx >= _slots.Count) break;

            var s = _slots[slotIdx];
            s.root.SetActive(true);

            // 아이콘
            var icon = (fruitIcons != null && fi < fruitIcons.Length) ? fruitIcons[fi] : null;
            if (s.icon) s.icon.sprite = icon;

            // 목표/현재값
            int target = Mathf.Max(0, targets[fi]);
            _targetCounts[fi] = target;
            _currentCounts[fi] = 0;

            if (s.amountText) s.amountText.text = $"{target}";

            _enabledIndexMap[slotIdx] = fi;
            enabledCount++;
        }
    }

    /// <summary>실시간 진행 값 반영: fruitIndex 기준.</summary>
    public void SetCurrent(int fruitIndex, int current)
    {
        if (_targetCounts == null || _currentCounts == null) return;
        if (fruitIndex < 0 || fruitIndex >= _currentCounts.Length) return;

        _currentCounts[fruitIndex] = Mathf.Max(0, current);
        // 현재 fruitIndex가 어느 슬롯에 매핑되어 있는지 찾아서 라벨 갱신
        for (int slot = 0; slot < _enabledIndexMap.Length; slot++)
        {
            if (_enabledIndexMap[slot] != fruitIndex) continue;
            var s = (slot >= 0 && slot < _slots.Count) ? _slots[slot] : null;
            if (s == null || s.amountText == null) return;

            int target = Mathf.Max(0, _targetCounts[fruitIndex]);
            s.amountText.text = $"{target - _currentCounts[fruitIndex]}";
            return;
        }
    }

    /// <summary>일괄 진행 값 반영(저장값/런타임 값 등).</summary>
    public void SetCurrents(int[] currents)
    {
        if (currents == null || _targetCounts == null) return;
        int n = Mathf.Min(currents.Length, _currentCounts?.Length ?? 0);
        for (int i = 0; i < n; i++)
        {
            _currentCounts[i] = Mathf.Max(0, currents[i]);
        }
        // 모든 표시 갱신
        for (int slot = 0; slot < _slots.Count; slot++)
        {
            int fi = (slot < _enabledIndexMap.Length) ? _enabledIndexMap[slot] : -1;
            if (fi < 0) continue;
            var s = _slots[slot];
            if (!s.amountText) continue;
            int target = Mathf.Max(0, _targetCounts[fi]);
            s.amountText.text = $"{target - _currentCounts[fi]}";
        }
    }
}

public static class TransformExtensions
{
    public static bool TryGetComponentInChildren<T>(this Transform t, out T comp) where T : Component
    {
        comp = t.GetComponentInChildren<T>(true);
        return comp != null;
    }
}
