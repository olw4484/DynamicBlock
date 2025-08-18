using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : IManager.cs
// Desc    : �Ŵ��� ���� �������̽�
// ================================
public interface IManager
{
    void PreInit();   // Config, ���� ����
    void Init();      // ��Ÿ�� �ʱ� ���� ����
    void PostInit();  // UI ����, �̺�Ʈ ����
}
