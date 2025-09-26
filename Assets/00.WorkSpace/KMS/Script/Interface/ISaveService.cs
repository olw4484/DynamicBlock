using System;
using UnityEngine;

// ================================
// Script  : ISaveService.cs
// Desc    : 세이브 기능 외부 API 계약
// ================================
public interface ISaveService
{
    // Core
    GameData Data { get; }     // 현재 런타임 GameData (read-only 참조)
    bool LoadOrCreate();
    void Save();
    void ResetData();
    void UpdateClassicScore(int score);
    void ClearRunState(bool save);

    // Locale
    void SetLanguageIndex(int index);

    // Events
    event Action<GameData> AfterLoad;
    event Action<GameData> AfterSave;
}
