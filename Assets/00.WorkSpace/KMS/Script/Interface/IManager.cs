using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : IManager.cs
// Desc    : �Ŵ��� ���� �������̽� (3-Phase Bootstrap)
// ================================
public interface IManager
{
    int Order { get; }     // �ʱ�ȭ �켱���� (�������� ����)
    void PreInit();        // Config, ���� ����
    void Init();           // ��Ÿ�� �ʱ� ����/��ü ����
    void PostInit();       // UI ����, �̺�Ʈ ����
}

// �� ������ ó��
public interface ITickable { void Tick(float dt); }

// ����/���ҽ� ����
public interface ITeardown { void Teardown(); }