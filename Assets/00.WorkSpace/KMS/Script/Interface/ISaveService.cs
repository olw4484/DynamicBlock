using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Script  : ISaveService.cs
// Desc    : 세이브기능 외부 API 계약
// ================================

public interface ISaveService
{
    GameData Data { get; }
    bool LoadOrCreate();
    void Save();
    void ResetData();
}