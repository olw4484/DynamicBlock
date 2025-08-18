using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : ScoreManager.cs
// Desc    : ��ġ/Ŭ����/�޺� ���ھ�
// ================================
public class ScoreManager : MonoBehaviour, IManager
{
    [SerializeField] private int _score;
    [SerializeField] private int _combo; // ���� Ŭ���� �� ���

    public void PreInit() { }
    public void Init() { _score = 0; _combo = 0; }
    public void PostInit() { EventBus.PublishScoreChanged(_score); }

    public void OnPlaced(int cells) { _score += cells; EventBus.PublishScoreChanged(_score); }
    public void OnCleared(int lines)
    {
        if (lines > 0) { _combo++; _score += lines * 10 + _combo * 5; }
        else _combo = 0;
        EventBus.PublishScoreChanged(_score);
        EventBus.PublishComboChanged(_combo);
    }
}
