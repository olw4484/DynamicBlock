using System;
using UnityEngine;

// ================================
// Script  : ISaveService.cs
// Desc    : ���̺� ��� �ܺ� API ���
// ================================
public interface ISaveService
{
    // Core
    GameData Data { get; }     // ���� ��Ÿ�� GameData (read-only ����)
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
