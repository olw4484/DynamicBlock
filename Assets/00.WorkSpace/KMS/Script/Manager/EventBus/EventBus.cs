using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// ================================
// Project : DynamicBlock
// Script  : EventBus.cs
// Desc    : 간단 이벤트 허브
// ================================
public static class EventBus
{
    public static Action OnHandRefilled;
    public static Action<int> OnScoreChanged;
    public static Action<int> OnComboChanged;
    public static Action OnBoardCleared;
    public static Action OnGameOver;
    public static Action OnPiecePlaced;

    // 발행 헬퍼(Null-conditional로 NRE 방지)
    public static void PublishHandRefilled() => OnHandRefilled?.Invoke();
    public static void PublishScoreChanged(int v) => OnScoreChanged?.Invoke(v);
    public static void PublishComboChanged(int v) => OnComboChanged?.Invoke(v);
    public static void PublishBoardCleared() => OnBoardCleared?.Invoke();
    public static void PublishGameOver() => OnGameOver?.Invoke();
    public static void PublishPiecePlaced() => OnPiecePlaced?.Invoke();
}
