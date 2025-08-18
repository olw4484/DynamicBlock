using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// ================================
// Project : DynamicBlock
// Script  : EventBus.cs
// Desc    : ���� �̺�Ʈ ���
// ================================
public static class EventBus
{
    public static Action OnHandRefilled;
    public static Action<int> OnScoreChanged;
    public static Action<int> OnComboChanged;
    public static Action OnBoardCleared;
    public static Action OnGameOver;
    public static Action OnPiecePlaced;
}
