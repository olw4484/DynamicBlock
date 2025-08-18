using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : ScoreManager.cs
// Desc    : 배치/클리어/콤보 스코어
// ================================
public class ScoreManager : MonoBehaviour, IManager
{
    [SerializeField] private int _score;
    [SerializeField] private int _combo; // 연속 클리어 시 상승

    public void PreInit() { }
    public void Init() { _score = 0; _combo = 0; }
    public void PostInit() { EventBus.OnScoreChanged?.Invoke(_score); }

    public void OnPlaced(int cells) { _score += cells; EventBus.OnScoreChanged?.Invoke(_score); }
    public void OnCleared(int lines)
    {
        if (lines > 0) { _combo++; _score += lines * 10 + _combo * 5; }
        else _combo = 0;
        EventBus.OnScoreChanged?.Invoke(_score);
        EventBus.OnComboChanged?.Invoke(_combo);
    }
}
