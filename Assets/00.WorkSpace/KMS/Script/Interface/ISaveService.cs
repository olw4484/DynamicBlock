using System;
using UnityEngine;

// ================================
// Script  : ISaveService.cs
// Desc    : ���̺� ��� �ܺ� API ���
// ================================
public interface ISaveService
{
    // Core
    GameData Data { get; }
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

    bool TryConsumeDownedPending(out int score, double ttlSeconds = 0);
    void MarkDownedPending(int score);
    void ClearClassicRun();
    void SkipNextSnapshot(string reason = null);
}
