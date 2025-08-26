using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : ISaveService.cs
// Desc    : ���̺��� �ܺ� API ���
// ================================

public interface ISaveService
{
    GameData Data { get; }
    bool LoadOrCreate();
    void Save();
    void ResetData();
}